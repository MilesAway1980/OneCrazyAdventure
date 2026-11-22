using System.Collections.Generic;
using UnityEngine;

public class EnemyPathFollow : MonoBehaviour
{
    [Tooltip("The places the enemy goes to, in order, and then round again. Drag in objects, placed where it should go.")]
    public List<GameObject> destinations = new List<GameObject>();

    [Tooltip("The destination to head to first. Leave empty to start with the first one in the list.")]
    public GameObject whichObjectFirst;

    [Tooltip("Visit the list from last to first instead of first to last.")]
    public bool reversePath;

    [Tooltip("Off: glide at a constant speed straight from point to point. On: use physics - speed up toward each point, brake to arrive at it, then curve toward the next one using momentum (needs a Rigidbody2D with Gravity Scale 0).")]
    public bool useMomentum;

    [Tooltip("Top speed, in units per second.")]
    public float velocity = 2f;

    [Tooltip("Units per second squared. Only used when Use Momentum is on.")]
    public float acceleration = 5f;

    [Tooltip("Units per second squared. How hard it brakes to arrive at a destination, set independently of Acceleration. 0 means no braking - it flies through each point at full speed. Only used when Use Momentum is on.")]
    public float deceleration = 5f;

    [Tooltip("How close the enemy has to get to a destination before it heads for the next one.")]
    public float distanceFromTargetToMoveToNext = 0.1f;

    [Tooltip("Seconds to wait on arriving at a destination. A Path Destination component on a destination overrides this for that one.")]
    public float pauseAtDestination;

    [Header("Obstacle avoidance")]
    [Tooltip("Plan a route around tiles that block the straight line to the next destination.")]
    public bool avoidObstacles = true;

    [Tooltip("What counts as an obstacle. Leave empty to use the Ground layer.")]
    public LayerMask obstacleLayers;

    [Tooltip("Size of the enemy when squeezing past obstacles. 0 measures it from the enemy's colliders.")]
    public float agentRadius;

    [Tooltip("Resolution of the route search, in world units. Smaller fits through tighter gaps but searches slower.")]
    public float pathCellSize = 0.5f;

    [Tooltip("Most cells one search will look at. If the destination can't be reached within that, the enemy heads for the closest point it found, and skips the destination if it can't get any closer.")]
    public int maxSearchNodes = 4000;

    [Tooltip("Seconds between checks that the route ahead is still clear.")]
    public float repathInterval = 0.25f;

    [Tooltip("Stick to the straight line toward the destination and only swing around an obstacle once it's close, then come back onto that line just past it. Off: a route around an obstacle is planned as soon as one is anywhere in the way, even far down the line.")]
    public bool stayOnOriginalPath;

    [Tooltip("Extra room the enemy gives obstacles so it can swing round them in a smooth curve instead of turning on a point. Bigger gives wider, gentler curves. 0 turns the smoothing off and takes the tightest route.")]
    public float smoothTurns = 0.5f;

    [Tooltip("How far ahead the enemy looks for obstacles when Stay On Original Path is on. Smaller means it holds its line longer and turns later, so give it enough room to steer (more if Use Momentum is on).")]
    public float avoidLookahead = 2f;

    [Space]
    [Tooltip("The place in the list that it's heading for now, counting from 0. Changes as it goes.")]
    public int currentDestination;

    // Plan to brake a bit gentler than the enemy actually can, so the gaps between physics steps never leave it
    // carrying too much speed to stop in time.
    private const float BrakingSafety = 0.8f;

    private Rigidbody2D rb;
    private float pauseTimeRemaining;

    // Until it has been to a destination there's nothing behind it to turn back to.
    private bool hasReachedDestination;
    private bool wasReversing;

    // The waypoints leading around obstacles to routeDestination; empty means there's a clear straight line.
    private readonly List<Vector2> route = new List<Vector2>();
    private int routeIndex;
    private Vector2 routeStart;
    private GameObject routeDestination;
    private Vector2 routeTarget;
    private float repathTimer;
    private float planningRadius;

    // Destinations are usually children of the enemy so they stay organised with it, which means they would
    // normally travel along with the enemy and never be reachable. A destination's world position is recorded
    // the moment it joins the path (Start, AddDestination, SetNewPath) and used as the real target; LateUpdate
    // then pins the object back to that spot.
    private Dictionary<GameObject, Vector3> destinationPositions = new Dictionary<GameObject, Vector3>();

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        wasReversing = reversePath;

        if (obstacleLayers.value == 0)
        {
            obstacleLayers = LayerMask.GetMask("Ground");
        }

        planningRadius = MeasurePlanningRadius();

        PurgeDestroyed();

        foreach (var destination in destinations)
        {
            RecordedPositionOf(destination);
        }

        if (destinations.Count == 0)
        {
            print($"{name}: EnemyPathFollow has no destinations yet.");
        }

        if (useMomentum && !rb)
        {
            print($"{name}: Use Momentum needs a Rigidbody2D - falling back to straight-line movement.");
        }

        var firstIndex = whichObjectFirst ? destinations.IndexOf(whichObjectFirst) : 0;
        currentDestination = Mathf.Max(firstIndex, 0);
    }

    void FixedUpdate()
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

        if (pauseTimeRemaining > 0f)
        {
            pauseTimeRemaining -= Time.fixedDeltaTime;
            HoldStill();
            return;
        }

        var destination = destinations[currentDestination];
        var speed = SpeedTowards(destination);

        Vector2 position = transform.position;
        Vector2 target = RecordedPositionOf(destination);

        // Never smaller than one step, or a fast enemy could skip right over the arrival zone.
        var arrivalDistance = Mathf.Max(distanceFromTargetToMoveToNext, speed * Time.fixedDeltaTime);

        if (Vector2.Distance(position, target) <= arrivalDistance)
        {
            hasReachedDestination = true;
            pauseTimeRemaining = PauseAt(destination);

            Advance();
            destination = destinations[currentDestination];
            target = RecordedPositionOf(destination);
            speed = SpeedTowards(destination);

            if (pauseTimeRemaining > 0f)
            {
                HoldStill();
                return;
            }
        }

        // Where to actually steer: the destination itself, or the next waypoint of a route around obstacles.
        var steer = target;

        if (avoidObstacles && !TryGetSteeringTarget(destination, position, target, speed, out steer))
        {
            // Walled off with no way closer, so move on rather than pushing against the wall forever.
            Advance();
            return;
        }

        if (useMomentum && rb)
        {
            // The fastest it can be going and still stop in time at the destination. Measured along the route
            // rather than in a straight line, so a detour around a wall doesn't make it brake too early.
            var brakingSpeed = deceleration > 0f
                ? Mathf.Sqrt(2f * deceleration * BrakingSafety * DistanceRemaining(position, target))
                : float.MaxValue;

            var desiredVelocity = (steer - position).normalized * Mathf.Min(speed, brakingSpeed);

            // Steer toward the velocity it should have, as hard as its acceleration (or braking) allows.
            var neededAcceleration = (desiredVelocity - rb.linearVelocity) / Time.fixedDeltaTime;
            var isBraking = desiredVelocity.sqrMagnitude < rb.linearVelocity.sqrMagnitude;

            rb.AddForce(Vector2.ClampMagnitude(neededAcceleration, isBraking ? deceleration : acceleration) * rb.mass);
        }
        else
        {
            var next = Vector2.MoveTowards(position, steer, speed * Time.fixedDeltaTime);

            if (rb)
            {
                rb.MovePosition(next);
            }
            else
            {
                transform.position = new Vector3(next.x, next.y, transform.position.z);
            }
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
        return true;
    }

    // Returns false if the destination can't be reached and the search found nowhere closer to go either.
    private bool TryGetSteeringTarget(GameObject destination, Vector2 position, Vector2 target, float speed, out Vector2 steer)
    {
        // A different destination, or one that has been moved, makes the old route useless.
        if (destination != routeDestination || target != routeTarget)
        {
            ClearRoute();
            routeDestination = destination;
            routeTarget = target;
            repathTimer = 0f;
        }

        repathTimer -= Time.fixedDeltaTime;

        var reach = Mathf.Max(pathCellSize * 0.5f, speed * Time.fixedDeltaTime);

        while (routeIndex < route.Count && HasPassedWaypoint(position, reach))
        {
            routeIndex++;
        }

        // Out of waypoints: either the straight line to the destination is clear now, or it's time to plan again.
        if (route.Count > 0 && routeIndex >= route.Count)
        {
            ClearRoute();
            repathTimer = 0f;
        }

        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;

            bool needsPlan;

            if (route.Count > 0)
            {
                // Mid-detour. Normally the detour is dropped the moment a shortcut to the destination opens up;
                // with Stay On Original Path it's followed all the way back onto the line instead.
                // Only with room to spare: cutting across the moment there's a bare minimum of clearance would turn
                // sharply at exactly the point the curve was meant to be carrying it round.
                if (!stayOnOriginalPath &&
                    PathFinder2D.HasClearPath(position, target, planningRadius + Mathf.Max(smoothTurns, 0f), obstacleLayers))
                {
                    ClearRoute();
                    needsPlan = false;
                }
                else
                {
                    needsPlan = !PathFinder2D.HasClearPath(position, route[routeIndex], planningRadius, obstacleLayers);
                }
            }
            else
            {
                needsPlan = !IsStraightPathClear(position, target);
            }

            if (needsPlan)
            {
                var path = PlanRoute(position, target);

                ClearRoute();
                route.AddRange(path);
                routeStart = position;

                if (route.Count == 0)
                {
                    steer = target;
                    return false;
                }
            }
        }

        steer = route.Count > 0 ? route[routeIndex] : target;
        return true;
    }

    // With Stay On Original Path only the stretch just ahead matters; anything further down the line can wait.
    private bool IsStraightPathClear(Vector2 position, Vector2 target)
    {
        if (stayOnOriginalPath)
        {
            target = Vector2.MoveTowards(position, target, avoidLookahead);
        }

        return PathFinder2D.HasClearPath(position, target, planningRadius, obstacleLayers);
    }

    private List<Vector2> PlanRoute(Vector2 position, Vector2 target)
    {
        var room = Mathf.Max(smoothTurns, 0f);

        if (room <= 0f)
        {
            return FindRoute(position, target, planningRadius, out _);
        }

        // Plan with room to spare, because a curve cuts in toward the inside of the corner it replaces. If there's no
        // way through with that much room, squeeze through at the enemy's real size and round whatever turns still fit.
        var roomy = FindRoute(position, target, planningRadius + room, out var reached);
        var route = reached ? roomy : FindRoute(position, target, planningRadius, out _);

        return PathFinder2D.RoundCorners(position, route, room, planningRadius, obstacleLayers);
    }

    private List<Vector2> FindRoute(Vector2 position, Vector2 target, float radius, out bool reachedGoal)
    {
        var cellSize = Mathf.Max(pathCellSize, 0.1f);

        // Stay On Original Path: detour only around what's in the way and rejoin the line just past it. If that
        // spot can't be reached, fall back to planning all the way to the destination.
        if (stayOnOriginalPath
            && PathFinder2D.TryFindRejoinPoint(position, target, radius, obstacleLayers, cellSize * 0.5f, out var rejoin))
        {
            var detour = PathFinder2D.FindPath(
                position, rejoin, radius, obstacleLayers, cellSize, maxSearchNodes, out var reachedRejoin);

            if (reachedRejoin)
            {
                reachedGoal = true;
                return detour;
            }
        }

        return PathFinder2D.FindPath(position, target, radius, obstacleLayers, cellSize, maxSearchNodes, out reachedGoal);
    }

    // True if the enemy has reached the current waypoint, or flown past it without getting close (easy to do with
    // momentum). Either way it's already further along the route, so there's no point turning back for it.
    private bool HasPassedWaypoint(Vector2 position, float reach)
    {
        var waypoint = route[routeIndex];

        if (Vector2.Distance(position, waypoint) <= reach)
        {
            return true;
        }

        // Past the waypoint means beyond the line through it that's square-on to the leg of the route leading to it.
        var legStart = routeIndex == 0 ? routeStart : route[routeIndex - 1];

        return Vector2.Dot(position - waypoint, waypoint - legStart) > 0f;
    }

    private void ClearRoute()
    {
        route.Clear();
        routeIndex = 0;
    }

    private float MeasurePlanningRadius()
    {
        var radius = agentRadius;

        if (radius <= 0f)
        {
            foreach (var col in GetComponents<Collider2D>())
            {
                var extents = col.bounds.extents;
                radius = Mathf.Max(radius, extents.x, extents.y);
            }
        }

        // A little breathing room so the enemy doesn't scrape along walls.
        return radius + 0.05f;
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

    // Straight-line movement stops by simply not moving; momentum movement has to brake to a halt.
    private void HoldStill()
    {
        if (!useMomentum || !rb)
        {
            return;
        }

        if (deceleration > 0f)
        {
            var neededAcceleration = -rb.linearVelocity / Time.fixedDeltaTime;
            rb.AddForce(Vector2.ClampMagnitude(neededAcceleration, deceleration) * rb.mass);
        }
        else
        {
            // No braking power, but a pause still has to hold still.
            rb.linearVelocity = Vector2.zero;
        }
    }

    // How far the enemy still has to travel to reach the destination, following the route if it's on one.
    private float DistanceRemaining(Vector2 position, Vector2 target)
    {
        if (route.Count == 0)
        {
            return Vector2.Distance(position, target);
        }

        var distance = Vector2.Distance(position, route[routeIndex]);

        for (var i = routeIndex; i < route.Count - 1; i++)
        {
            distance += Vector2.Distance(route[i], route[i + 1]);
        }

        return distance + Vector2.Distance(route[route.Count - 1], target);
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

        Gizmos.color = Color.yellow;
        Vector3 routeFrom = transform.position;

        for (var i = routeIndex; i < route.Count; i++)
        {
            Gizmos.DrawLine(routeFrom, route[i]);
            routeFrom = route[i];
        }
    }
}
