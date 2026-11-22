using System.Collections.Generic;
using UnityEngine;

// Visits a list of destinations in order, like EnemyPathFollow, but on foot: it obeys gravity, walks off ledges
// and falls, and jumps up walls (just high enough to clear them) to get where it's going. Needs a Rigidbody2D
// (Dynamic, Gravity Scale above 0) and a Collider2D.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyGroundPath : MonoBehaviour
{
    [Tooltip("The places the enemy walks to, in order, and then round again. Drag in objects, placed where it should go.")]
    public List<GameObject> destinations = new List<GameObject>();

    [Tooltip("The destination to head to first. Leave empty to start with the first one in the list.")]
    public GameObject whichObjectFirst;

    [Tooltip("Visit the list from last to first instead of first to last.")]
    public bool reversePath;

    [Tooltip("Walking speed, in units per second.")]
    public float velocity = 2f;

    [Tooltip("How close (sideways) the enemy has to get to a destination before it heads for the next one.")]
    public float distanceFromTargetToMoveToNext = 0.1f;

    [Tooltip("Seconds to wait on arriving at a destination. A Path Destination component on a destination overrides this for that one.")]
    public float pauseAtDestination;

    [Header("Jumping and falling")]
    [Tooltip("The highest it can jump, measured from where it takes off to the top of the jump. It only jumps as high as each ledge needs (plus a little clearance), however high this is set. A wall taller than this can't be climbed. Very large values make route searches slower.")]
    public float maxJumpHeight = 2f;

    [Tooltip("The furthest it will drop. Ledges higher than this are never walked off.")]
    public float maxFallDistance = 8f;

    [Tooltip("Off: it only jumps at walls and ledges (cheap). On: it also considers jumping from anywhere along a route, so it can reach platforms overhead, at the cost of slower route searches.")]
    public bool jumpAnywhere = true;

    [Header("Route search")]
    [Tooltip("What counts as ground and walls. Leave empty to use the Ground layer.")]
    public LayerMask obstacleLayers;

    [Tooltip("Resolution of the route search, in world units. Smaller is more precise but searches slower.")]
    public float pathCellSize = 0.5f;

    [Tooltip("Most spots one search will look at. If the destination can't be reached within that, it's skipped.")]
    public int maxSearchNodes = 600;

    [Tooltip("How long, in seconds, the enemy remembers a jump or fall that didn't work when it tried it, and steers routes away from it. Each repeat failure makes it more wary. 0 means it never learns. RefreshRoute() forgets what it learned.")]
    public float failedMoveMemorySeconds = 120f;

    [Tooltip("How long, in seconds, a destination found to be out of reach is skipped without searching for it again (when the enemy is back near where it failed). 0 searches every time. RefreshRoute() forgets what it learned.")]
    public float unreachableMemorySeconds = 30f;

    [Header("Debugging")]
    [Tooltip("Write to the Console whenever the enemy replans its route, gives up on a destination, or stands still for a while, saying why.")]
    public bool logProblems = true;

    [Tooltip("Write a line to the Console each time a route is found, saying how long it took to search and over how many frames.")]
    public bool logPlanning;

    [Tooltip("The most time, in milliseconds, that all ground enemies together spend searching for routes in one frame. Lower means smoother frames but slower decisions.")]
    public float planningBudgetMilliseconds = 3f;

    [Header("Physics")]
    [Tooltip("Remove friction while in the air, so the enemy can't catch on a wall it's jumping alongside.")]
    public bool slipperyInAir = true;

    [Space]
    [Tooltip("The place in the list that it's heading for now, counting from 0. Changes as it goes.")]
    public int currentDestination;

    private const float StuckSeconds = 1f;
    private const float MaxStepSeconds = 6f;
    private const float TakeoffSeconds = 0.1f;
    private const float UnreachableDelay = 2f;
    private const float IdleLogSeconds = 0.75f;

    // How near to where a route ahead was planned to start the enemy must be for it to be used.
    private const float PreparedRouteTolerance = 0.5f;

    // How far past a jump's planned takeoff spot the enemy may be and still jump from where it is.
    private const float TakeoffOvershoot = 0.75f;

    // How near to where it failed the enemy must be for a remembered failure to still apply.
    private const float UnreachableMemoryRadius = 2f;
    private const int MaxFailures = 5;

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private PhysicsMaterial2D groundMaterial;
    private PhysicsMaterial2D airMaterial;
    private bool onAirMaterial;
    private Vector2 centerOffset;
    private float gravity;

    private float pauseTimeRemaining;
    private float unreachableTimer;
    private bool hasReachedDestination;
    private bool wasReversing;

    // The route to the current destination. Steps are positions of the collider's centre, not the transform.
    private readonly List<GroundStep> plan = new List<GroundStep>();
    private int planIndex;
    private bool needsPlan = true;

    // A route search in progress, picked up again each frame until it's done.
    private GroundPathFinder2D search;
    private Vector2 searchStart;
    private Vector2 searchGoal;

    // The route onward to the next destination, searched for while still on the way to the current one.
    private GroundPathFinder2D lookahead;
    private GameObject lookaheadDestination;
    private Vector2 lookaheadTarget;
    private Vector2 lookaheadStart;
    private Vector2 lookaheadGoal;
    private float lookaheadSpeed;
    private PreparedRoute prepared;

    private class PreparedRoute
    {
        public GameObject destination;
        public Vector2 target;
        public float speed;
        public Vector2 start;
        public List<GroundStep> steps;
        public float cost;
    }

    // Destinations found to be out of reach, and from where, so they aren't searched for again every lap.
    private struct UnreachableRecord
    {
        public Vector2 target;
        public Vector2 from;
        public float until;
    }

    private readonly Dictionary<GameObject, UnreachableRecord> unreachable = new Dictionary<GameObject, UnreachableRecord>();

    // Jumps and falls that were planned but didn't work out, so routes can steer away from them.
    private readonly List<FailedMove> failedMoves = new List<FailedMove>();
    private GameObject planDestination;
    private Vector2 planTarget;
    private float planSpeed;

    private bool stepStarted;
    private float stepTime;
    private float takeoffTimer;
    private bool wasAirborne;

    private float stuckTimer;
    private Vector2 stuckPosition;
    private int failures;
    private int unreachableInARow;

    // Why the enemy is standing still this physics step, if it is, and how long that has gone on.
    private string idleReason;
    private string lastIdleReason;
    private string pauseReason;
    private float idleTime;
    private bool idleLogged;
    private readonly HashSet<GameObject> reportedUnreachable = new HashSet<GameObject>();
    private GameObject progressDestination;
    private float bestRouteCost;

    // Destinations are usually children of the enemy so they stay organised with it, which means they would
    // normally travel along with the enemy and never be reachable. A destination's world position is recorded
    // the moment it joins the path and used as the real target; LateUpdate then pins the object back to that spot.
    private Dictionary<GameObject, Vector3> destinationPositions = new Dictionary<GameObject, Vector3>();

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        wasReversing = reversePath;

        foreach (var candidate in GetComponents<Collider2D>())
        {
            if (!candidate.isTrigger)
            {
                col = candidate;
                break;
            }
        }

        if (!col)
        {
            Debug.LogError($"{name}: EnemyGroundPath needs a Collider2D.", this);
            enabled = false;
            return;
        }

        gravity = Physics2D.gravity.y * rb.gravityScale;

        if (rb.bodyType != RigidbodyType2D.Dynamic || gravity >= 0f)
        {
            Debug.LogError($"{name}: EnemyGroundPath needs a Dynamic Rigidbody2D with a Gravity Scale above 0.", this);
            enabled = false;
            return;
        }

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        groundMaterial = col.sharedMaterial;
        airMaterial = new PhysicsMaterial2D("EnemyGroundPathAir") { friction = 0f, bounciness = 0f };

        centerOffset = (Vector2)col.bounds.center - (Vector2)transform.position;

        if (obstacleLayers.value == 0)
        {
            obstacleLayers = LayerMask.GetMask("Ground");
        }

        PurgeDestroyed();

        foreach (var destination in destinations)
        {
            RecordedPositionOf(destination);
        }

        if (destinations.Count == 0)
        {
            print($"{name}: EnemyGroundPath has no destinations yet.");
        }

        var firstIndex = whichObjectFirst ? destinations.IndexOf(whichObjectFirst) : 0;
        currentDestination = Mathf.Max(firstIndex, 0);
    }

    void Update()
    {
        if (spriteRenderer && rb && Mathf.Abs(rb.linearVelocity.x) > 0.05f)
        {
            spriteRenderer.flipX = rb.linearVelocity.x < 0f;
        }
    }

    void FixedUpdate()
    {
        idleReason = null;
        Tick();
        TrackIdle();
    }

    // Says why the enemy is standing still when it's been doing so for a noticeable time. Whatever holds it still
    // sets idleReason during Tick; if nothing does, it's moving.
    private void TrackIdle()
    {
        if (idleReason == null)
        {
            if (idleLogged && logProblems)
            {
                Debug.Log($"{name}: moving again after {idleTime:0.0} seconds of {lastIdleReason}.", this);
            }

            idleTime = 0f;
            idleLogged = false;
            return;
        }

        idleTime += Time.fixedDeltaTime;
        lastIdleReason = idleReason;

        if (!idleLogged && idleTime >= IdleLogSeconds)
        {
            idleLogged = true;

            if (logProblems)
            {
                Debug.Log($"{name}: standing still for over {IdleLogSeconds} seconds - {idleReason}.", this);
            }
        }
    }

    private void Tick()
    {
        PurgeDestroyed();

        if (destinations.Count == 0)
        {
            return;
        }

        if (currentDestination < 0 || currentDestination >= destinations.Count)
        {
            currentDestination = 0;
        }

        if (reversePath != wasReversing)
        {
            wasReversing = reversePath;
            TurnAround();
        }

        takeoffTimer -= Time.fixedDeltaTime;

        var grounded = takeoffTimer <= 0f && IsGrounded();
        Vector2 position = col.bounds.center;

        SetSlippery(!grounded);

        if (!grounded)
        {
            wasAirborne = true;

            // Walking off a ledge is the start of a fall step.
            if (!needsPlan && planIndex < plan.Count && plan[planIndex].type == GroundMoveType.Fall)
            {
                stepStarted = true;
            }
        }

        var destination = destinations[currentDestination];
        var speed = SpeedTowards(destination);
        var target = (Vector2)RecordedPositionOf(destination) + centerOffset;

        // A different destination, one that has been moved, or a new speed makes the old route useless.
        if (destination != planDestination || target != planTarget || !Mathf.Approximately(speed, planSpeed))
        {
            ClearPlan();
            planDestination = destination;
            planTarget = target;
            planSpeed = speed;
        }

        if (grounded && wasAirborne)
        {
            wasAirborne = false;
            OnLanded(position);
            return;
        }

        if (needsPlan)
        {
            if (!grounded)
            {
                return;
            }

            if (unreachableTimer > 0f)
            {
                unreachableTimer -= Time.fixedDeltaTime;
                idleReason = $"waiting {Mathf.Max(unreachableTimer, 0f):0.0} more seconds because none of the destinations could be reached";
                SetVelocityX(0f);
                return;
            }

            // Already worked out while it was heading here, and it ended up where that route starts.
            if (prepared != null && prepared.destination == destination && prepared.target == target
                && Mathf.Approximately(prepared.speed, speed)
                && Vector2.Distance(position, prepared.start) <= PreparedRouteTolerance)
            {
                var route = prepared;
                prepared = null;

                ApplyRoute(route.steps, route.cost, "prepared on the way");
            }
            else if (IsKnownUnreachable(destination, target, position))
            {
                SkipUnreachable(null, destination);
                return;
            }
            else
            {
                var planning = PlanRoute(position, target, speed, out var problem);

                if (planning == Planning.Thinking)
                {
                    idleReason = $"searching for a route to {destination.name} ({search.FramesTaken} frames, {search.MillisecondsSpent:0} ms of searching so far)";
                    SetVelocityX(0f);
                    return;
                }

                if (planning == Planning.Unreachable)
                {
                    RememberUnreachable(destination, target, position);
                    SkipUnreachable(problem, destination);
                    return;
                }
            }

            unreachableInARow = 0;
        }

        // Planning carries on during a pause, so the route is ready when the pause is over.
        if (pauseTimeRemaining > 0f)
        {
            pauseTimeRemaining -= Time.fixedDeltaTime;
            idleReason = $"{pauseReason} ({Mathf.Max(pauseTimeRemaining, 0f):0.0} seconds left)";

            if (grounded)
            {
                SetVelocityX(0f);
            }

            LookAhead(destination);
            return;
        }

        if (planIndex < plan.Count)
        {
            ExecuteStep(plan[planIndex], position, speed, grounded);
        }
        else
        {
            FinishApproach(destination, position, target, speed, grounded);
        }

        LookAhead(destination);
    }

    // No way to get there, so move on rather than standing around forever. The reason is said once per destination,
    // not every time it comes round again.
    private void SkipUnreachable(string problem, GameObject destination)
    {
        if (problem != null && reportedUnreachable.Add(destination))
        {
            Debug.LogWarning($"{name}: can't reach {destination.name}, skipping it - {problem}", this);
        }

        // Only once every destination has failed in a row does it wait before trying again, so it doesn't spend
        // its time searching for routes that can't exist.
        unreachableInARow++;

        if (unreachableInARow >= destinations.Count)
        {
            unreachableInARow = 0;
            unreachableTimer = UnreachableDelay;
        }

        Advance();
        SetVelocityX(0f);
    }

    private bool IsKnownUnreachable(GameObject destination, Vector2 target, Vector2 from)
    {
        return unreachableMemorySeconds > 0f
            && unreachable.TryGetValue(destination, out var known)
            && Time.time < known.until
            && known.target == target
            && Vector2.Distance(known.from, from) <= UnreachableMemoryRadius;
    }

    private void RememberUnreachable(GameObject destination, Vector2 target, Vector2 from)
    {
        if (unreachableMemorySeconds > 0f)
        {
            unreachable[destination] = new UnreachableRecord
            {
                target = target, from = from, until = Time.time + unreachableMemorySeconds
            };
        }
    }

    // While the enemy is heading for one destination, works out the route onward to the one after, starting from
    // where the current route ends. By the time it gets there the next route is ready and it doesn't stop to think.
    private void LookAhead(GameObject current)
    {
        // Once it has arrived and moved on to the next destination (this very step), `current` is already behind it.
        if (needsPlan || destinations.Count < 2 || current != destinations[currentDestination])
        {
            return;
        }

        // Where the enemy will be standing on arrival: the end of the route in height, and right at the destination
        // sideways, since the last stretch of walking carries it all the way there.
        var arrivalY = plan.Count > 0 ? plan[plan.Count - 1].to.y : col.bounds.center.y;
        var start = new Vector2(RecordedPositionOf(current).x + centerOffset.x, arrivalY);

        // The next destination in line that isn't already known to be out of reach.
        GameObject next = null;
        var target = Vector2.zero;
        var speed = 0f;
        var index = currentDestination;

        for (var tries = 0; tries < destinations.Count - 1; tries++)
        {
            index += reversePath ? -1 : 1;
            index = (index + destinations.Count) % destinations.Count;

            var candidate = destinations[index];

            if (candidate == current)
            {
                break;
            }

            var candidateTarget = (Vector2)RecordedPositionOf(candidate) + centerOffset;

            if (IsKnownUnreachable(candidate, candidateTarget, start))
            {
                continue;
            }

            next = candidate;
            target = candidateTarget;
            speed = SpeedTowards(candidate);
            break;
        }

        if (next == null)
        {
            return;
        }

        // Already done, and still starting from the right place.
        if (prepared != null && prepared.destination == next && prepared.target == target
            && Mathf.Approximately(prepared.speed, speed) && Vector2.Distance(prepared.start, start) <= 0.25f)
        {
            return;
        }

        // The search under way is for something that has changed.
        if (lookahead != null
            && (lookaheadDestination != next || lookaheadTarget != target || !Mathf.Approximately(lookaheadSpeed, speed)))
        {
            lookahead = null;
        }

        if (lookahead == null)
        {
            lookahead = new GroundPathFinder2D(
                col.bounds.size, obstacleLayers, speed, gravity, pathCellSize,
                maxJumpHeight, maxFallDistance, jumpAnywhere, maxSearchNodes, ActiveFailedMoves());

            if (!lookahead.TryFindStand(target, out lookaheadGoal))
            {
                lookahead = null;
                RememberUnreachable(next, target, start);

                if (reportedUnreachable.Add(next))
                {
                    Debug.LogWarning($"{name}: can't reach {next.name}, it will be skipped - there's no ground at or below it.", this);
                }

                return;
            }

            lookaheadDestination = next;
            lookaheadTarget = target;
            lookaheadSpeed = speed;
            lookaheadStart = start;
            lookahead.BeginSearch(start, lookaheadGoal);
        }

        GroundPlanningBudget.MillisecondsPerFrame = planningBudgetMilliseconds;

        if (!lookahead.Advance())
        {
            return;
        }

        var finder = lookahead;
        lookahead = null;

        if (logPlanning)
        {
            Debug.Log(
                $"{name}: while on the way, searched {finder.NodesSearched} spots for {next.name} in {finder.MillisecondsSpent:0.0} ms " +
                $"over {finder.FramesTaken} frame(s) - {(finder.ReachedGoal ? $"found a route of {finder.Steps.Count} steps" : "no route")}.",
                this);
        }

        if (finder.ReachedGoal)
        {
            prepared = new PreparedRoute
            {
                destination = next, target = target, speed = speed,
                start = start, steps = finder.Steps, cost = finder.RouteCost
            };

            return;
        }

        RememberUnreachable(next, target, start);

        if (reportedUnreachable.Add(next))
        {
            Debug.LogWarning(
                $"{name}: can't reach {next.name}, it will be skipped - {ExplainNoRoute(finder, lookaheadStart, lookaheadGoal)}", this);
        }
    }

    void LateUpdate()
    {
        foreach (var destination in destinations)
        {
            if (destination)
            {
                destination.transform.position = RecordedPositionOf(destination);
            }
        }
    }

    // Throws away the current route, and everything worked out or learned about routes so far, and plans afresh from
    // where the enemy is. Call it after anything that changes what the enemy can do or what it has to get past: its
    // jump height, fall distance, gravity or collider size, or changes to the map. (A change to its speed, or to a
    // destination, is noticed on its own.) The enemy finishes any jump or fall it's in the middle of first.
    public void RefreshRoute()
    {
        // Gravity is read once at the start, and what a jump can reach depends on it.
        if (rb)
        {
            gravity = Physics2D.gravity.y * rb.gravityScale;
        }

        // None of this can be trusted now: routes prepared ahead, destinations that used to be out of reach, and
        // jumps and falls that failed when the enemy could do something different.
        prepared = null;
        lookahead = null;
        unreachable.Clear();
        failedMoves.Clear();
        reportedUnreachable.Clear();
        unreachableInARow = 0;
        unreachableTimer = 0f;
        failures = 0;

        ClearPlan();
    }

    // Places newDestination right after precedingDestination, or at the start if precedingDestination is null.
    // The new destination's current world position becomes its fixed target.
    public bool AddDestination(GameObject newDestination, GameObject precedingDestination = null)
    {
        if (!newDestination)
        {
            Debug.LogError($"{name}: AddDestination was given a null destination.", this);
            return false;
        }

        PurgeDestroyed();

        var insertIndex = 0;

        // ReferenceEquals so a destroyed object counts as "doesn't exist" rather than being read as null.
        if (!ReferenceEquals(precedingDestination, null))
        {
            var precedingIndex = destinations.IndexOf(precedingDestination);

            if (precedingIndex < 0)
            {
                Debug.LogError($"{name}: AddDestination couldn't find the preceding destination in the current path.", this);
                return false;
            }

            insertIndex = precedingIndex + 1;
        }

        RecordedPositionOf(newDestination);
        destinations.Insert(insertIndex, newDestination);

        // Keep heading for the same destination as before rather than being shifted onto a different one.
        if (destinations.Count > 1 && insertIndex <= currentDestination)
        {
            currentDestination++;
        }

        return true;
    }

    // Removes every occurrence of the destination. The object itself is not destroyed.
    public bool RemoveDestination(GameObject destination)
    {
        if (!destination)
        {
            return false;
        }

        PurgeDestroyed();

        var index = destinations.IndexOf(destination);

        if (index < 0)
        {
            return false;
        }

        while (index >= 0)
        {
            RemoveAtIndex(index);
            index = destinations.IndexOf(destination);
        }

        destinationPositions.Remove(destination);
        return true;
    }

    public bool MoveDestination(GameObject destination, Vector3 newPosition)
    {
        if (!destination)
        {
            return false;
        }

        PurgeDestroyed();

        if (!destinations.Contains(destination))
        {
            return false;
        }

        destinationPositions[destination] = newPosition;
        destination.transform.position = newPosition;
        return true;
    }

    // Note: this is 1-based (1 is the first destination), unlike currentDestination, which is 0-based.
    public GameObject GetDestinationFromIndex(int index)
    {
        PurgeDestroyed();

        return index >= 1 && index <= destinations.Count ? destinations[index - 1] : null;
    }

    public GameObject GetFirstDestination()
    {
        PurgeDestroyed();

        return destinations.Count > 0 ? destinations[0] : null;
    }

    public GameObject GetLastDestination()
    {
        PurgeDestroyed();

        return destinations.Count > 0 ? destinations[destinations.Count - 1] : null;
    }

    public int GetDestinationCount()
    {
        PurgeDestroyed();

        return destinations.Count;
    }

    // A copy, so changing it doesn't change the path - use the other functions for that.
    public List<GameObject> GetAllDestinations()
    {
        PurgeDestroyed();

        return new List<GameObject>(destinations);
    }

    // Replaces the whole path and starts again from its first destination. Destinations already on the path
    // keep their recorded positions; new ones use their current world position. Returns false and leaves the
    // current path alone if the list is null or contains a null destination. An empty list is allowed and
    // leaves the enemy with nowhere to go.
    public bool SetNewPath(IEnumerable<GameObject> newDestinations)
    {
        if (newDestinations == null)
        {
            Debug.LogError($"{name}: SetNewPath was given a null list.", this);
            return false;
        }

        var path = new List<GameObject>(newDestinations);

        foreach (var destination in path)
        {
            if (!destination)
            {
                Debug.LogError($"{name}: SetNewPath was given a list containing a null destination.", this);
                return false;
            }
        }

        var newPositions = new Dictionary<GameObject, Vector3>();

        foreach (var destination in path)
        {
            newPositions[destination] = destinationPositions.TryGetValue(destination, out var existing)
                ? existing
                : destination.transform.position;
        }

        destinations.Clear();
        destinations.AddRange(path);
        destinationPositions = newPositions;
        currentDestination = 0;
        hasReachedDestination = false;
        ClearPlan();
        return true;
    }

    private enum Planning
    {
        Thinking,
        Ready,
        Unreachable
    }

    // Works on the route for a little while each frame, so planning never hogs a frame however far it has to look.
    private Planning PlanRoute(Vector2 position, Vector2 target, float speed, out string problem)
    {
        problem = null;

        // The enemy stands still while it thinks, so the route is still right when it's done. If it was shoved
        // somewhere else, start again from there.
        if (search != null && (position - searchStart).sqrMagnitude > 0.25f)
        {
            search = null;
        }

        if (search == null)
        {
            search = new GroundPathFinder2D(
                col.bounds.size, obstacleLayers, speed, gravity, pathCellSize,
                maxJumpHeight, maxFallDistance, jumpAnywhere, maxSearchNodes, ActiveFailedMoves());

            if (!search.TryFindStand(target, out searchGoal))
            {
                problem = $"there's no ground at or below the destination (looked from {target} down 30 units, lifting it " +
                          "up to 2 units out of anything solid). Is it over a bottomless gap, or is the Ground layer wrong?";
                search = null;
                return Planning.Unreachable;
            }

            searchStart = position;
            search.BeginSearch(position, searchGoal);
        }

        GroundPlanningBudget.MillisecondsPerFrame = planningBudgetMilliseconds;

        if (!search.Advance())
        {
            return Planning.Thinking;
        }

        var finder = search;
        search = null;

        if (logPlanning)
        {
            Debug.Log(
                $"{name}: searched {finder.NodesSearched} spots for {planDestination.name} in {finder.MillisecondsSpent:0.0} ms " +
                $"over {finder.FramesTaken} frame(s) - {(finder.ReachedGoal ? $"found a route of {finder.Steps.Count} steps" : "no route")}.",
                this);
        }

        if (!finder.ReachedGoal)
        {
            problem = ExplainNoRoute(finder, searchStart, searchGoal);
            return Planning.Unreachable;
        }

        ApplyRoute(finder.Steps, finder.RouteCost, "searched just now");
        return Planning.Ready;
    }

    // Makes a route the one being followed.
    private void ApplyRoute(List<GroundStep> steps, float routeCost, string source)
    {
        if (logPlanning)
        {
            var text = new System.Text.StringBuilder();

            foreach (var step in steps)
            {
                text.Append($"\n  {step.type}: ({step.from.x:0.00}, {step.from.y:0.00}) to ({step.to.x:0.00}, {step.to.y:0.00})");

                if (step.type == GroundMoveType.Jump)
                {
                    text.Append($" at speed {step.velocityY:0.00} up");
                }
            }

            Debug.Log($"{name}: following the route to {planDestination.name} ({source}), from {col.bounds.center}:{text}", this);
        }

        // Reachable after all, so say so again if it ever isn't.
        reportedUnreachable.Remove(planDestination);
        unreachable.Remove(planDestination);

        // A new route that's quicker than any before it for this destination means the enemy is getting somewhere,
        // even if it keeps landing a little off the plan (down a staircase, say), so that isn't counted against it.
        if (planDestination != progressDestination)
        {
            progressDestination = planDestination;
            bestRouteCost = float.MaxValue;
        }

        if (routeCost < bestRouteCost - 0.05f)
        {
            bestRouteCost = routeCost;
            failures = 0;
        }

        plan.Clear();
        plan.AddRange(steps);
        planIndex = 0;
        needsPlan = false;
        ResetStep();
    }

    private string ExplainNoRoute(GroundPathFinder2D finder, Vector2 position, Vector2 goal)
    {
        var above = goal.y - position.y;
        var side = goal.x - position.x;

        var message = $"no route found from {position} to the ground at {goal} " +
                      $"({Mathf.Abs(above):0.0} {(above >= 0f ? "above" : "below")}, " +
                      $"{Mathf.Abs(side):0.0} {(side >= 0f ? "right" : "left")} of the enemy). " +
                      $"Searched {finder.NodesSearched} spots and got no closer than {finder.ClosestPosition}.";

        if (finder.HitNodeLimit)
        {
            message += $" The search stopped at Max Search Nodes ({maxSearchNodes}) with places still to try - raise it.";
        }
        else
        {
            message += " It ran out of places to go, so every way forward is blocked.";
        }

        if (above > 0.5f && !jumpAnywhere)
        {
            message += " Jump Anywhere is off, so it only jumps at walls and ledges and can't climb onto a platform from open floor - turn it on.";
        }

        // What it would take to get past wherever it got stuck, heading toward the destination.
        var direction = goal.x >= finder.ClosestPosition.x ? 1f : -1f;

        finder.DiagnoseBlockage(
            finder.ClosestPosition, direction,
            out var jumpHeight, out var jumpLanding, out var fallDistance, out var fallLanding);

        message += $" At {finder.ClosestPosition}, heading {(direction > 0f ? "right" : "left")}:";

        message += jumpHeight > 0f
            ? $" jumping up a ledge there needs a Max Jump Height of at least {jumpHeight:0.00} (it's {maxJumpHeight}; that lands at {jumpLanding})."
            : " there's no ledge it could jump up onto, even with a very high jump.";

        message += fallDistance > 0f
            ? $" Going off the edge there is a drop of {fallDistance:0.00} (Max Fall Distance is {maxFallDistance}; it would land at {fallLanding})."
            : " there's no drop-off to fall down.";

        return message;
    }

    private void ExecuteStep(GroundStep step, Vector2 position, float speed, bool grounded)
    {
        var tolerance = Mathf.Max(0.05f, speed * Time.fixedDeltaTime);

        switch (step.type)
        {
            case GroundMoveType.Walk:
            {
                var direction = Mathf.Sign(step.velocityX);

                if ((position.x - step.to.x) * direction >= -tolerance)
                {
                    CompleteStep();
                    return;
                }

                if (grounded)
                {
                    SetVelocityX(direction * speed);
                }

                CheckStuck(position);
                return;
            }

            case GroundMoveType.Jump:
            {
                if (stepStarted)
                {
                    Fly(step, position);
                    return;
                }

                if (!grounded)
                {
                    return;
                }

                var toTakeoff = step.from.x - position.x;

                // How far the takeoff spot is ahead of the enemy, in the direction the jump goes. Negative means it
                // has already walked or landed past it.
                var ahead = Mathf.Abs(step.velocityX) > 0.001f ? toTakeoff * Mathf.Sign(step.velocityX) : Mathf.Abs(toTakeoff);

                // Not quite at the takeoff spot yet.
                if (ahead > tolerance * 2f)
                {
                    SetVelocityX(Mathf.Sign(toTakeoff) * speed);
                    CheckStuck(position);
                    return;
                }

                // A little past it, usually from landing further along than planned. Jumping from here works just as
                // well, and walking back to the exact spot first would look like backtracking.
                if (ahead < -TakeoffOvershoot)
                {
                    SetVelocityX(Mathf.Sign(toTakeoff) * speed);
                    CheckStuck(position);
                    return;
                }

                rb.linearVelocity = new Vector2(step.velocityX, step.velocityY);
                takeoffTimer = TakeoffSeconds;
                stepStarted = true;
                stepTime = 0f;
                return;
            }

            case GroundMoveType.Fall:
            {
                if (stepStarted)
                {
                    Fly(step, position);
                    return;
                }

                // Still on the ledge: keep walking until it runs out.
                if (grounded)
                {
                    SetVelocityX(Mathf.Sign(step.velocityX) * speed);
                    CheckStuck(position);
                }

                return;
            }
        }
    }

    // In the air during a jump or fall: keep pushing toward where it should land, but not past it.
    private void Fly(GroundStep step, Vector2 position)
    {
        stepTime += Time.fixedDeltaTime;

        if (stepTime > MaxStepSeconds)
        {
            RequestReplan($"still in the air after {MaxStepSeconds} seconds at {position} on {Describe(step)}");
            return;
        }

        var pushing = Mathf.Abs(step.velocityX) > 0.001f
            && (step.to.x - position.x) * Mathf.Sign(step.velocityX) > 0.02f;

        SetVelocityX(pushing ? step.velocityX : 0f);
    }

    // The route is done; walk the last stretch to the destination itself.
    private void FinishApproach(GameObject destination, Vector2 position, Vector2 target, float speed, bool grounded)
    {
        if (!grounded)
        {
            return;
        }

        // Never smaller than one step, or a fast enemy could skip right over the arrival zone.
        var arrivalDistance = Mathf.Max(distanceFromTargetToMoveToNext, speed * Time.fixedDeltaTime);
        var offset = target.x - position.x;

        if (Mathf.Abs(offset) <= arrivalDistance)
        {
            hasReachedDestination = true;
            pauseTimeRemaining = PauseAt(destination);
            pauseReason = $"pausing at {destination.name} (" +
                          (destination.TryGetComponent(out PathDestination settings) && settings.pauseSeconds >= 0f
                              ? "its Path Destination's Pause Seconds"
                              : "the enemy's Pause At Destination") + ")";
            failures = 0;
            stuckTimer = 0f;

            Advance();

            if (pauseTimeRemaining > 0f)
            {
                SetVelocityX(0f);
            }

            return;
        }

        SetVelocityX(Mathf.Sign(offset) * speed);
        CheckStuck(position);
    }

    // Called each time the enemy touches down after being in the air.
    private void OnLanded(Vector2 position)
    {
        if (needsPlan)
        {
            return;
        }

        if (planIndex < plan.Count && stepStarted)
        {
            var step = plan[planIndex];

            // A jump or fall that ended where it was meant to carries on with the route.
            if (step.type != GroundMoveType.Walk
                && Mathf.Abs(position.x - step.to.x) <= pathCellSize + 0.4f
                && Mathf.Abs(position.y - step.to.y) <= 0.75f)
            {
                CompleteStep();
                return;
            }

            // Knocked off course, or landed somewhere unexpected.
            RequestReplan($"landed at {position} instead of {step.to} on {Describe(step)}");
            return;
        }

        // It left the ground when the route didn't call for it (a bump, a tiny hop going up a slope) but is back
        // down, still on the route: nothing is wrong, so carry on instead of planning again.
        if (IsOnRoute(position))
        {
            return;
        }

        RequestReplan(planIndex < plan.Count
            ? $"left the ground unexpectedly and landed at {position}, away from {Describe(plan[planIndex])}"
            : $"left the ground unexpectedly and landed at {position} while walking to the destination");
    }

    // Whether the enemy is where the route expects it to be right now. Within a walking step that is anywhere along
    // it, before a jump or fall it's near where that begins, and once the steps are done it's near where they ended.
    private bool IsOnRoute(Vector2 position)
    {
        const float VerticalTolerance = 0.6f;

        if (plan.Count == 0)
        {
            return false;
        }

        if (planIndex >= plan.Count)
        {
            var end = plan[plan.Count - 1].to;

            return Mathf.Abs(position.y - end.y) <= VerticalTolerance;
        }

        var step = plan[planIndex];

        if (step.type == GroundMoveType.Walk)
        {
            return DistanceToSegment(position, step.from, step.to) <= VerticalTolerance;
        }

        return Mathf.Abs(position.x - step.from.x) <= pathCellSize + 0.2f
            && Mathf.Abs(position.y - step.from.y) <= VerticalTolerance;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.sqrMagnitude;
        var along = lengthSquared < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);

        return Vector2.Distance(point, a + ab * along);
    }

    // Stuck means it was supposed to be moving and hasn't gone anywhere for a while.
    private void CheckStuck(Vector2 position)
    {
        if ((position - stuckPosition).sqrMagnitude > 0.0025f)
        {
            stuckPosition = position;
            stuckTimer = 0f;
            return;
        }

        stuckTimer += Time.fixedDeltaTime;

        if (stuckTimer >= StuckSeconds)
        {
            stuckTimer = 0f;

            RequestReplan(planIndex < plan.Count
                ? $"hasn't moved for {StuckSeconds} second(s) at {position} on {Describe(plan[planIndex])}"
                : $"hasn't moved for {StuckSeconds} second(s) at {position} while walking to the destination");
        }
    }

    private void RequestReplan(string reason)
    {
        var destinationName = planDestination ? planDestination.name : "its destination";

        // If it was partway through a jump or fall, that move didn't work, so steer clear of it from now on.
        if (planIndex < plan.Count && plan[planIndex].type != GroundMoveType.Walk)
        {
            RememberFailedMove(plan[planIndex]);
        }

        ClearPlan();
        failures++;

        // It keeps failing to get there, so give up on this one.
        if (failures >= MaxFailures)
        {
            if (logProblems)
            {
                Debug.LogWarning(
                    $"{name}: giving up on {destinationName} after {MaxFailures} failed attempts (last: {reason}).", this);
            }

            failures = 0;
            Advance();
            return;
        }

        if (logProblems)
        {
            Debug.LogWarning($"{name}: replanning the route to {destinationName} (attempt {failures}) - {reason}.", this);
        }
    }

    // Notes that a planned jump or fall didn't work. Planning then treats it, and moves very like it, as much more
    // expensive each time it fails, so the enemy tries something else if there is anything else.
    private void RememberFailedMove(GroundStep step)
    {
        if (failedMoveMemorySeconds <= 0f)
        {
            return;
        }

        var until = Time.time + failedMoveMemorySeconds;

        // Anything planned ahead was worked out before knowing this.
        prepared = null;
        lookahead = null;

        for (var i = 0; i < failedMoves.Count; i++)
        {
            var known = failedMoves[i];

            if (known.type == step.type
                && Mathf.Sign(known.to.x - known.from.x) == Mathf.Sign(step.to.x - step.from.x)
                && Vector2.Distance(known.from, step.from) <= 0.5f
                && Vector2.Distance(known.to, step.to) <= 0.75f)
            {
                known.count++;
                known.until = until;
                failedMoves[i] = known;

                if (logProblems)
                {
                    Debug.Log($"{name}: remembering that {step.type} from {step.from} to {step.to} has now failed {known.count} times.", this);
                }

                return;
            }
        }

        failedMoves.Add(new FailedMove { type = step.type, from = step.from, to = step.to, count = 1, until = until });

        if (logProblems)
        {
            Debug.Log($"{name}: remembering that {step.type} from {step.from} to {step.to} failed.", this);
        }
    }

    // What has been learned about moves that don't work, without the things that have been forgotten by now.
    private List<FailedMove> ActiveFailedMoves()
    {
        failedMoves.RemoveAll(move => Time.time >= move.until);

        return new List<FailedMove>(failedMoves);
    }

    private string Describe(GroundStep step)
    {
        return $"{step.type} step {planIndex + 1} of {plan.Count} ({step.from} to {step.to})";
    }

    private void CompleteStep()
    {
        planIndex++;
        ResetStep();
    }

    private void ResetStep()
    {
        stepStarted = false;
        stepTime = 0f;
        stuckTimer = 0f;
        stuckPosition = col ? (Vector2)col.bounds.center : Vector2.zero;
    }

    private void ClearPlan()
    {
        plan.Clear();
        planIndex = 0;
        needsPlan = true;
        planDestination = null;
        search = null;
        ResetStep();
    }

    private bool IsGrounded()
    {
        var bounds = col.bounds;
        var size = new Vector2(Mathf.Max(bounds.size.x - 0.04f, 0.05f), Mathf.Max(bounds.size.y - 0.04f, 0.05f));
        var hit = Physics2D.BoxCast(bounds.center, size, 0f, Vector2.down, 0.1f, obstacleLayers);

        return hit.collider && hit.normal.y > 0.6f;
    }

    private void SetVelocityX(float x)
    {
        rb.linearVelocity = new Vector2(x, rb.linearVelocity.y);
    }

    private void SetSlippery(bool slippery)
    {
        if (!slipperyInAir || slippery == onAirMaterial)
        {
            return;
        }

        onAirMaterial = slippery;
        col.sharedMaterial = slippery ? airMaterial : groundMaterial;
    }

    private float SpeedTowards(GameObject destination)
    {
        return destination.TryGetComponent(out PathDestination settings) && settings.approachSpeed > 0f
            ? settings.approachSpeed
            : velocity;
    }

    private float PauseAt(GameObject destination)
    {
        return destination.TryGetComponent(out PathDestination settings) && settings.pauseSeconds >= 0f
            ? settings.pauseSeconds
            : pauseAtDestination;
    }

    private Vector3 RecordedPositionOf(GameObject destination)
    {
        if (!destinationPositions.TryGetValue(destination, out var position))
        {
            position = destination.transform.position;
            destinationPositions[destination] = position;
        }

        return position;
    }

    private void TurnAround()
    {
        if (!hasReachedDestination)
        {
            return;
        }

        // reversePath has already flipped, so stepping to the next destination in the new direction is the same
        // as going back to the one it was just coming from. Doing it this way swaps correctly on every flip.
        Advance();

        // Paused: it's sitting on that destination already, so carry on to the one after it.
        if (pauseTimeRemaining > 0f)
        {
            Advance();
        }
    }

    private void Advance()
    {
        currentDestination += reversePath ? -1 : 1;

        if (currentDestination >= destinations.Count)
        {
            currentDestination = 0;
        }
        else if (currentDestination < 0)
        {
            currentDestination = destinations.Count - 1;
        }

        failures = 0;
    }

    // Removes one entry while keeping currentDestination pointing at the same place in the path. If the entry
    // being removed is the one the enemy is heading for, it moves on to whichever comes next in its direction.
    private void RemoveAtIndex(int index)
    {
        destinations.RemoveAt(index);

        if (destinations.Count == 0)
        {
            currentDestination = 0;
            return;
        }

        if (index < currentDestination || (index == currentDestination && reversePath))
        {
            currentDestination--;
        }

        if (currentDestination >= destinations.Count)
        {
            currentDestination = 0;
        }
        else if (currentDestination < 0)
        {
            currentDestination = destinations.Count - 1;
        }
    }

    // Drops empty slots and destinations that have been destroyed, so the rest of the script can trust the list.
    private void PurgeDestroyed()
    {
        var purged = false;

        for (var i = destinations.Count - 1; i >= 0; i--)
        {
            if (!destinations[i])
            {
                RemoveAtIndex(i);
                purged = true;
            }
        }

        if (!purged)
        {
            return;
        }

        foreach (var destination in new List<GameObject>(destinationPositions.Keys))
        {
            if (!destination)
            {
                destinationPositions.Remove(destination);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (destinations == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        for (var i = 0; i < destinations.Count; i++)
        {
            var from = destinations[i];
            var to = destinations[(i + 1) % destinations.Count];

            if (!from || !to)
            {
                continue;
            }

            Gizmos.DrawWireSphere(from.transform.position, 0.15f);
            Gizmos.DrawLine(from.transform.position, to.transform.position);
        }

        // The route being followed: yellow for walking, green for jumps, red for falls.
        for (var i = planIndex; i < plan.Count; i++)
        {
            var step = plan[i];

            Gizmos.color = step.type == GroundMoveType.Walk ? Color.yellow
                : step.type == GroundMoveType.Jump ? Color.green
                : Color.red;

            Gizmos.DrawLine(step.from, step.to);
            Gizmos.DrawWireSphere(step.to, 0.08f);
        }
    }
}
