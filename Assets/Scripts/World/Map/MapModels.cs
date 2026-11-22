using System;
using UnityEngine;

// What a ray found in one direction. When it hit nothing, hasHit is false and the other values mean nothing, so check
// hasHit before using them. A struct, so it can be shown in the inspector and doesn't make garbage every frame.
[Serializable]
public struct TileDirection
{
    [Tooltip("Whether it found a tile in this direction.")]
    public bool hasHit;
    [Tooltip("The cell of the tile it found.")]
    public Vector2Int position;
    [Tooltip("How far away, in units, the tile it found is.")]
    public float distance;
    [Tooltip("The angle, in degrees, that it looked along.")]
    public float angle;

    public TileDirection(Vector2Int tilePosition, float tileDistance, float tileAngle)
    {
        hasHit = true;
        position = tilePosition;
        distance = tileDistance;
        angle = tileAngle;
    }
}
