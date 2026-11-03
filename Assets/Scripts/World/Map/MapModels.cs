using System;
using UnityEngine;

// What a ray found in one direction. When it hit nothing, hasHit is false and the other values mean nothing, so check
// hasHit before using them. A struct, so it can be shown in the inspector and doesn't make garbage every frame.
[Serializable]
public struct TileDirection
{
    public bool hasHit;
    public Vector2Int position;
    public float distance;
    public float angle;

    public TileDirection(Vector2Int tilePosition, float tileDistance, float tileAngle)
    {
        hasHit = true;
        position = tilePosition;
        distance = tileDistance;
        angle = tileAngle;
    }
}
