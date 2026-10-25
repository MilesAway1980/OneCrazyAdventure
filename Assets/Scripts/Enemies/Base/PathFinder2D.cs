using System.Collections.Generic;
using UnityEngine;

// Finds routes through open space for a circle of a given size, treating anything on the obstacle layers as solid.
// Cells are checked with the physics engine as the search reaches them rather than baked ahead of time, so routes
// always reflect the map as it is right now (destroyed or moved tiles included) and any collider shape works.
public static class PathFinder2D
{
    private const float Diagonal = 1.41421356f;

    // Turns gentler than this (about 6 degrees) aren't worth rounding.
    private const float MinRoundedTurn = 0.1f;

    // Curves too tight to be worth the extra waypoints are left as plain corners.
    private const float MinCurveRadius = 0.05f;

    // A curve is drawn as straight pieces turning by about this much each.
    private const float CurveStepDegrees = 20f;

    private static readonly Vector2Int[] Steps =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    public static bool HasClearPath(Vector2 from, Vector2 to, float radius, LayerMask obstacles)
    {
        var offset = to - from;
        var distance = offset.magnitude;

        if (distance < 0.0001f)
        {
            return true;
        }

        return !Physics2D.CircleCast(from, radius, offset / distance, distance, obstacles);
    }

    // Walks the straight line from `from` to `to` past the first obstacle on it and finds the first spot beyond
    // where the circle fits with a little room to keep going - the point a detour should rejoin the line at.
    // Returns false if the line is clear all the way, or never opens up again before `to`.
    public static bool TryFindRejoinPoint(
        Vector2 from, Vector2 to, float radius, LayerMask obstacles, float step, out Vector2 rejoin)
    {
        rejoin = to;

        var offset = to - from;
        var distance = offset.magnitude;

        if (distance < 0.0001f)
        {
            return false;
        }

        var direction = offset / distance;
        var hit = Physics2D.CircleCast(from, radius, direction, distance, obstacles);

        if (!hit)
        {
            return false;
        }

        for (var travelled = hit.distance + step; travelled < distance; travelled += step)
        {
            var point = from + direction * travelled;

            if (Physics2D.OverlapCircle(point, radius, obstacles))
            {
                continue;
            }

            // Needs room ahead as well, or the "gap" is just a sliver the enemy couldn't really pass through.
            var roomAhead = Mathf.Min(step * 2f, distance - travelled);

            if (HasClearPath(point, point + direction * roomAhead, radius, obstacles))
            {
                rejoin = point;
                return true;
            }
        }

        return false;
    }

    // Returns the waypoints to follow, not including start. If the goal can't be reached within maxNodes cells
    // (or can't be reached at all) the route leads to the closest point found instead and reachedGoal is false;
    // an empty list then means there was nowhere closer to go.
    public static List<Vector2> FindPath(
        Vector2 start, Vector2 goal, float radius, LayerMask obstacles, float cellSize, int maxNodes, out bool reachedGoal)
    {
        var startCell = ToCell(start, cellSize);
        var goalCell = ToCell(goal, cellSize);

        var blockedCache = new Dictionary<Vector2Int, bool>();

        // The enemy is already standing in its start cell and the destination was put where it was on purpose,
        // so neither is ever treated as blocked even if a cell centre happens to clip an obstacle.
        bool IsBlocked(Vector2Int cell)
        {
            if (cell == startCell || cell == goalCell)
            {
                return false;
            }

            if (!blockedCache.TryGetValue(cell, out var blocked))
            {
                blocked = Physics2D.OverlapCircle(ToWorld(cell, cellSize), radius, obstacles) != null;
                blockedCache[cell] = blocked;
            }

            return blocked;
        }

        var cheapestCost = new Dictionary<Vector2Int, float> { [startCell] = 0f };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();

        // A cell can be queued more than once as cheaper routes to it turn up; the stale entries are skipped.
        var open = new SortedSet<(float score, int id)>();
        var cellById = new List<Vector2Int>();

        void Queue(Vector2Int cell, float score)
        {
            cellById.Add(cell);
            open.Add((score, cellById.Count - 1));
        }

        var best = startCell;
        var bestRemaining = Heuristic(startCell, goalCell);
        Queue(startCell, bestRemaining);

        var expanded = 0;
        var found = false;

        while (open.Count > 0 && expanded < maxNodes)
        {
            var entry = open.Min;
            open.Remove(entry);

            var current = cellById[entry.id];

            if (!closed.Add(current))
            {
                continue;
            }

            if (current == goalCell)
            {
                found = true;
                break;
            }

            expanded++;

            var remaining = Heuristic(current, goalCell);

            if (remaining < bestRemaining)
            {
                bestRemaining = remaining;
                best = current;
            }

            foreach (var step in Steps)
            {
                var next = current + step;

                if (closed.Contains(next) || IsBlocked(next))
                {
                    continue;
                }

                var isDiagonal = step.x != 0 && step.y != 0;

                // Don't squeeze diagonally past the corner of an obstacle.
                if (isDiagonal && (IsBlocked(current + new Vector2Int(step.x, 0)) || IsBlocked(current + new Vector2Int(0, step.y))))
                {
                    continue;
                }

                var cost = cheapestCost[current] + (isDiagonal ? Diagonal : 1f);

                if (cheapestCost.TryGetValue(next, out var existing) && cost >= existing)
                {
                    continue;
                }

                cheapestCost[next] = cost;
                cameFrom[next] = current;
                Queue(next, cost + Heuristic(next, goalCell));
            }
        }

        reachedGoal = found;

        var cells = new List<Vector2Int>();

        for (var cell = found ? goalCell : best; cell != startCell; cell = cameFrom[cell])
        {
            cells.Add(cell);
        }

        cells.Reverse();

        var points = new List<Vector2> { start };

        foreach (var cell in cells)
        {
            points.Add(ToWorld(cell, cellSize));
        }

        if (found)
        {
            points.Add(goal);
        }

        return Smooth(points, radius, obstacles);
    }

    // Replaces each sharp turn in a route with a curve, so the enemy sweeps round obstacles instead of turning on a
    // point. A curve cuts in toward the inside of the turn it replaces, so `room` is how much it's allowed to cut:
    // the route should have been planned with at least that much clearance to spare. Each curve is still checked
    // against the obstacles, and a turn is left sharp if its curve wouldn't fit. Waypoints are the route after
    // `start`, and the last one stays exactly where it is.
    public static List<Vector2> RoundCorners(
        Vector2 start, List<Vector2> waypoints, float room, float radius, LayerMask obstacles)
    {
        if (waypoints.Count == 0)
        {
            return waypoints;
        }

        var points = new List<Vector2>(waypoints.Count + 1) { start };
        points.AddRange(waypoints);

        var result = new List<Vector2>(points.Count * 2);

        for (var i = 1; i < points.Count - 1; i++)
        {
            AddRoundedCorner(result, points[i - 1], points[i], points[i + 1], room, radius, obstacles);
        }

        result.Add(points[points.Count - 1]);
        return result;
    }

    private static void AddRoundedCorner(
        List<Vector2> result, Vector2 before, Vector2 corner, Vector2 after, float room, float radius, LayerMask obstacles)
    {
        var incoming = corner - before;
        var outgoing = after - corner;
        var incomingLength = incoming.magnitude;
        var outgoingLength = outgoing.magnitude;

        if (incomingLength < 0.01f || outgoingLength < 0.01f)
        {
            result.Add(corner);
            return;
        }

        var directionIn = incoming / incomingLength;
        var directionOut = outgoing / outgoingLength;
        var turn = Vector2.Angle(directionIn, directionOut) * Mathf.Deg2Rad;

        if (turn < MinRoundedTurn)
        {
            result.Add(corner);
            return;
        }

        // A curve of radius r cuts in toward the inside of the turn by r * (1 / cos(turn / 2) - 1). Keep that within
        // the room available, and its start and end within the first half of each side so that the curves of
        // neighbouring corners can never overlap.
        var cut = 1f / Mathf.Cos(turn * 0.5f) - 1f;
        var curveRadius = Mathf.Min(
            room * 0.9f / cut,
            Mathf.Min(incomingLength, outgoingLength) * 0.5f / Mathf.Tan(turn * 0.5f));

        if (curveRadius < MinCurveRadius)
        {
            result.Add(corner);
            return;
        }

        var tangent = curveRadius * Mathf.Tan(turn * 0.5f);
        var curveStart = corner - directionIn * tangent;
        var turnsLeft = Cross(directionIn, directionOut) > 0f;
        var sideways = turnsLeft ? new Vector2(-directionIn.y, directionIn.x) : new Vector2(directionIn.y, -directionIn.x);
        var center = curveStart + sideways * curveRadius;
        var spoke = curveStart - center;
        var sweep = (turnsLeft ? turn : -turn) * Mathf.Rad2Deg;

        var steps = Mathf.Max(1, Mathf.CeilToInt(turn * Mathf.Rad2Deg / CurveStepDegrees));
        var curve = new List<Vector2>(steps + 1);

        for (var step = 0; step <= steps; step++)
        {
            curve.Add(center + (Vector2)(Quaternion.Euler(0f, 0f, sweep * step / steps) * spoke));
        }

        for (var i = 1; i < curve.Count; i++)
        {
            if (!HasClearPath(curve[i - 1], curve[i], radius, obstacles))
            {
                result.Add(corner);
                return;
            }
        }

        result.AddRange(curve);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    // Drops every waypoint that can be skipped without bumping into anything, so the route doesn't zig-zag
    // along the grid.
    private static List<Vector2> Smooth(List<Vector2> points, float radius, LayerMask obstacles)
    {
        var result = new List<Vector2>();
        var from = 0;

        while (from < points.Count - 1)
        {
            var to = points.Count - 1;

            while (to > from + 1 && !HasClearPath(points[from], points[to], radius, obstacles))
            {
                to--;
            }

            result.Add(points[to]);
            from = to;
        }

        return result;
    }

    private static float Heuristic(Vector2Int from, Vector2Int to)
    {
        var dx = Mathf.Abs(from.x - to.x);
        var dy = Mathf.Abs(from.y - to.y);

        return Mathf.Max(dx, dy) + (Diagonal - 1f) * Mathf.Min(dx, dy);
    }

    private static Vector2Int ToCell(Vector2 point, float cellSize)
    {
        return new Vector2Int(Mathf.RoundToInt(point.x / cellSize), Mathf.RoundToInt(point.y / cellSize));
    }

    private static Vector2 ToWorld(Vector2Int cell, float cellSize)
    {
        return new Vector2(cell.x * cellSize, cell.y * cellSize);
    }
}
