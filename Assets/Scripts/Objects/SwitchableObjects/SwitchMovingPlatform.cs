using System.Collections.Generic;
using UnityEngine;

// A platform that follows a path of points when it is switched on. A lever, button or anything else that works switchable objects
// runs it.
//
// How it's set up:
//  - Put the platform where the path starts, with a Rigidbody2D set to Kinematic, so whatever stands on it is carried. (Without
//    one it moves by its transform, and carries nothing.)
//  - Path Points are empty objects, in the order it visits them. It goes from where it starts to the first one, and from then on the
//    points are all there is to the path: where it started isn't one of them. They shouldn't be children of the platform, or they
//    would move with it. Where they are when the game starts is the path.
//  - Speed is how fast it goes. At the last point it stops, turns around and goes back and forth between the first point and the last,
//    or goes round the points in a loop (Path End). Only switching it off takes it back to where it started.
//  - Corner Radius rounds the corners. At 0 it goes right to each point before turning; the higher it is, the rounder the turn, up to
//    what the path has room for. Where a corner is rounded, it's the middle of the curve that counts as being at the point.
//  - Movement Method is how its speed changes along the way: steady, easing in and out at the ends of the path, or easing at every
//    point. Pause At Points makes it wait at each one.
//
// What switching does:
//  - On a lever or a button that is pressed all the way, switching on sets it off along the path, and switching off either stops it
//    where it is, or takes it back to where it started (When Switched Off).
//  - A button set to Allow Partial Presses moves it by how far down the button is pressed: half way down takes it half way along the path,
//    to the last point (as far as the path goes, not in a straight line). It follows the button back up as well. This always goes to
//    the end of the path and back, whatever Path End and When Switched Off say.
//  - Stay Switched On leaves it running, or where a button has taken it. Switch Off Time switches it off that many seconds after
//    the last time it was told to run.
public class SwitchMovingPlatform : SwitchableObject
{
    public enum PathEndAction
    {
        // Stops at the last point, and stays there until it's taken back.
        Stop,
        // Goes back along the path to the first point, and turns around again there, for as long as it's switched on. It doesn't go
        // back to where it started.
        TurnAround,
        // Goes on from the last point to the first, round and round, for as long as it's switched on. It doesn't go back to where it
        // started.
        Loop
    }

    public enum SwitchOffAction
    {
        // Stops where it is, and carries on from there when it's switched on again.
        Stop,
        // Goes back to where it started, by the first point and then straight to the start, however far round it was.
        ReturnToStart
    }

    public enum Method
    {
        // The same speed all the way, and all at once at the start and the end.
        Constant,
        // Speeds up from rest at the start and slows to rest at the end of the path. In a loop, which has no end, it's steady.
        SmoothEnds,
        // Slows to a near stop at every point and speeds up again after it.
        EaseBetweenPoints
    }

    [Header("What moves")]
    [Tooltip("The platform. If it's empty, this object moves.")]
    public Transform platform;

    [Tooltip("Where it goes, in order. It goes from where it starts to the first, and after that only between these: where it started isn't one of them. Not children of the platform.")]
    public Transform[] pathPoints = new Transform[0];

    [Header("How it moves")]
    [Min(0.01f)]
    [Tooltip("How fast it goes, in units per second.")]
    public float speed = 3f;

    [Tooltip("What it does when it gets to the last point: stop there, go back and forth between the first point and the last, or go round the points in a loop.")]
    public PathEndAction pathEnd = PathEndAction.Stop;

    [Tooltip("What it does when the switch is turned off: stop where it is, or go back to where it started.")]
    public SwitchOffAction whenSwitchedOff = SwitchOffAction.ReturnToStart;

    [Min(0f)]
    [Tooltip("How round the corners are. 0 goes right to every point before turning. A larger number is a rounder turn, as far as the path has room for.")]
    public float cornerRadius;

    [Tooltip("How its speed changes along the way.")]
    public Method movementMethod = Method.Constant;

    [Min(0.05f)]
    [Tooltip("How long it takes to get up to speed from rest, and to slow to rest, in seconds, for the methods that ease.")]
    public float easeSeconds = 0.6f;

    [Min(0f)]
    [Tooltip("How long it waits at each point on its way, in seconds. 0 doesn't stop it.")]
    public float pauseAtPoints;

    private enum Intent
    {
        // Holding still, or slowing to it.
        Stopped,
        // Going along the path as Path End says.
        Running,
        // Going back to the start.
        Returning,
        // Going to a place along the path that a button has asked for.
        Seeking
    }

    private Rigidbody2D body;
    // The way from where it starts, through the points, to the last. And, to go round in a loop, the points alone, joined end to start: it
    // gets on to this from the open one at the first point.
    private Route open;
    private Route loop;
    private Route route;

    // How far along the open route the first point is, so how long the way from the start to it is.
    private float leadIn;

    private Intent intent = Intent.Stopped;
    private float distance;
    private float velocity;
    private float direction = 1f;
    private float seekGoal;
    private float seekAmount;
    private bool amountMode;
    private float pausedUntil = float.NegativeInfinity;
    private float lastActivated = float.NegativeInfinity;
    private Vector2 startPosition;

    // How close to a point counts as being at it, in units.
    private const float AtPoint = 0.01f;

    private static readonly float[] NoStops = new float[0];

    // Whether it goes round the points in a loop. A loop needs two points at least: with only one, there's nowhere to go round, so it
    // goes there and stays, as Stop does.
    private bool Cycles
    {
        get { return pathEnd == PathEndAction.Loop && loop != null && loop.points.Length >= 2; }
    }

    protected override void Start()
    {
        base.Start();

        if (!platform)
        {
            platform = transform;
        }

        body = KinematicBody();
        startPosition = body.position;

        var vertices = Vertices(startPosition, true);

        if (vertices.Count < 2)
        {
            Debug.LogWarning($"{name}: the moving platform has no Path Points, so it won't move.", this);
        }

        open = Route.Build(vertices, false, cornerRadius);
        loop = Route.Build(Vertices(startPosition, false), true, cornerRadius);
        leadIn = open.leadIn;

        // It always begins at the start, from where it goes to the first point.
        route = open;
        distance = 0f;

        // Moved a step at a time by the physics, so it's drawn smoothly between them.
        if (body)
        {
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    // The platform's body. It has to be kinematic, or what stands on it isn't carried: a platform moved by its transform has no
    // movement for a character to take, and one that isn't kinematic falls or is pushed about. So one is added, or made so, if it
    // isn't already, and said so.
    private Rigidbody2D KinematicBody()
    {
        var found = platform.GetComponent<Rigidbody2D>();

        if (!found)
        {
            found = platform.gameObject.AddComponent<Rigidbody2D>();
            Debug.LogWarning($"{name}: {platform.name} had no Rigidbody2D, so a Kinematic one has been added, which is what carries " +
                             "whatever stands on it. Add one yourself, set to Kinematic, to stop this message.", this);
        }

        if (found.bodyType != RigidbodyType2D.Kinematic)
        {
            found.bodyType = RigidbodyType2D.Kinematic;
            Debug.LogWarning($"{name}: {platform.name}'s Rigidbody2D wasn't Kinematic, so it has been made Kinematic. " +
                             "Set it to Kinematic yourself to stop this message.", this);
        }

        return found;
    }

    // Each of the points as they are now, and before them, if it's asked for, the start.
    private List<Vector2> Vertices(Vector2 start, bool withStart)
    {
        var vertices = new List<Vector2>();

        if (withStart)
        {
            vertices.Add(start);
        }

        foreach (var point in pathPoints)
        {
            if (point)
            {
                vertices.Add(point.position);
            }
        }

        return vertices;
    }

    // Not the base class's Update, which switches the object off again by itself every frame unless something keeps switching it on.
    protected override void Update()
    {
        // In someone else's game the host decides, and where the platform is comes from the network.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        if (!staySwitchedOn && switchOffTime > 0f && intent == Intent.Running && Time.time - lastActivated >= switchOffTime)
        {
            SwitchOff();
        }
    }

    void FixedUpdate()
    {
        if (GameSession.IsClientOnly || route == null || route.points.Length < 2)
        {
            return;
        }

        Step(Time.fixedDeltaTime);

        var position = route.PointAt(distance);

        if (body)
        {
            body.MovePosition(position);
        }
        else
        {
            platform.position = new Vector3(position.x, position.y, platform.position.z);
        }
    }

    // ----- Being switched -----

    public override void Activate()
    {
        base.Activate();

        lastActivated = Time.time;

        if (intent == Intent.Running && !amountMode)
        {
            return;
        }

        // From a button's amount, or taking it back, to going along the path.
        if (intent == Intent.Returning || intent == Intent.Seeking)
        {
            direction = 1f;
        }

        amountMode = false;
        intent = Intent.Running;

        // To go round, it's on the loop, once it has got to the first point. Before that, on the way to it, it stays on the open route,
        // which the loop doesn't include.
        if (Cycles && !(route == open && distance < leadIn - AtPoint))
        {
            UseRoute(loop);
        }
        else if (!Cycles)
        {
            UseRoute(open);
        }
    }

    public override bool AcceptsAmount
    {
        get { return true; }
    }

    // Takes it that fraction of the way along the path: a button that is half pressed takes it half way. When it's let go it goes back
    // to the start, unless it stays switched on, which keeps it as far as it's been taken.
    public override void SetAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (amount <= 0f)
        {
            Deactivate();
            return;
        }

        base.Activate();

        lastActivated = Time.time;
        amountMode = true;
        seekAmount = staySwitchedOn ? Mathf.Max(seekAmount, amount) : amount;
        UseRoute(open);
        seekGoal = seekAmount * open.total;
        intent = Intent.Seeking;
    }

    public override void Deactivate()
    {
        base.Deactivate();

        if (!staySwitchedOn)
        {
            SwitchOff();
        }
    }

    private void SwitchOff()
    {
        if (route == null)
        {
            return;
        }

        // A button that was taking it along follows the button back up, all the way to the start.
        if (amountMode)
        {
            seekAmount = 0f;
            seekGoal = 0f;
            intent = Intent.Seeking;
            return;
        }

        intent = whenSwitchedOff == SwitchOffAction.ReturnToStart ? Intent.Returning : Intent.Stopped;
    }

    // Changes which path it's on, keeping it where it is: the loop and the open path are cut differently at the corners.
    private void UseRoute(Route next)
    {
        if (route == next || next == null)
        {
            return;
        }

        var here = route.PointAt(distance);
        route = next;
        distance = route.Project(here);
    }

    // ----- Moving -----

    // One physics step: where it's heading, how fast, and what happens when it gets there.
    private void Step(float dt)
    {
        // Waiting at a point.
        if (Time.time < pausedUntil)
        {
            velocity = 0f;
            return;
        }

        var total = route.total;
        var closed = route.closed;
        var hasGoal = false;
        var goal = 0f;
        float sign;

        switch (intent)
        {
            case Intent.Running:
                // Going along an open path and stopping at the end: always forward, and the end is where it's heading for.
                if ((pathEnd == PathEndAction.Stop || (pathEnd == PathEndAction.Loop && !Cycles)) && !closed)
                {
                    direction = 1f;
                    hasGoal = true;
                    goal = total;
                }

                sign = direction;
                break;

            case Intent.Returning:
                hasGoal = true;

                // Round a loop it makes for the first point, the nearer way, which is where the loop meets the way to the start. From
                // there it goes back to the start.
                goal = closed ? (distance <= total * 0.5f ? 0f : total) : 0f;
                sign = goal < distance ? -1f : 1f;
                break;

            case Intent.Seeking:
                hasGoal = true;
                goal = seekGoal;
                sign = goal < distance ? -1f : 1f;
                break;

            default:
                sign = velocity != 0f ? Mathf.Sign(velocity) : direction;
                break;
        }

        // How fast it wants to go.
        var wanted = intent == Intent.Stopped ? 0f : speed;

        if (movementMethod != Method.Constant && wanted > 0f)
        {
            var ahead = DistanceToNextStop(sign, hasGoal, goal);
            var acceleration = speed / easeSeconds;

            wanted = Mathf.Min(wanted, Mathf.Sqrt(2f * acceleration * ahead));
        }

        if (movementMethod == Method.Constant)
        {
            velocity = sign * wanted;
        }
        else
        {
            velocity = Mathf.MoveTowards(velocity, sign * wanted, speed / easeSeconds * dt);
        }

        var next = distance + velocity * dt;

        // Reaching the place it was sent to.
        if (hasGoal && (goal - distance) * (goal - next) <= 0f && (velocity != 0f || Mathf.Abs(goal - distance) <= AtPoint))
        {
            distance = goal;
            velocity = 0f;
            Arrived();
            return;
        }

        // On the way from the start to the first point of a loop, and there: from here on it goes round the points, and the start is left
        // behind. With a pause to make, it stops at the first point as it does at every other.
        if (intent == Intent.Running && Cycles && route == open && next >= leadIn)
        {
            route = loop;

            if (pauseAtPoints > 0f)
            {
                distance = 0f;
                velocity = 0f;
                Wait();
            }
            else
            {
                distance = Mathf.Repeat(next - leadIn, loop.total);
            }

            return;
        }

        if (!closed)
        {
            // Back and forth between the first point and the last. The way from the start to the first point is only gone along once.
            if (intent == Intent.Running && pathEnd == PathEndAction.TurnAround && sign < 0f && distance >= leadIn - AtPoint && next <= leadIn)
            {
                distance = leadIn;
                velocity = 0f;
                direction = 1f;
                Wait();
                return;
            }

            // The ends of the path, going back and forth.
            if (next >= total)
            {
                distance = total;
                velocity = 0f;
                direction = -1f;
                Wait();
                return;
            }

            if (next <= 0f)
            {
                distance = 0f;
                velocity = 0f;
                direction = 1f;

                if (intent == Intent.Running)
                {
                    Wait();
                }

                return;
            }
        }

        // Passing a point it has to wait at.
        if (pauseAtPoints > 0f && intent == Intent.Running)
        {
            var stop = StopBetween(distance, next);

            if (stop.HasValue)
            {
                distance = Wrap(stop.Value);
                velocity = 0f;
                Wait();
                return;
            }
        }

        // Round a loop it carries on past the start; anywhere else the ends are the ends.
        distance = closed && (intent == Intent.Running || intent == Intent.Stopped) ? Mathf.Repeat(next, total) : Mathf.Clamp(next, 0f, total);
    }

    // At the place it was sent to.
    private void Arrived()
    {
        switch (intent)
        {
            case Intent.Running:
                // Along the path and stopping at its end.
                intent = Intent.Stopped;
                break;

            case Intent.Returning:
                // Round a loop, at the first point: on to the way back to the start, which isn't part of the loop. It goes on from there
                // at the same place, and arrives when it gets to the start.
                if (route == loop)
                {
                    route = open;
                    distance = leadIn;
                    break;
                }

                intent = Intent.Stopped;
                direction = 1f;
                break;
        }
    }

    // It has reached a point, and waits there if it's meant to.
    private void Wait()
    {
        if (pauseAtPoints > 0f && intent == Intent.Running)
        {
            pausedUntil = Time.time + pauseAtPoints;
        }
    }

    private float Wrap(float value)
    {
        return route.closed ? Mathf.Repeat(value, route.total) : Mathf.Clamp(value, 0f, route.total);
    }

    // The nearest point that it should slow to a stop at, in the way it's going, and how far off it is. Where that is depends on
    // the method: the ends of the path, or every point, and wherever it's been sent to.
    private float DistanceToNextStop(float sign, bool hasGoal, float goal)
    {
        var best = float.PositiveInfinity;
        var stops = movementMethod == Method.EaseBetweenPoints ? route.stops : route.ends;

        // On its way to a loop it doesn't slow at the ends of the way, as it doesn't stop at either.
        if (intent == Intent.Running && Cycles && route == open && movementMethod != Method.EaseBetweenPoints)
        {
            stops = NoStops;
        }

        // Going back and forth, it turns around at the first point, not at the start, so that's where it slows.
        if (intent == Intent.Running && pathEnd == PathEndAction.TurnAround && !route.closed && sign < 0f && movementMethod != Method.Constant)
        {
            var toFirst = (leadIn - distance) * sign;

            if (toFirst > AtPoint)
            {
                best = Mathf.Min(best, toFirst);
            }
        }

        foreach (var stop in stops)
        {
            float ahead;

            if (route.closed)
            {
                ahead = sign > 0f ? Mathf.Repeat(stop - distance, route.total) : Mathf.Repeat(distance - stop, route.total);

                if (ahead <= AtPoint)
                {
                    ahead += route.total;
                }
            }
            else
            {
                ahead = (stop - distance) * sign;

                if (ahead <= AtPoint)
                {
                    continue;
                }
            }

            best = Mathf.Min(best, ahead);
        }

        if (hasGoal)
        {
            best = Mathf.Min(best, Mathf.Max(0f, (goal - distance) * sign));
        }

        return best;
    }

    // A point it waits at, between where it was and where it would be, if it passes one.
    private float? StopBetween(float from, float to)
    {
        float? found = null;
        var total = route.total;

        foreach (var stop in route.stops)
        {
            if (!route.closed)
            {
                if ((stop - from) * (stop - to) <= 0f && Mathf.Abs(stop - from) > AtPoint)
                {
                    if (!found.HasValue || Mathf.Abs(stop - from) < Mathf.Abs(found.Value - from))
                    {
                        found = stop;
                    }
                }

                continue;
            }

            // On a loop, the point comes round again each lap.
            var offset = Mathf.Repeat(stop - from, total);

            if (offset > AtPoint && offset <= to - from + 0.0001f)
            {
                var place = from + offset;

                if (!found.HasValue || place < found.Value)
                {
                    found = place;
                }
            }
        }

        return found;
    }

    // ----- The path -----

    // A path made of straight pieces and, where a corner is rounded, arcs, measured along its length. It's made once, when the game
    // starts.
    private sealed class Route
    {
        public Vector2[] points;
        public float[] along;
        public float total;

        // How far along each of the points of the path is: the middle of the turn, where a corner is rounded.
        public float[] stops;

        // Just where it starts and where it ends.
        public float[] ends;

        public bool closed;

        // For a route made from the start and then the points: how far along the first point is (the middle of its turn, if it's rounded),
        // which is how long the way from the start to it is.
        public float leadIn;

        public Vector2 PointAt(float at)
        {
            if (points.Length == 1 || total <= 0f)
            {
                return points[0];
            }

            at = closed ? Mathf.Repeat(at, total) : Mathf.Clamp(at, 0f, total);

            var index = System.Array.BinarySearch(along, at);

            if (index >= 0)
            {
                return points[index];
            }

            index = ~index;

            if (index <= 0)
            {
                return points[0];
            }

            if (index >= points.Length)
            {
                return points[points.Length - 1];
            }

            var span = along[index] - along[index - 1];

            return span <= 0f ? points[index] : Vector2.Lerp(points[index - 1], points[index], (at - along[index - 1]) / span);
        }

        // How far along the path the nearest place to a position is.
        public float Project(Vector2 position)
        {
            var best = 0f;
            var bestSquared = float.PositiveInfinity;

            for (var i = 1; i < points.Length; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var run = b - a;
                var lengthSquared = run.sqrMagnitude;
                var t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(position - a, run) / lengthSquared) : 0f;
                var squared = (position - (a + run * t)).sqrMagnitude;

                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    best = along[i - 1] + t * Mathf.Sqrt(lengthSquared);
                }
            }

            return best;
        }

        private static bool Rounded(int index, int count, bool closed, float radius)
        {
            return radius > 0f && (closed || (index > 0 && index < count - 1));
        }

        public static Route Build(IList<Vector2> given, bool closed, float radius)
        {
            // The same point twice in a row is one.
            var v = new List<Vector2>();

            foreach (var p in given)
            {
                if (v.Count == 0 || (p - v[v.Count - 1]).sqrMagnitude > 0.000001f)
                {
                    v.Add(p);
                }
            }

            if (closed && v.Count > 1 && (v[0] - v[v.Count - 1]).sqrMagnitude <= 0.000001f)
            {
                v.RemoveAt(v.Count - 1);
            }

            var count = v.Count;
            var route = new Route { closed = closed && count >= 2 };

            if (count < 2)
            {
                route.points = new[] { count > 0 ? v[0] : Vector2.zero };
                route.along = new[] { 0f };
                route.stops = new[] { 0f };
                route.ends = new[] { 0f };
                return route;
            }

            // The points that make up each corner: just the corner, or the arc that rounds it.
            var pieces = new List<Vector2>[count];
            var apexes = new int[count];

            for (var i = 0; i < count; i++)
            {
                pieces[i] = new List<Vector2>();

                if (Rounded(i, count, route.closed, radius))
                {
                    var previous = v[(i - 1 + count) % count];
                    var following = v[(i + 1) % count];
                    var inVector = v[i] - previous;
                    var outVector = following - v[i];
                    var inLength = inVector.magnitude;
                    var outLength = outVector.magnitude;
                    var inDirection = inVector / inLength;
                    var outDirection = outVector / outLength;
                    var turn = Vector2.Angle(inDirection, outDirection);

                    // A corner that hardly turns, or turns right back on itself, can't be, or needn't be, rounded.
                    if (turn >= 1f && turn <= 175f)
                    {
                        var half = turn * 0.5f * Mathf.Deg2Rad;

                        // The arc starts this far before the corner: as far as the radius needs, but no more than the room there is,
                        // which is half of a piece shared with another rounded corner.
                        var room = Mathf.Min(
                            inLength * (Rounded((i - 1 + count) % count, count, route.closed, radius) ? 0.5f : 1f),
                            outLength * (Rounded((i + 1) % count, count, route.closed, radius) ? 0.5f : 1f));
                        var tangent = Mathf.Min(radius * Mathf.Tan(half), room);
                        var arcRadius = tangent / Mathf.Tan(half);

                        var start = v[i] - inDirection * tangent;
                        var centre = v[i] + (outDirection - inDirection).normalized * (arcRadius / Mathf.Cos(half));
                        var startAngle = Mathf.Atan2(start.y - centre.y, start.x - centre.x);
                        var sweep = Mathf.Sign(inDirection.x * outDirection.y - inDirection.y * outDirection.x) * turn * Mathf.Deg2Rad;
                        var steps = Mathf.Max(2, Mathf.CeilToInt(turn / 6f));

                        for (var k = 0; k <= steps; k++)
                        {
                            var angle = startAngle + sweep * k / steps;
                            pieces[i].Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * arcRadius);
                        }

                        apexes[i] = steps / 2;
                        continue;
                    }
                }

                pieces[i].Add(v[i]);
                apexes[i] = 0;
            }

            // Put them together in order. A loop starts from the middle of the first corner, which it comes back to.
            var points = new List<Vector2>();
            var apexIndexes = new List<int>();

            if (route.closed)
            {
                var first = pieces[0];

                for (var k = apexes[0]; k < first.Count; k++)
                {
                    points.Add(first[k]);
                }

                apexIndexes.Add(0);

                for (var i = 1; i < count; i++)
                {
                    var apexAt = points.Count + apexes[i];
                    points.AddRange(pieces[i]);
                    apexIndexes.Add(apexAt);
                }

                for (var k = 0; k <= apexes[0]; k++)
                {
                    points.Add(first[k]);
                }
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    apexIndexes.Add(points.Count + apexes[i]);
                    points.AddRange(pieces[i]);
                }
            }

            route.points = points.ToArray();
            route.along = new float[points.Count];

            for (var i = 1; i < points.Count; i++)
            {
                route.along[i] = route.along[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }

            route.total = route.along[points.Count - 1];
            route.stops = new float[apexIndexes.Count];

            for (var i = 0; i < apexIndexes.Count; i++)
            {
                route.stops[i] = route.along[apexIndexes[i]];
            }

            route.ends = route.closed ? new float[0] : new[] { 0f, route.total };

            // The first point is the second place on the route, after the start, unless the start is on it, and then they are one.
            var startIsFirst = given.Count > 1 && (given[1] - given[0]).sqrMagnitude <= 0.000001f;
            route.leadIn = route.closed || startIsFirst ? 0f : route.stops[1];
            return route;
        }
    }

    // The path, as it will be followed, with its points, drawn when the platform is selected.
    public override Vector3 GizmoPoint
    {
        get { return platform ? platform.position : transform.position; }
    }

    void OnDrawGizmosSelected()
    {
        var target = platform ? platform : transform;
        var start = Application.isPlaying ? startPosition : (Vector2)target.position;

        // The way from the start through the points...
        var shown = Route.Build(Vertices(start, true), false, cornerRadius);

        Gizmos.color = Color.cyan;

        for (var i = 1; i < shown.points.Length; i++)
        {
            Gizmos.DrawLine(shown.points[i - 1], shown.points[i]);
        }

        // ...and, for a loop, the points joined end to start, which is the part it goes round.
        if (pathEnd == PathEndAction.Loop)
        {
            var round = Route.Build(Vertices(start, false), true, cornerRadius);

            for (var i = 1; i < round.points.Length; i++)
            {
                Gizmos.DrawLine(round.points[i - 1], round.points[i]);
            }
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(start, 0.2f);

        foreach (var point in pathPoints)
        {
            if (point)
            {
                Gizmos.DrawWireSphere(point.position, 0.15f);
            }
        }
    }
}
