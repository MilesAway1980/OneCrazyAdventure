using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameMath: MonoBehaviour
{
    public static float GetAngle(Vector2 from, Vector2 to)
    {
        return GetAngle(from.x, from.y, to.x, to.y);
    }
    
    public static float GetAngle(float x1, float y1, float x2, float y2)
    {
        if (x1 == x2 && y1 == y2)
        {
            print($"GameMath.GetAngle - Start and End Points are the same.  ({x1}, {y1})");
            return 0;
        }

        float angle = Mathf.Rad2Deg * Mathf.Atan2(
            x1 - x2, 
            y1 - y2
        );

        angle -= 180;

        while (angle < 0 || angle >= 360)
        {
            if (angle < 0)
            {
                angle += 360;
            }

            if (angle >= 360)
            {
                angle -= 360;
            }
        }

        return angle;
    }

    public static Vector2 GetVectorFromAngle(float angle, float distance = 1.0f)
    {
        return MoveTowardsAngle(Vector2.zero, angle, distance);
    }

    public static Vector3 MoveTowardsAngle(Vector3 currentPosition, float angle, float distance)
    {
        angle -= 90;        //This re-orients 0° to UP
        return new Vector3(
            currentPosition.x + Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
            currentPosition.y - Mathf.Sin(angle * Mathf.Deg2Rad) * distance,
            currentPosition.z
        );
    }
    
    public static Vector2 MoveTowardsAngle(Vector2 currentPosition, float angle, float distance)
    {
        angle -= 90;        //This re-orients 0° to UP
        return new Vector2(
            currentPosition.x + Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
            currentPosition.y - Mathf.Sin(angle * Mathf.Deg2Rad) * distance
        );
    }
    
    public static Vector3 MoveTowardsTarget(Vector3 currentPosition, Vector3 target, float distance)
    {
        var angle = GetAngle(currentPosition, target);
        return MoveTowardsAngle(currentPosition, angle, distance);
    }

    public static Vector3 FindCenter(IEnumerable<Vector3> positions)
    {
        float x = 0;
        float y = 0;
        float z = 0;
        
        if (positions == null)
        {
            return Vector3.zero;
        }
        
        var total = positions.Count();

        if (total == 0)
        {
            return Vector3.zero;
        }

        if (total == 1)
        {
            return positions.First();
        }

        foreach (var pos in positions)
        {
            x += pos.x;
            y += pos.y;
            z += pos.z;
        }

        float inverse = 1.0f / total;
        
        return new Vector3(x *  inverse, y * inverse, z * inverse);
    }
    
    public static Vector2 FindCenter(IEnumerable<Vector2> positions)
    {
        float x = 0;
        float y = 0;
        
        if (positions == null)
        {
            return Vector2.zero;
        }
        
        var total = positions.Count();

        if (total == 0)
        {
            return Vector2.zero;
        }

        if (total == 1)
        {
            return positions.First();
        }

        foreach (var pos in positions)
        {
            x += pos.x;
            y += pos.y;
        }

        float inverse = 1.0f / total;
        
        return new Vector2(x *  inverse, y * inverse);
    }

    public static Vector2 GetMagnitudeFromAngle(float angle, float magnification = 1.0f)
    {
        var newAngle = angle * Mathf.Deg2Rad;
        var normal = new Vector2(
            Mathf.Sin(newAngle),
            Mathf.Cos(newAngle)
        ).normalized;

        return normal * magnification;
    }
    
    public static void FixAngle(ref float angle)
    {
        while (angle >= 360)
        {
            angle -= 360;
        }
        
        while (angle < 0)
        {
            angle += 360;
        }
    }

    public static float FixAngle(float angle)
    {
        FixAngle(ref angle);
        
        return angle;
    }
}
