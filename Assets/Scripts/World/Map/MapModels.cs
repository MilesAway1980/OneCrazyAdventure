using UnityEngine;

public class TileDirection
{
    public Vector2Int? position;
    public float? distance;
    public float? angle;

    public TileDirection()
    {
        position = null;
        distance = null;
        angle = null;
    }

    public TileDirection(Vector2Int? tilePosition, float? tileDistance, float? tileAngle)
    {
        position = tilePosition;
        distance = tileDistance;
        angle = tileAngle;
    }
}