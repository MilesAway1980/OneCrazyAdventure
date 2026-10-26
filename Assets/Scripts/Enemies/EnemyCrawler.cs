using System.Collections.Generic;
using UnityEngine;

public enum NarrowGapBehavior
{
    TreatAsSolid,
    TurnAround
}

// Crawls around the nearest body of tiles. It reads that body's outline, pushes it out by the enemy's radius, and
// hands the resulting closed loop to EnemyPathFollow as a list of destinations - so the enemy simply follows a path
// that hugs the surface instead of working out walls and corners as it goes.
//
// The tiles need a CompositeCollider2D on the Ground layer (a Tilemap Collider 2D set to Used By Composite).
[RequireComponent(typeof(EnemyPathFollow))]
public class EnemyCrawler : Enemy
{
    [Tooltip("Speed along the path. Passed to Enemy Path Follow's Velocity.")]
    [SerializeField] private float speed = 2f;

    [Tooltip("Which way around the tiles it crawls. Can be flipped at any time.")]
    public bool clockwise = true;

    [Tooltip("How far away to look for a body of tiles to crawl around.")]
    [SerializeField] private float searchDistance = 20f;

    [Tooltip("Turn the sprite so it stays 'feet down' relative to whatever it's crawling on.")]
    [SerializeField] private bool rotateToSurface = true;

    [Tooltip("Degrees per second the sprite can turn at.")]
    [SerializeField] private float rotationSpeed = 540f;

    [Tooltip("Build a new path whenever the tiles around it change, such as when some are destroyed. You can also call RebuildPath yourself.")]
    [SerializeField] private bool rebuildWhenTilesChange = true;

    [Tooltip("Seconds between checks for tile changes.")]
    [SerializeField] private float changeCheckInterval = 0.25f;

    [Tooltip("What the enemy does where two bodies of tiles are too close for it to fit between them. Turn Around: it travels as far as it fits, then goes back. Treat As Solid: the gap counts as filled in, so it carries on around the other body of tiles.")]
    [SerializeField] private NarrowGapBehavior narrowGaps = NarrowGapBehavior.TurnAround;

    // Rounding a corner of the tiles is drawn as a few straight pieces; smaller steps follow the curve more closely.
    private const float ArcStepDegrees = 30f;

    // The tiles might not have finished generating their outlines on the very first frame.
    private const int BuildAttempts = 5;

    // The path is checked with a circle a little smaller than the enemy, so it isn't thrown by the path sitting
    // exactly against the surface it's following (or by the corners being drawn as straight pieces).
    private const float ClearanceTolerance = 0.03f;

    // A usable stretch of path shorter than this isn't worth having the enemy travel along.
    private const float MinRunLength = 0.02f;

    // How near the end of one stretch of path has to be to the start of another for them to count as meeting at
    // the same pinch point. They never land exactly together, because each is found by sweeping a slightly
    // smaller circle than the real one.
    private const float JoinTolerance = 0.25f;

    private EnemyPathFollow path;
    private List<List<Vector2>> surfaces;
    private bool pathBuilt;
    private int attemptsLeft = BuildAttempts;
    private Transform crawlContainer;

    // The tile colliders the current path was built from, and a fingerprint of their outlines at the time, so a
    // change to any of them can be spotted.
    private List<CompositeCollider2D> nearbyTiles = new List<CompositeCollider2D>();
    private readonly List<Vector2> fingerprintPoints = new List<Vector2>();
    private int tilesFingerprint;
    private int pendingFingerprint;
    private float changeCheckTimer;

    protected override void Start()
    {
        base.Start();

        path = GetComponent<EnemyPathFollow>();

        if (!path)
        {
            path = gameObject.AddComponent<EnemyPathFollow>();
        }

        // The loop is already clear of the tiles and the enemy just glides along it, so neither the route finder
        // nor physics has any part to play.
        path.avoidObstacles = false;
        path.useMomentum = false;
        path.velocity = speed;

        if (rigidBody2D)
        {
            rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
            rigidBody2D.gravityScale = 0;
        }
    }

    protected override void Update()
    {
        // Kept in step every frame so changing either during play takes effect straight away.
        path.velocity = speed;
        path.reversePath = !clockwise;

        if (!pathBuilt && attemptsLeft > 0)
        {
            attemptsLeft--;
            pathBuilt = TryBuildPath(out var problem);

            if (!pathBuilt && attemptsLeft == 0)
            {
                Debug.LogError($"{name}: EnemyCrawler couldn't build a path. {problem}", this);
            }
        }

        if (pathBuilt && rebuildWhenTilesChange)
        {
            CheckForTileChanges();
        }

        if (pathBuilt && rotateToSurface)
        {
            TurnToSurface();
        }
    }

    // Traces the tiles again from where the enemy is now and swaps in the new path. This happens by itself when the
    // tiles change; call it directly after changing the map in some way that isn't picked up. If a new path can't be
    // built the old one is kept, so the enemy is never left without one.
    public bool RebuildPath()
    {
        if (!path)
        {
            return false;
        }

        if (TryBuildPath(out var problem))
        {
            pathBuilt = true;
            return true;
        }

        // Don't try again until the tiles change again.
        tilesFingerprint = pendingFingerprint = FingerprintOfNearbyTiles();
        Debug.LogWarning($"{name}: EnemyCrawler couldn't rebuild its path, so it's keeping the old one. {problem}", this);
        return false;
    }

    private void CheckForTileChanges()
    {
        changeCheckTimer -= Time.deltaTime;

        if (changeCheckTimer > 0f)
        {
            return;
        }

        changeCheckTimer = changeCheckInterval;

        var fingerprint = FingerprintOfNearbyTiles();

        if (fingerprint == tilesFingerprint)
        {
            pendingFingerprint = fingerprint;
            return;
        }

        // Wait until the tiles have stopped changing, so a blast that takes out several over a few frames only
        // rebuilds the path once, from the finished result.
        if (fingerprint != pendingFingerprint)
        {
            pendingFingerprint = fingerprint;
            return;
        }

        RebuildPath();
    }

    private int FingerprintOfNearbyTiles()
    {
        var hash = new System.HashCode();

        foreach (var tiles in nearbyTiles)
        {
            // A tile collider that has been destroyed outright counts as a change too.
            hash.Add(tiles ? 1 : 0);

            if (!tiles)
            {
                continue;
            }

            hash.Add(tiles.pathCount);

            for (var i = 0; i < tiles.pathCount; i++)
            {
                tiles.GetPath(i, fingerprintPoints);

                foreach (var point in fingerprintPoints)
                {
                    hash.Add(Mathf.RoundToInt(point.x * 100f));
                    hash.Add(Mathf.RoundToInt(point.y * 100f));
                }
            }
        }

        return hash.ToHashCode();
    }

    private bool TryBuildPath(out string problem)
    {
        var radius = MeasureRadius();
        var obstacles = LayerMask.GetMask("Ground");
        Vector2 position = transform.position;
        var tilesFound = new List<CompositeCollider2D>();
        var neighbours = new List<List<Vector2>>();

        // Bodies of tiles closer than this to each other can leave a gap the enemy can't fit through.
        var neighbourReach = narrowGaps == NarrowGapBehavior.TreatAsSolid ? radius * 2f + 0.5f : 0f;

        if (!TryFindSurface(position, obstacles, tilesFound, neighbourReach, neighbours, out var outline, out var side, out problem))
        {
            return false;
        }

        var loop = OffsetLoop(outline, side, radius);
        RemoveCloseNeighbours(loop);

        if (loop.Count < 3)
        {
            problem = "The outline around the tiles was too small or too tight for the enemy to fit around.";
            return false;
        }

        // Forward along the list is clockwise, so Clockwise just chooses whether Enemy Path Follow runs it reversed.
        var reversed = SignedArea(loop) > 0f;

        if (reversed)
        {
            loop.Reverse();
        }

        // Sweep the enemy's circle along every part of the loop. Anywhere it would touch a tile (another body that's
        // close but not touching, for instance, leaving a gap too narrow to fit through) the path is cut there.
        var clearRadius = Mathf.Max(radius - ClearanceTolerance, 0.01f);
        var runs = FindClearRuns(loop, clearRadius, obstacles);

        List<Vector2> route;
        bool closedRoute;

        if (runs == null)
        {
            // Clear all the way round: a closed loop.
            route = loop;
            closedRoute = true;
        }
        else
        {
            if (narrowGaps == NarrowGapBehavior.TreatAsSolid)
            {
                AddRunsAroundNeighbours(runs, neighbours, side, reversed, radius, clearRadius, obstacles);
            }

            var startRun = NearestRun(runs, position);

            if (startRun < 0)
            {
                problem = "There's no stretch of the outline wide enough for the enemy to travel along.";
                return false;
            }

            if (narrowGaps == NarrowGapBehavior.TreatAsSolid)
            {
                // Stretches that end at the same pinch point are joined up, carrying the path on round the next body.
                route = ChainRuns(runs, startRun, clearRadius, obstacles, out closedRoute);
                closedRoute &= route.Count >= 3;
            }
            else
            {
                // The enemy can only travel the stretch it's on, so it goes to the end of that, turns back, and goes
                // to the other end.
                route = runs[startRun];
                closedRoute = false;
            }
        }

        // The enemy starts out by joining the route at whichever point of it is closest.
        DistanceToPath(route, closedRoute, position, out var edge, out var nearest);
        var startIndex = InsertPoint(route, closedRoute, edge, nearest);

        List<int> order;

        if (closedRoute)
        {
            order = new List<int>(route.Count);

            for (var i = 0; i < route.Count; i++)
            {
                order.Add(i);
            }
        }
        else
        {
            order = BackAndForth(route.Count);
        }

        var oldContainer = crawlContainer;

        crawlContainer = new GameObject($"{name} Crawl Path").transform;
        crawlContainer.SetParent(transform, true);

        var markers = new List<GameObject>(route.Count);

        foreach (var point in route)
        {
            var marker = new GameObject("Crawl Point");
            marker.transform.SetParent(crawlContainer, false);
            marker.transform.position = new Vector3(point.x, point.y, transform.position.z);
            markers.Add(marker);
        }

        // Points the enemy passes twice (the ends of a back-and-forth path) share a marker.
        var destinations = new List<GameObject>(order.Count);

        foreach (var index in order)
        {
            destinations.Add(markers[index]);
        }

        path.SetNewPath(destinations);
        path.whichObjectFirst = markers[startIndex];
        path.currentDestination = startIndex;

        // Only now that the new path is in place is it safe to throw the old markers away.
        if (oldContainer)
        {
            Destroy(oldContainer.gameObject);
        }

        nearbyTiles = tilesFound;
        tilesFingerprint = pendingFingerprint = FingerprintOfNearbyTiles();

        surfaces = new List<List<Vector2>> { outline };
        surfaces.AddRange(neighbours);
        problem = null;
        return true;
    }

    // Finds the closest loop of tile outline to the enemy, and which side of it is open space.
    // If neighbourReach is above 0, the other bodies of tiles within that distance of it (and of each other) are
    // collected into neighbours too, with any that touch each other already joined up.
    private bool TryFindSurface(
        Vector2 position, int obstacles, List<CompositeCollider2D> tilesFound, float neighbourReach,
        List<List<Vector2>> neighbours, out List<Vector2> outline, out int side, out string problem)
    {
        outline = null;
        side = 1;
        problem = $"There are no tiles on the Ground layer within {searchDistance} units.";

        var points = new List<Vector2>();
        var loops = new List<List<Vector2>>();

        foreach (var tiles in Physics2D.OverlapCircleAll(position, searchDistance, obstacles))
        {
            if (tiles is not CompositeCollider2D composite)
            {
                problem = "The nearby tiles don't have a CompositeCollider2D. Set the Tilemap Collider 2D to Used By Composite.";
                continue;
            }

            tilesFound.Add(composite);

            for (var i = 0; i < composite.pathCount; i++)
            {
                composite.GetPath(i, points);

                if (points.Count < 3)
                {
                    continue;
                }

                var loop = new List<Vector2>(points.Count);

                foreach (var local in points)
                {
                    loop.Add(composite.transform.TransformPoint(local));
                }

                loops.Add(loop);
            }
        }

        var bestDistance = float.MaxValue;
        var bestIndex = -1;
        var bestEdge = 0;
        var bestClosest = position;

        for (var i = 0; i < loops.Count; i++)
        {
            var distance = DistanceToPath(loops[i], true, position, out var edge, out var closest);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
                bestEdge = edge;
                bestClosest = closest;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        outline = loops[bestIndex];
        loops.RemoveAt(bestIndex);

        // Open space is whichever side of the nearest edge the enemy is standing on.
        var edgeDirection = outline[(bestEdge + 1) % outline.Count] - outline[bestEdge];
        side = Cross(edgeDirection, position - bestClosest) >= 0f ? 1 : -1;

        MergeTouchingLoops(outline, loops);

        if (neighbourReach > 0f)
        {
            CollectNeighbours(outline, loops, neighbourReach, neighbours);
        }

        if (bestDistance < 0.01f)
        {
            outline = null;
            problem = "The enemy is touching or inside the tiles. Place it in the open next to them.";
            return false;
        }

        return true;
    }

    // Tiles that only touch at a corner (a ramp meeting the end of a row of blocks, say) come out of the composite
    // collider as separate loops, but to the enemy they're one object: crawling along the top of the blocks carries
    // straight on up the ramp. So any loop that shares a corner with this one is spliced into it, repeatedly until
    // nothing else touches. Loops wound the opposite way are cavities rather than neighbouring bodies, so they're
    // left alone.
    private static void MergeTouchingLoops(List<Vector2> main, List<List<Vector2>> others)
    {
        var direction = Mathf.Sign(SignedArea(main));
        var merged = true;

        while (merged)
        {
            merged = false;

            // Only loops whose bounding boxes meet can possibly share a corner, which saves comparing every pair of
            // corners on every loop nearby.
            var mainBounds = Inflate(BoundsOf(main), 0.01f);

            for (var i = 0; i < others.Count; i++)
            {
                if (!mainBounds.Overlaps(BoundsOf(others[i])) ||
                    Mathf.Sign(SignedArea(others[i])) != direction ||
                    !TryFindSharedVertex(main, others[i], out var mainIndex, out var otherIndex))
                {
                    continue;
                }

                // Walk the main loop up to the shared corner, all the way around the other loop, then carry on.
                var detour = new List<Vector2>(others[i].Count);

                for (var step = 1; step < others[i].Count; step++)
                {
                    detour.Add(others[i][(otherIndex + step) % others[i].Count]);
                }

                detour.Add(main[mainIndex]);
                main.InsertRange(mainIndex + 1, detour);

                others.RemoveAt(i);
                merged = true;
                break;
            }
        }
    }

    private static bool TryFindSharedVertex(List<Vector2> a, List<Vector2> b, out int indexInA, out int indexInB)
    {
        for (indexInA = 0; indexInA < a.Count; indexInA++)
        {
            for (indexInB = 0; indexInB < b.Count; indexInB++)
            {
                if ((a[indexInA] - b[indexInB]).sqrMagnitude < 0.0001f)
                {
                    return true;
                }
            }
        }

        indexInA = indexInB = -1;
        return false;
    }

    // Pushes a loop out to the open side by radius: rounding the corners that stick out, and meeting at a point
    // in the corners that fold in. side is 1 if open space is on the left as you walk the loop, -1 if on the right.
    private static List<Vector2> OffsetLoop(List<Vector2> loop, int side, float radius)
    {
        var result = new List<Vector2>();
        var count = loop.Count;

        for (var i = 0; i < count; i++)
        {
            var previous = loop[(i + count - 1) % count];
            var vertex = loop[i];
            var next = loop[(i + 1) % count];

            var inDirection = (vertex - previous).normalized;
            var outDirection = (next - vertex).normalized;

            // Pointing out toward the open side.
            var inNormal = side * new Vector2(-inDirection.y, inDirection.x);
            var outNormal = side * new Vector2(-outDirection.y, outDirection.x);

            var turn = Cross(inDirection, outDirection);

            if (Mathf.Abs(turn) < 0.0001f && Vector2.Dot(inDirection, outDirection) > 0f)
            {
                continue;
            }

            if (turn * side < 0f)
            {
                var angle = Vector2.SignedAngle(inNormal, outNormal);
                var steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(angle) / ArcStepDegrees));

                for (var step = 0; step <= steps; step++)
                {
                    var rotated = Quaternion.Euler(0f, 0f, angle * step / steps) * inNormal;
                    result.Add(vertex + (Vector2)rotated * radius);
                }
            }
            else
            {
                var miter = (inNormal + outNormal) / Mathf.Max(1f + Vector2.Dot(inNormal, outNormal), 0.1f);
                result.Add(vertex + miter * radius);
            }
        }

        return result;
    }

    // Sweeps a circle of the given radius along every edge of the loop and works out where it can't go because it
    // would touch a tile. Returns null if the whole loop is clear. Otherwise returns the stretches that are, each as
    // a list of points in order: a blocked edge ends one stretch where the circle first touches something, and
    // starts the next at the last spot the circle can reach coming from the other end.
    private static List<List<Vector2>> FindClearRuns(List<Vector2> loop, float radius, int obstacles)
    {
        var count = loop.Count;

        // How far along each edge the circle gets from its start, and from its end, before touching a tile.
        // -1 means the edge is clear all the way.
        var fromStart = new float[count];
        var fromEnd = new float[count];
        var firstBlocked = -1;

        for (var i = 0; i < count; i++)
        {
            fromStart[i] = -1f;
            fromEnd[i] = -1f;

            var start = loop[i];
            var end = loop[(i + 1) % count];
            var length = Vector2.Distance(start, end);

            if (length < 0.0001f)
            {
                continue;
            }

            var direction = (end - start) / length;
            var forward = Physics2D.CircleCast(start, radius, direction, length, obstacles);

            if (!forward)
            {
                continue;
            }

            var backward = Physics2D.CircleCast(end, radius, -direction, length, obstacles);

            fromStart[i] = forward.distance;
            fromEnd[i] = backward ? backward.distance : length;

            if (firstBlocked < 0)
            {
                firstBlocked = i;
            }
        }

        if (firstBlocked < 0)
        {
            return null;
        }

        var runs = new List<List<Vector2>>();
        List<Vector2> run = null;

        // Walk the loop starting just after a blocked edge, so no stretch is ever split by where the list begins.
        var k = firstBlocked;
        var kStart = loop[k];
        var kEnd = loop[(k + 1) % count];
        var kDirection = (kEnd - kStart).normalized;

        if (fromEnd[k] > MinRunLength)
        {
            run = new List<Vector2> { kEnd - kDirection * fromEnd[k], kEnd };
        }

        for (var step = 1; step < count; step++)
        {
            var i = (k + step) % count;
            var start = loop[i];
            var end = loop[(i + 1) % count];

            if (fromStart[i] < 0f)
            {
                run ??= new List<Vector2> { start };
                run.Add(end);
                continue;
            }

            var direction = (end - start).normalized;

            if (fromStart[i] > MinRunLength)
            {
                run ??= new List<Vector2> { start };
                run.Add(start + direction * fromStart[i]);
            }

            if (run != null)
            {
                runs.Add(run);
                run = null;
            }

            if (fromEnd[i] > MinRunLength)
            {
                run = new List<Vector2> { end - direction * fromEnd[i], end };
            }
        }

        // The edge the walk began after is the last one reached: its first stretch finishes whatever run got there.
        if (fromStart[k] > MinRunLength)
        {
            run ??= new List<Vector2> { kStart };
            run.Add(kStart + kDirection * fromStart[k]);
        }

        if (run != null)
        {
            runs.Add(run);
        }

        runs.RemoveAll(candidate => candidate.Count < 2);
        return runs;
    }

    // Gathers the loops that could pinch against the main body: any within reach of its bounding box, then any within
    // reach of those, and so on. Loops that touch each other are joined up so each body comes out as one loop.
    private static void CollectNeighbours(
        List<Vector2> main, List<List<Vector2>> loops, float reach, List<List<Vector2>> neighbours)
    {
        var nearby = new List<List<Vector2>>();
        var searched = new List<Rect> { Inflate(BoundsOf(main), reach) };
        var grew = true;

        while (grew)
        {
            grew = false;

            for (var i = loops.Count - 1; i >= 0; i--)
            {
                var bounds = BoundsOf(loops[i]);

                if (!searched.Exists(area => area.Overlaps(bounds)))
                {
                    continue;
                }

                nearby.Add(loops[i]);
                searched.Add(Inflate(bounds, reach));
                loops.RemoveAt(i);
                grew = true;
            }
        }

        while (nearby.Count > 0)
        {
            var body = nearby[0];
            nearby.RemoveAt(0);

            MergeTouchingLoops(body, nearby);
            neighbours.Add(body);
        }
    }

    // Works out the stretches the enemy can travel around each neighbouring body of tiles, the same way as for the
    // main one, so that they can be joined up where the gap between two bodies pinches shut.
    private static void AddRunsAroundNeighbours(
        List<List<Vector2>> runs, List<List<Vector2>> neighbours, int side, bool reversed,
        float radius, float clearRadius, int obstacles)
    {
        foreach (var body in neighbours)
        {
            var loop = OffsetLoop(body, side, radius);
            RemoveCloseNeighbours(loop);

            if (loop.Count < 3)
            {
                continue;
            }

            // Walked the same way round as the main body, so the open side stays on the same side of the direction
            // of travel all the way along.
            if (reversed)
            {
                loop.Reverse();
            }

            var bodyRuns = FindClearRuns(loop, clearRadius, obstacles);

            if (bodyRuns != null)
            {
                runs.AddRange(bodyRuns);
            }
        }
    }

    // Strings stretches of path together where one ends at the same pinch point another starts, so the path carries
    // on along the next body of tiles instead of stopping. Returns the chain that contains runs[start] - closed if
    // it joins back up on itself, otherwise open, in which case it's the whole chain, not just the part after start.
    private static List<Vector2> ChainRuns(List<List<Vector2>> runs, int start, float radius, int obstacles, out bool closed)
    {
        var count = runs.Count;
        var next = new int[count];
        var previous = new int[count];

        // joints[i] is where run i meets the run after it.
        var joints = new Vector2[count];

        for (var i = 0; i < count; i++)
        {
            next[i] = -1;
            previous[i] = -1;
        }

        var candidates = new List<(float distance, int source, int target)>();

        for (var source = 0; source < count; source++)
        {
            var end = runs[source][runs[source].Count - 1];

            for (var target = 0; target < count; target++)
            {
                var distance = Vector2.Distance(end, runs[target][0]);

                if (distance <= JoinTolerance)
                {
                    candidates.Add((distance, source, target));
                }
            }
        }

        // Closest pairs first, each run's end and start used only once.
        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

        foreach (var candidate in candidates)
        {
            if (next[candidate.source] >= 0 || previous[candidate.target] >= 0)
            {
                continue;
            }

            if (!TryJoin(runs[candidate.source], runs[candidate.target], radius, obstacles, out var joint))
            {
                continue;
            }

            next[candidate.source] = candidate.target;
            previous[candidate.target] = candidate.source;
            joints[candidate.source] = joint;
        }

        // Back up to where the chain begins, or all the way round to the start if it's a loop.
        var head = start;
        closed = false;

        while (previous[head] >= 0)
        {
            head = previous[head];

            if (head == start)
            {
                closed = true;
                break;
            }
        }

        var sequence = new List<int>();
        var current = head;

        do
        {
            sequence.Add(current);
            current = next[current];
        }
        while (current >= 0 && current != head);

        var chain = new List<Vector2>(runs[sequence[0]]);

        for (var i = 1; i < sequence.Count; i++)
        {
            chain[chain.Count - 1] = joints[sequence[i - 1]];

            var run = runs[sequence[i]];

            for (var k = 1; k < run.Count; k++)
            {
                chain.Add(run[k]);
            }
        }

        if (closed)
        {
            // The last run's end and the first run's start are the same joint, so it only needs to appear once.
            chain[0] = joints[sequence[sequence.Count - 1]];
            chain.RemoveAt(chain.Count - 1);
        }

        return chain;
    }

    // Works out where two stretches of path meet. Ideally that's exactly where the two edges cross - the one spot
    // the enemy touches both bodies of tiles at once - but if they're too nearly parallel to say, halfway between
    // the two ends will do. Only joins them if the enemy can actually travel in and out of that point.
    private static bool TryJoin(List<Vector2> source, List<Vector2> target, float radius, int obstacles, out Vector2 joint)
    {
        var previous = source[source.Count - 2];
        var end = source[source.Count - 1];
        var start = target[0];
        var next = target[1];

        var arriving = (end - previous).normalized;
        var leaving = (next - start).normalized;
        var cross = Cross(arriving, leaving);

        if (Mathf.Abs(cross) > 0.15f)
        {
            var along = Cross(start - previous, leaving) / cross;
            var crossing = previous + arriving * along;

            if (Vector2.Distance(crossing, end) <= JoinTolerance &&
                Vector2.Distance(crossing, start) <= JoinTolerance &&
                PathFinder2D.HasClearPath(previous, crossing, radius, obstacles) &&
                PathFinder2D.HasClearPath(crossing, next, radius, obstacles))
            {
                joint = crossing;
                return true;
            }
        }

        joint = (end + start) * 0.5f;

        return PathFinder2D.HasClearPath(previous, joint, radius, obstacles) &&
               PathFinder2D.HasClearPath(joint, next, radius, obstacles);
    }

    private static int NearestRun(List<List<Vector2>> runs, Vector2 position)
    {
        var best = -1;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < runs.Count; i++)
        {
            var distance = DistanceToPath(runs[i], false, position, out _, out _);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private static void RemoveCloseNeighbours(List<Vector2> loop)
    {
        for (var i = loop.Count - 1; i > 0; i--)
        {
            if ((loop[i] - loop[i - 1]).sqrMagnitude < 0.0001f)
            {
                loop.RemoveAt(i);
            }
        }
    }

    private static Rect BoundsOf(List<Vector2> points)
    {
        var min = points[0];
        var max = points[0];

        foreach (var point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Rect Inflate(Rect rect, float amount)
    {
        return Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);
    }

    // The order to visit a stretch of path with the given number of points so it's walked to one end, then back
    // to the other, and so on: 0, 1, 2 ... last, then back down to 1 (the loop then wraps round to 0).
    private static List<int> BackAndForth(int count)
    {
        var order = new List<int>(count * 2);

        for (var i = 0; i < count; i++)
        {
            order.Add(i);
        }

        for (var i = count - 2; i >= 1; i--)
        {
            order.Add(i);
        }

        return order;
    }

    // Adds the point to the path after the given edge's start (unless it's already at an end), returning its index.
    private static int InsertPoint(List<Vector2> points, bool closed, int edge, Vector2 point)
    {
        var end = closed ? (edge + 1) % points.Count : edge + 1;

        if (Vector2.Distance(point, points[edge]) < 0.05f)
        {
            return edge;
        }

        if (Vector2.Distance(point, points[end]) < 0.05f)
        {
            return end;
        }

        points.Insert(edge + 1, point);
        return edge + 1;
    }

    private void TurnToSurface()
    {
        Vector2 position = transform.position;

        // Up points away from the nearest surface, whichever body of tiles that belongs to.
        var nearest = float.MaxValue;
        var closest = position;

        foreach (var outline in surfaces)
        {
            var distance = DistanceToPath(outline, true, position, out _, out var candidate);

            if (distance < nearest)
            {
                nearest = distance;
                closest = candidate;
            }
        }

        var away = position - closest;

        if (away.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var target = Quaternion.Euler(0f, 0f, Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg - 90f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    private float MeasureRadius()
    {
        var radius = 0f;

        foreach (var col in GetComponents<Collider2D>())
        {
            var extents = col.bounds.extents;
            radius = Mathf.Max(radius, extents.x, extents.y);
        }

        return radius > 0f ? radius : 0.5f;
    }

    // Shortest distance from a point to a path, along with which edge (starting at that index) and where on it. A
    // closed path also has the edge joining its last point back round to its first.
    private static float DistanceToPath(List<Vector2> points, bool closed, Vector2 point, out int edge, out Vector2 closest)
    {
        var best = float.MaxValue;
        edge = 0;
        closest = point;

        var edges = closed ? points.Count : points.Count - 1;

        for (var i = 0; i < edges; i++)
        {
            var candidate = ClosestPointOnSegment(points[i], points[(i + 1) % points.Count], point);
            var distance = (candidate - point).sqrMagnitude;

            if (distance < best)
            {
                best = distance;
                edge = i;
                closest = candidate;
            }
        }

        return Mathf.Sqrt(best);
    }

    private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        var ab = b - a;
        var lengthSquared = ab.sqrMagnitude;

        if (lengthSquared < 0.000001f)
        {
            return a;
        }

        return a + ab * Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
    }

    // Positive if the loop runs anticlockwise.
    private static float SignedArea(List<Vector2> loop)
    {
        var area = 0f;

        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            area += a.x * b.y - b.x * a.y;
        }

        return area * 0.5f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}
