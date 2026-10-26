using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public enum GroundMoveType
{
    Walk,
    Jump,
    Fall
}

// One piece of a ground route. Walk: head sideways from `from` to `to`. Jump: leave the ground at `from` with the given
// velocity and land at `to`. Fall: carry on walking off the ledge at `from` and drop to `to`.
public struct GroundStep
{
    public GroundMoveType type;
    public Vector2 from;
    public Vector2 to;
    public float velocityX;
    public float velocityY;
}

// A move that was planned and then didn't work out when the enemy tried it. Routes steer away from moves like it.
public struct FailedMove
{
    public GroundMoveType type;
    public Vector2 from;
    public Vector2 to;
    public int count;
    public float until;
}

// How much time all ground enemies together may spend searching for routes in one frame. Searches that don't finish
// carry on next frame, so many enemies planning at once spread out instead of causing one big hitch.
public static class GroundPlanningBudget
{
    public static float MillisecondsPerFrame = 3f;

    // Even when the budget is spent, each search still gets this much, so none is starved by the others.
    private const float MinimumMilliseconds = 0.5f;

    private static int frame = -1;
    private static float used;

    public static float Available()
    {
        if (frame != Time.frameCount)
        {
            frame = Time.frameCount;
            used = 0f;
        }

        return Mathf.Max(MillisecondsPerFrame - used, MinimumMilliseconds);
    }

    public static void Spend(float milliseconds)
    {
        Available();
        used += milliseconds;
    }
}

// Finds routes for a box that walks along the ground, jumps up walls and across gaps, and falls off ledges. Like
// PathFinder2D it asks the physics engine as the search goes rather than baking anything, so routes always reflect
// the map as it is right now. Every jump and fall is played out step by step under gravity against the obstacles,
// so a route only ever contains moves that actually work.
//
// A search is started with BeginSearch and then given a little time each frame with Advance until it says it's done.
public class GroundPathFinder2D
{
    // Floors steeper than this (about 53 degrees) count as walls.
    private const float MinGroundNormal = 0.6f;

    // The box used for queries is a little smaller than the enemy so that sliding along a surface never counts as hitting it.
    private const float Skin = 0.04f;

    // Positions are kept just above the floor so that a box resting on it isn't touching it.
    private const float Lift = 0.02f;

    // A simulated flight is checked against the map once every few physics steps rather than every one.
    private const int StrideSteps = 3;
    private const int MaxFlightStrides = 120;

    // Preferring walking over jumping, and low jumps over high ones. Each unit of jump height costs this many seconds,
    // which is a lot more than walking a little further costs, so the enemy walks up close to a ledge and makes a
    // small jump rather than leaping early from far away with a big one.
    private const float JumpPenalty = 0.2f;
    private const float JumpHeightPenalty = 1.5f;

    // Jump heights tried run from this up to the maximum, each this many times the one before, and are then narrowed
    // down by halving this many times.
    private const float SmallestJump = 0.25f;
    private const float JumpHeightGrowth = 1.5f;
    private const int JumpRefinements = 5;

    // A jump is carried out a little higher than the lowest the plan says would do. One that only just scrapes over
    // a corner in the plan would otherwise clip it in practice, by a hair, and land short.
    private const float JumpClearance = 0.12f;

    // Leaning the search toward the goal. Above 1 it looks at far fewer spots and the route can be a little longer
    // than the very best one.
    private const float HeuristicWeight = 1.5f;

    // When looking for something to jump onto from open ground, how far ahead and up to look.
    private const float JumpLookaheadDistance = 8f;
    private const float JumpLookaheadHeight = 12f;

    // A move like one that failed before (same kind, same direction, starting and ending nearby) costs this many
    // seconds more for each time it has failed, so the search goes another way if there is one.
    private const float FailedMovePenalty = 20f;
    private const float FailedMoveStartRadius = 1f;
    private const float FailedMoveEndRadius = 1.5f;

    // How far a jump or fall is followed when working out what it would take to get past a blockage.
    private const float DiagnosticLimit = 60f;

    private readonly Vector2 castSize;
    private readonly LayerMask obstacles;
    private readonly float speed;
    private readonly float gravity;
    private readonly float cellSize;
    private readonly float maxStep;
    private readonly float maxJumpHeight;
    private readonly List<float> jumpHeights = new List<float>();
    private readonly float maxFall;
    private readonly bool jumpAnywhere;
    private readonly int maxNodes;
    private readonly float stepTime;
    private readonly List<FailedMove> failedMoves;

    // What the last search found out, for explaining a failure.
    public int NodesSearched { get; private set; }
    public bool HitNodeLimit { get; private set; }
    public Vector2 ClosestPosition { get; private set; }

    // How long the last route found takes, in seconds. Lower means closer to the goal.
    public float RouteCost { get; private set; }

    // The finished search: whether it got there, and the route if so.
    public bool ReachedGoal { get; private set; }
    public List<GroundStep> Steps { get; private set; } = new List<GroundStep>();

    // Time spent searching in total, and over how many frames.
    public float MillisecondsSpent { get; private set; }
    public int FramesTaken { get; private set; }

    private struct Node
    {
        public Vector2 position;
        public int parent;
        public GroundMoveType type;
        public float velocityX;
        public float velocityY;
        public float cost;

        // For a jump: where it takes off from, if that isn't the parent node (a wall, or the edge of a ledge).
        public bool hasTakeoff;
        public Vector2 takeoff;
    }

    // The search in progress.
    private readonly List<Node> nodes = new List<Node>();
    private readonly Dictionary<Vector2Int, float> cheapest = new Dictionary<Vector2Int, float>();
    private readonly HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
    private readonly SortedSet<(float score, int id)> open = new SortedSet<(float score, int id)>();
    private Vector2 goal;
    private int expanded;
    private int goalNode;
    private float closestDistance;

    // gravity is the downward acceleration, so it is negative.
    public GroundPathFinder2D(
        Vector2 size, LayerMask obstacles, float speed, float gravity, float cellSize,
        float maxJumpHeight, float maxFall, bool jumpAnywhere, int maxNodes, List<FailedMove> failedMoves = null)
    {
        this.failedMoves = failedMoves ?? new List<FailedMove>();
        castSize = new Vector2(Mathf.Max(size.x - Skin, 0.05f), Mathf.Max(size.y - Skin, 0.05f));
        this.obstacles = obstacles;
        this.speed = Mathf.Max(speed, 0.01f);
        this.gravity = gravity;
        this.cellSize = Mathf.Max(cellSize, 0.1f);
        this.maxJumpHeight = Mathf.Max(maxJumpHeight, 0f);
        this.maxFall = Mathf.Max(maxFall, 0f);
        this.jumpAnywhere = jumpAnywhere;
        this.maxNodes = Mathf.Max(maxNodes, 1);

        // The biggest height difference between neighbouring spots it will simply walk over: a 45 degree slope.
        maxStep = this.cellSize * 1.05f;
        stepTime = Time.fixedDeltaTime * StrideSteps;

        // Heights to try, rising by a fixed ratio, always ending on the maximum itself.
        if (this.maxJumpHeight > 0f)
        {
            for (var height = SmallestJump; height < this.maxJumpHeight; height *= JumpHeightGrowth)
            {
                jumpHeights.Add(height);
            }

            jumpHeights.Add(this.maxJumpHeight);
        }
    }

    // Finds the spot on the ground at or below the hint where the enemy would stand. Lifts the hint out of
    // anything it's buried in first.
    public bool TryFindStand(Vector2 hint, out Vector2 stand)
    {
        for (var lift = 0f; lift <= 2f; lift += cellSize)
        {
            if (TryStand(hint + Vector2.up * lift, 30f + lift, out stand))
            {
                return true;
            }
        }

        stand = hint;
        return false;
    }

    // Starts a search from start to goal (both are box centres).
    public void BeginSearch(Vector2 start, Vector2 goal)
    {
        this.goal = goal;

        nodes.Clear();
        cheapest.Clear();
        closed.Clear();
        open.Clear();

        expanded = 0;
        goalNode = -1;
        closestDistance = float.MaxValue;
        ClosestPosition = start;
        ReachedGoal = false;
        Steps = new List<GroundStep>();
        MillisecondsSpent = 0f;
        FramesTaken = 0;

        Push(start, -1, GroundMoveType.Walk, 0f, 0f, 0f);
    }

    // Searches for a little while, within what this frame's budget allows. Returns true once the search is over,
    // whether or not it found a route (see ReachedGoal).
    public bool Advance()
    {
        var allowed = GroundPlanningBudget.Available();
        var clock = Stopwatch.StartNew();
        var finished = false;

        FramesTaken++;

        while (true)
        {
            if (open.Count == 0 || expanded >= maxNodes)
            {
                finished = true;
                break;
            }

            var entry = open.Min;
            open.Remove(entry);

            var node = nodes[entry.id];

            if (!closed.Add(KeyOf(node.position)))
            {
                continue;
            }

            var distanceToGoal = Vector2.Distance(node.position, goal);

            if (distanceToGoal < closestDistance)
            {
                closestDistance = distanceToGoal;
                ClosestPosition = node.position;
            }

            if (IsGoal(node.position))
            {
                goalNode = entry.id;
                finished = true;
                break;
            }

            expanded++;
            Expand(entry.id, node);

            if (clock.Elapsed.TotalMilliseconds >= allowed)
            {
                break;
            }
        }

        var spent = (float)clock.Elapsed.TotalMilliseconds;

        GroundPlanningBudget.Spend(spent);
        MillisecondsSpent += spent;

        if (finished)
        {
            Finish();
        }

        return finished;
    }

    private void Finish()
    {
        ReachedGoal = goalNode >= 0;
        NodesSearched = expanded;
        HitNodeLimit = !ReachedGoal && open.Count > 0;
        RouteCost = ReachedGoal ? nodes[goalNode].cost : float.MaxValue;

        var steps = new List<GroundStep>();

        if (ReachedGoal)
        {
            // Built from the goal back to the start, then turned round.
            for (var i = goalNode; nodes[i].parent >= 0; i = nodes[i].parent)
            {
                var node = nodes[i];
                var origin = nodes[node.parent].position;
                var takeoff = node.hasTakeoff ? node.takeoff : origin;

                steps.Add(new GroundStep
                {
                    type = node.type,
                    from = takeoff,
                    to = node.position,
                    velocityX = node.velocityX,
                    velocityY = node.velocityY
                });

                // A jump from further along than the spot reached by walking: the walk there comes first.
                if (Mathf.Abs(takeoff.x - origin.x) > 0.01f)
                {
                    steps.Add(new GroundStep
                    {
                        type = GroundMoveType.Walk,
                        from = origin,
                        to = takeoff,
                        velocityX = Mathf.Sign(takeoff.x - origin.x) * speed,
                        velocityY = 0f
                    });
                }
            }

            steps.Reverse();
            steps = MergeWalks(steps);
        }

        Steps = steps;
    }

    private Vector2Int KeyOf(Vector2 point)
    {
        return new Vector2Int(Mathf.RoundToInt(point.x / cellSize), Mathf.RoundToInt(point.y / 0.2f));
    }

    private bool IsGoal(Vector2 point)
    {
        return Mathf.Abs(point.x - goal.x) <= cellSize * 0.75f && Mathf.Abs(point.y - goal.y) <= maxStep + 0.15f;
    }

    private void Push(
        Vector2 position, int parent, GroundMoveType type, float velocityX, float velocityY, float cost,
        bool hasTakeoff = false, Vector2 takeoff = default)
    {
        var key = KeyOf(position);

        if (closed.Contains(key) || (cheapest.TryGetValue(key, out var existing) && cost >= existing))
        {
            return;
        }

        cheapest[key] = cost;
        nodes.Add(new Node
        {
            position = position, parent = parent, type = type,
            velocityX = velocityX, velocityY = velocityY, cost = cost,
            hasTakeoff = hasTakeoff, takeoff = takeoff
        });

        // The time to cross the horizontal distance left is the least it could possibly take; leaning on it a bit
        // harder than that makes the search head for the goal instead of fanning out.
        open.Add((cost + HeuristicWeight * Mathf.Abs(position.x - goal.x) / speed, nodes.Count - 1));
    }

    // Queues everywhere the enemy can get to from a spot in one move.
    private void Expand(int id, Node node)
    {
        for (var side = 0; side < 2; side++)
        {
            var direction = side == 0 ? -1f : 1f;
            var next = node.position + new Vector2(direction * cellSize, 0f);
            var blocked = false;
            var dropOff = false;

            if (TryStand(next + Vector2.up * maxStep, maxStep * 2f + Skin, out var stand)
                && Mathf.Abs(stand.y - node.position.y) <= maxStep + 0.03f
                && SweepClear(node.position, stand))
            {
                Push(stand, id, GroundMoveType.Walk, direction * speed, 0f,
                    node.cost + Vector2.Distance(node.position, stand) / speed);
            }
            else if (!Physics2D.OverlapBox(next, castSize, 0f, obstacles)
                && !Physics2D.BoxCast(next, castSize, 0f, Vector2.down, maxStep + Skin, obstacles))
            {
                // Nothing to stand on ahead: walk off the edge and fall.
                dropOff = true;

                // The ground runs out somewhere between this spot and the next; the fall starts exactly there.
                var edge = FindLedgeEdge(node.position, direction);

                if (Simulate(edge, direction * speed, 0f, out var landing, out var flightTime))
                {
                    Push(landing, id, GroundMoveType.Fall, direction * speed, 0f,
                        node.cost + Vector2.Distance(node.position, edge) / speed + flightTime
                        + FailurePenalty(GroundMoveType.Fall, node.position, landing));
                }
            }
            else
            {
                blocked = true;
            }

            // At a ledge a jump has to get across the gap, which can be to ground at the same height or higher (lower
            // is a fall, handled above). At a wall, or anywhere else if allowed, it has to get up onto something:
            // landing at the same height just reaches somewhere walking would, so it's not worth a node.
            // Jumps take off from the very edge of the ledge or the very foot of the wall rather than from this spot,
            // which could be most of a cell short of it. Otherwise whether a jump works would depend on where the
            // search's grid happened to fall.
            if (dropOff)
            {
                var edge = FindLedgeEdge(node.position, direction);

                AddJump(id, node, edge - new Vector2(direction * Skin * 0.75f, 0f), direction * speed, -maxStep);
            }
            else if (blocked)
            {
                AddJump(id, node, FindWallContact(node.position, direction), direction * speed, maxStep);
            }
            else if (jumpAnywhere && SomethingToJumpOnto(node.position, direction))
            {
                AddJump(id, node, node.position, direction * speed, maxStep);
            }
        }
    }

    // Tries jumps of rising height and queues the first that lands somewhere useful, narrowed down to the lowest
    // height that still gets there, so the enemy only ever jumps as high as it has to, however high it could jump.
    // minRise is how far above the takeoff spot (negative: below) a landing has to be to count.
    private void AddJump(int parentId, Node from, Vector2 takeoff, float velocityX, float minRise)
    {
        var startKey = KeyOf(takeoff);
        var previousHeight = 0f;

        // Getting to the takeoff spot from the node takes a little walking.
        var walkCost = Vector2.Distance(from.position, takeoff) / speed;

        bool Lands(float height, out Vector2 landing, out float flightTime)
        {
            return Simulate(takeoff, velocityX, VelocityForHeight(height), out landing, out flightTime)
                && landing.y - takeoff.y >= minRise
                && KeyOf(landing) != startKey;
        }

        foreach (var height in jumpHeights)
        {
            if (Lands(height, out var landing, out var flightTime))
            {
                var low = previousHeight;
                var high = height;

                for (var i = 0; i < JumpRefinements; i++)
                {
                    var middle = (low + high) * 0.5f;

                    if (Lands(middle, out var refined, out var refinedTime))
                    {
                        high = middle;
                        landing = refined;
                        flightTime = refinedTime;
                    }
                    else
                    {
                        low = middle;
                    }
                }

                // Carried out a touch higher than planned, for clearance.
                Push(landing, parentId, GroundMoveType.Jump, velocityX, VelocityForHeight(high + JumpClearance),
                    from.cost + walkCost + flightTime + JumpPenalty + high * JumpHeightPenalty
                    + FailurePenalty(GroundMoveType.Jump, takeoff, landing),
                    true, takeoff);
                return;
            }

            previousHeight = height;
        }
    }

    // Where the box would be standing right up against a wall in front of it, found by walking it forward from a
    // spot that has room to move. If nothing is in the way within a cell, it stays where it is.
    private Vector2 FindWallContact(Vector2 from, float direction)
    {
        var hit = Physics2D.BoxCast(from, castSize, 0f, new Vector2(direction, 0f), cellSize, obstacles);

        if (!hit.collider)
        {
            return from;
        }

        // The same hair's breadth short of the wall that the jump simulation keeps.
        return new Vector2(hit.centroid.x - direction * Skin * 0.25f, from.y);
    }

    // A cheap look at whether there's anything above the floor ahead to jump onto, so open ground doesn't cost a pile
    // of jump simulations that could only end back on the ground.
    private bool SomethingToJumpOnto(Vector2 position, float direction)
    {
        var height = Mathf.Min(maxJumpHeight, JumpLookaheadHeight);
        var floorTop = position.y - castSize.y * 0.5f;
        var center = new Vector2(position.x + direction * JumpLookaheadDistance * 0.5f, floorTop + 0.3f + height * 0.5f);

        return Physics2D.OverlapBox(center, new Vector2(JumpLookaheadDistance, height), 0f, obstacles);
    }

    // Extra cost for a move that resembles ones that have failed.
    private float FailurePenalty(GroundMoveType type, Vector2 from, Vector2 to)
    {
        var penalty = 0f;

        foreach (var failed in failedMoves)
        {
            if (failed.type == type
                && Mathf.Sign(failed.to.x - failed.from.x) == Mathf.Sign(to.x - from.x)
                && Vector2.Distance(failed.from, from) <= FailedMoveStartRadius
                && Vector2.Distance(failed.to, to) <= FailedMoveEndRadius)
            {
                penalty += FailedMovePenalty * failed.count;
            }
        }

        return penalty;
    }

    private float VelocityForHeight(float height)
    {
        return Mathf.Sqrt(2f * -gravity * height);
    }

    // For explaining a failed search: from a spot the enemy can stand, works out what it would take to get past
    // whatever stops it going `direction` (-1 or 1). jumpHeight is the least Max Jump Height that gets it up onto a
    // ledge there; fallDistance is how far it would drop going off the edge there. Each is -1 if that doesn't apply.
    public void DiagnoseBlockage(
        Vector2 from, float direction,
        out float jumpHeight, out Vector2 jumpLanding, out float fallDistance, out Vector2 fallLanding)
    {
        jumpHeight = -1f;
        jumpLanding = from;
        fallDistance = -1f;
        fallLanding = from;

        // Quarter-unit steps up to a height that no ledge will ever need.
        for (var height = 0.25f; height <= 12f; height += 0.25f)
        {
            var velocityY = VelocityForHeight(height);

            if (Simulate(from, direction * speed, velocityY, DiagnosticLimit, out var landing, out _)
                && landing.y > from.y + 0.25f)
            {
                jumpHeight = height;
                jumpLanding = landing;
                break;
            }
        }

        var next = from + new Vector2(direction * cellSize, 0f);

        if (!Physics2D.OverlapBox(next, castSize, 0f, obstacles)
            && !Physics2D.BoxCast(next, castSize, 0f, Vector2.down, maxStep + Skin, obstacles))
        {
            var edge = FindLedgeEdge(from, direction);

            if (Simulate(edge, direction * speed, 0f, DiagnosticLimit, out var landing, out _))
            {
                fallDistance = from.y - landing.y;
                fallLanding = landing;
            }
        }
    }

    // Between a spot with ground underfoot and the next one without, finds where the box really stops being held up.
    // The enemy begins falling there, which can be up to a whole cell earlier than the spot with nothing underneath.
    private Vector2 FindLedgeEdge(Vector2 supported, float direction)
    {
        var near = 0f;
        var far = cellSize;

        for (var i = 0; i < 6; i++)
        {
            var middle = (near + far) * 0.5f;
            var probe = supported + new Vector2(direction * middle, 0f);

            if (Physics2D.BoxCast(probe, castSize, 0f, Vector2.down, Skin * 2.5f, obstacles))
            {
                near = middle;
            }
            else
            {
                far = middle;
            }
        }

        return supported + new Vector2(direction * far, 0f);
    }

    // A run of walking in one direction is a single step.
    private static List<GroundStep> MergeWalks(List<GroundStep> steps)
    {
        var merged = new List<GroundStep>(steps.Count);

        foreach (var step in steps)
        {
            if (step.type == GroundMoveType.Walk && merged.Count > 0)
            {
                var last = merged[merged.Count - 1];

                if (last.type == GroundMoveType.Walk && Mathf.Sign(last.velocityX) == Mathf.Sign(step.velocityX))
                {
                    last.to = step.to;
                    merged[merged.Count - 1] = last;
                    continue;
                }
            }

            merged.Add(step);
        }

        return merged;
    }

    // Where the box would rest if dropped from `from`, which must be free.
    private bool TryStand(Vector2 from, float maxDrop, out Vector2 stand)
    {
        stand = from;

        if (Physics2D.OverlapBox(from, castSize, 0f, obstacles))
        {
            return false;
        }

        var hit = Physics2D.BoxCast(from, castSize, 0f, Vector2.down, maxDrop, obstacles);

        if (!hit.collider || hit.normal.y < MinGroundNormal)
        {
            return false;
        }

        stand = hit.centroid + Vector2.up * Lift;
        return true;
    }

    // A walking step has to follow the ground, not cut through a step or a corner. The box is sliced thinner and
    // raised for this so that rounding the top of a ramp doesn't count.
    private bool SweepClear(Vector2 from, Vector2 to)
    {
        var offset = to - from;
        var distance = offset.magnitude;

        if (distance < 0.0001f)
        {
            return true;
        }

        var size = new Vector2(castSize.x, Mathf.Max(castSize.y - 0.4f, 0.05f));
        var raise = (castSize.y - size.y) * 0.5f;

        return !Physics2D.BoxCast(from + Vector2.up * raise, size, 0f, offset / distance, distance, obstacles);
    }

    // Plays out leaving the ground at `start` with the given velocity, stepping through physics the way Unity does.
    // Sideways velocity is held the whole way, as the enemy keeps pushing: pressed against a wall it goes nowhere,
    // and once it's high enough to clear the top it carries on over. Returns true if it lands on something.
    private bool Simulate(Vector2 start, float velocityX, float velocityY, out Vector2 landing, out float time)
    {
        return Simulate(start, velocityX, velocityY, maxFall, out landing, out time);
    }

    private bool Simulate(
        Vector2 start, float velocityX, float velocityY, float fallLimit, out Vector2 landing, out float time)
    {
        var dt = Time.fixedDeltaTime;
        var position = start;

        landing = start;
        time = 0f;

        for (var stride = 0; stride < MaxFlightStrides; stride++)
        {
            var move = Vector2.zero;

            for (var i = 0; i < StrideSteps; i++)
            {
                velocityY += gravity * dt;
                move += new Vector2(velocityX, velocityY) * dt;
            }

            time += stepTime;

            if (Mathf.Abs(move.x) > 0.0001f)
            {
                var hit = Physics2D.BoxCast(
                    position, castSize, 0f, new Vector2(Mathf.Sign(move.x), 0f), Mathf.Abs(move.x), obstacles);

                // Stops a hair short of a wall. Right up against it, the sideways contact gets mistaken for a floor
                // or ceiling by the vertical move below, which ends the jump on the spot.
                position.x = hit.collider
                    ? hit.centroid.x - Mathf.Sign(move.x) * Skin * 0.25f
                    : position.x + move.x;
            }

            if (Mathf.Abs(move.y) > 0.0001f)
            {
                var falling = move.y < 0f;
                var hit = Physics2D.BoxCast(
                    position, castSize, 0f, falling ? Vector2.down : Vector2.up, Mathf.Abs(move.y), obstacles);

                // Scraping along the side of a wall isn't a floor or a ceiling, so it doesn't stop the vertical move.
                if (hit.collider && Mathf.Abs(hit.normal.y) >= 0.5f)
                {
                    position.y = hit.centroid.y;

                    if (falling && hit.normal.y >= MinGroundNormal)
                    {
                        landing = position + Vector2.up * Lift;
                        return true;
                    }

                    velocityY = 0f;
                }
                else
                {
                    position.y += move.y;
                }
            }

            if (start.y - position.y > fallLimit)
            {
                return false;
            }
        }

        return false;
    }
}
