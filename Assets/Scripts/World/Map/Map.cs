using System;
using System.Collections.Generic;
using System.Linq;
//using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Map : MonoBehaviour
{
    public static int numMaps;
    private static List<Tilemap> tilemaps;
    private static List<Tilemap> destructibleTilemaps;
    private static Dictionary<Tilemap, LiquidMap> liquidMaps;
    private static bool initialized = false;
    private static Map mapInstance;

    void Start()
    {
        if (tilemaps == null)
        {
            tilemaps = FindObjectsByType<Tilemap>().ToList();
            destructibleTilemaps = new List<Tilemap>();

            foreach (var tilemap in tilemaps)
            {
                var collider2D = tilemap.GetComponent<Collider2D>();

                if (collider2D)
                {
                    destructibleTilemaps.Add(tilemap);
                }
            }
        }

        liquidMaps = new Dictionary<Tilemap, LiquidMap>();
        foreach (var tilemap in tilemaps)
        {
            var liquidMap = tilemap.GetComponent<LiquidMap>();
            liquidMaps.Add(tilemap, liquidMap);
        }

        numMaps = tilemaps.Count;
        mapInstance = this;
        initialized = true;
    }

    /*
    void Update()
    {
        
    }
    */

    public static bool IsInitialized()
    {
        return initialized;
    }

    // Tiles are talked about everywhere in the game by where they are in the world: the whole-number position of the corner of
    // the square a tile covers. That doesn't depend on where any tilemap is put. A tilemap numbers its own cells from wherever
    // it and its grid are, so this is the one place that turns a world position into the cell on a particular tilemap. Anything
    // that asks a tilemap about a tile by cell has to come through here.
    public static Vector3Int ToCell(Tilemap tilemap, int x, int y)
    {
        // The middle of the square, so a tilemap that is a little off the grid still gives the cell the square is mostly over.
        var cell = tilemap.WorldToCell(new Vector3(x + 0.5f, y + 0.5f, tilemap.transform.position.z));
        cell.z = 0;

        return cell;
    }

    public static Vector3Int ToCell(Tilemap tilemap, Vector2Int position)
    {
        return ToCell(tilemap, position.x, position.y);
    }

    private static readonly RaycastHit2D[] rayHits = new RaycastHit2D[16];

    // The nearest thing on the Ground layer along a ray, or a hit with no collider if there is nothing. With
    // ignorePushable, anything marked PushableSolid is looked straight through, so it's the next thing that really
    // blocks the way. With ignoreBody, so is anything attached to that body: a character that has joined the Ground
    // layer, such as a solid Brick, would otherwise find himself with every ray that starts inside him. alsoIgnoreBody is one
    // more body to look through: what that character is carrying, which is held out in front of him.
    private static RaycastHit2D NearestGround(
        Vector3 point, Vector2 direction, float distance, bool ignorePushable, Rigidbody2D ignoreBody,
        Rigidbody2D alsoIgnoreBody = null)
    {
        if (!ignorePushable && !ignoreBody && !alsoIgnoreBody)
        {
            return Physics2D.Raycast(point, direction, distance, LayerMask.GetMask("Ground"));
        }

        var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
        filter.SetLayerMask(LayerMask.GetMask("Ground"));

        var count = Physics2D.Raycast(point, direction, filter, rayHits, distance);
        var nearest = new RaycastHit2D();

        for (int i = 0; i < count; i++)
        {
            var hit = rayHits[i];

            if (ignoreBody && hit.collider.attachedRigidbody == ignoreBody)
            {
                continue;
            }

            if (alsoIgnoreBody && hit.collider.attachedRigidbody == alsoIgnoreBody)
            {
                continue;
            }

            if (ignorePushable)
            {
                var pushable = hit.collider.GetComponentInParent<PushableSolid>();

                if (pushable && pushable.enabled)
                {
                    continue;
                }
            }

            if (!nearest.collider || hit.distance < nearest.distance)
            {
                nearest = hit;
            }
        }

        return nearest;
    }

    public static float? DistanceFromDirection(
        Vector3 point, Vector2 direction, float distance = 20, bool ignorePushable = false, Rigidbody2D ignoreBody = null,
        Rigidbody2D alsoIgnoreBody = null)
    {
        var tile = NearestGround(point, direction, distance, ignorePushable, ignoreBody, alsoIgnoreBody);

        if (!tile.collider)
        {
            return null;
        }

        return tile.distance;
    }

    public static GameObject GetGameObjectInDirection(Vector3 point, Vector2 direction, float distance = 20)
    {
        var obj = Physics2D.Raycast(point, direction, distance, LayerMask.GetMask("Ground"));
        if (!obj.collider)
        {
            return null;
        }
        
        return obj.collider.gameObject;
    }

    /*
    public Vector2Int? GetTileInDirection(Vector3 point, float angle, float distance = 20)
    {
        turn angle into a vector    
    }
    */

    public static TileDirection GetTilePositionInDirection(
        Vector3 point, Vector2 direction, float distance = 20, Rigidbody2D ignoreBody = null)
    {
        var hit = NearestGround(point, direction, distance, false, ignoreBody);
        if (!hit.collider)
        {
            return new TileDirection();
        }

        //The point is outside of the box it's hitting.  Physics2D collision points aren't inside the box
        //they're slightly outside.  For example, hitting a box at -11, -5 from the left side will show up
        //as -11.000003, -4.5873.  From the other direction will be 10.999995 for 11, etc.
        
        //We're dealing with floating point errors here, so turning them into integers helps to remove the discrepancy
        
        long hitX = (long)(hit.point.x * 10000.0f);
        long hitY = (long)(hit.point.y * 10000.0f);

        long diffX = Math.Abs(hitX % 10000);
        long diffY = Math.Abs(hitY % 10000);

        var angle = GameMath.GetAngle(new Vector2(point.x, point.y), new Vector2(point.x + direction.x, point.y + direction.y));

        //var up = false;
        var down = false;
        var left = false;
        //var right = false;

        /*if (angle == 0)
        {
            up = true;
        }
        else if (angle == 90)
        {
            right = true;
        }
        else */if (angle == 180)
        {
            down = true;
        }
        else if (angle == 270)
        {
            left = true;
        }
        else
        {
            //up = (angle < 90 && angle > 0) || (angle < 360 && angle > 270);
            down = angle > 90 && angle < 270;
            left = angle > 180 && angle < 360;
            //right = angle > 0 && angle < 180;
        }

        Vector2Int tileHit = new Vector2Int();

        if (diffX <= 1 || diffX >= 9998)
        {
            //Hitting right on the edge.
            
            // Add 1000 and then round.  This makes edge cases be the same.
            // 29999 + 1000 = 31999  (3.1999)
            // 30000 + 1000 = 32000  (3.2000)
            // Both round to 3
            
            tileHit.x = Mathf.RoundToInt((hitX + 1000) * 0.0001f);

            if (left)
            {
                tileHit.x -= 1;
            }
        }
        else
        {
            tileHit.x = Mathf.FloorToInt(hit.point.x);
        }
        
        if (diffY <= 1 || diffY >= 9998)
        {
            tileHit.y = Mathf.RoundToInt((hitY + 1000) * 0.0001f);

            if (down)
            {
                tileHit.y -= 1;
            }
        }
        else
        {
            tileHit.y = Mathf.FloorToInt(hit.point.y);
        }

        return new TileDirection(tileHit, hit.distance, angle);
    }

    public static Vector2? GetCollisionPointInDirection(Vector3 point, Vector2 direction, float distance = 20)
    {
        var hit = Physics2D.Raycast(point, direction, distance, LayerMask.GetMask("Ground"));
        if (!hit.collider)
        {
            return null;
        }

        return hit.point;
    }

    // The liquid at a point, or null if it's in the air.
    public static Fluid FluidAt(Vector3 point)
    {
        Fluid found = null;

        foreach (var tilemap in tilemaps)
        {
            var liquid = liquidMaps[tilemap];
            if (liquid && tilemap.GetTile(tilemap.WorldToCell(point)))
            {
                found = liquid.Fluid;
            }
        }

        return found;
    }

    private static readonly List<Vector2> shapePoints = new List<Vector2>();

    // Whether a point is inside the solid part of a tile on a tilemap that collides. The colliders themselves can't say:
    // they are just the outline of the ground, so something deep in the ground touches no edge and overlaps nothing.
    public static bool IsInsideSolid(Vector2 point)
    {
        foreach (var tilemap in destructibleTilemaps)
        {
            var cell = tilemap.WorldToCell(point);
            var colliderType = tilemap.GetColliderType(cell);

            if (colliderType == Tile.ColliderType.None)
            {
                continue;
            }

            if (colliderType == Tile.ColliderType.Grid)
            {
                return true;
            }

            var sprite = tilemap.GetSprite(cell);

            if (!sprite)
            {
                continue;
            }

            // Relative to where the sprite's pivot sits in the cell, undoing any flip or rotation the tile was given.
            var anchor = tilemap.CellToWorld(cell) + tilemap.tileAnchor;
            var local = tilemap.GetTransformMatrix(cell).inverse.MultiplyPoint3x4(new Vector3(point.x, point.y) - anchor);

            for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
            {
                sprite.GetPhysicsShape(i, shapePoints);

                if (IsInsidePolygon(local, shapePoints))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInsidePolygon(Vector2 point, List<Vector2> polygon)
    {
        var inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static Vector3Int TilePositionFromWorldPoint(Vector3 worldPoint)
    {
        return TilePositionFromWorldPoint(worldPoint.x, worldPoint.y);
    }

    public static Vector3Int TilePositionFromWorldPoint(Vector2 worldPoint)
    {
        return TilePositionFromWorldPoint(worldPoint.x, worldPoint.y);
    }
    
    public static Vector3Int TilePositionFromWorldPoint(float x, float y)
    {
        // Where it is in the world, not a cell on any one tilemap: see ToCell for that.
        return new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 0);
    }
    
    public static bool DestroyTile(Vector2Int pos)
    {
        return DestroyTile(pos.x, pos.y);
    }
    
    public static bool DestroyTile(int x, int y)
    {
        foreach (var map in destructibleTilemaps)
        {
            map.SetTile(ToCell(map, x, y), null);
        }

        return true;
    }

    public static void CreateTile(Vector2Int tilePosition, List<(Tilemap, TileBase)> tiles)
    {
        CreateTile(tilePosition.x, tilePosition.y, tiles);
    }

    // Raised just before a tile comes back, so that anything standing in its space can be moved out first.
    public static event Action<Vector2Int> TileCreating;

    // Says that a tile is about to become solid where it is, without it being placed, for a tile that is already there and
    // is only about to start colliding. Anything standing in its space is moved out first, as it would be for a new tile.
    public static void AnnounceSolidTile(Vector2Int tilePosition)
    {
        TileCreating?.Invoke(tilePosition);
    }

    // Whether a tile that collides is in a space, on any of the tilemaps that collide. A tile whose collider has been
    // switched off doesn't count.
    public static bool HasSolidTile(int x, int y)
    {
        if (destructibleTilemaps == null)
        {
            return false;
        }

        foreach (var tilemap in destructibleTilemaps)
        {
            var cell = ToCell(tilemap, x, y);

            if (tilemap.HasTile(cell) && tilemap.GetColliderType(cell) != Tile.ColliderType.None)
            {
                return true;
            }
        }

        return false;
    }

    public static void CreateTile(int x, int y, List<(Tilemap, TileBase)> tiles)
    {
        TileCreating?.Invoke(new Vector2Int(x, y));

        foreach (var tile in tiles)
        {
            var tilemap = tile.Item1;
            var tilebase = tile.Item2;
            
            tilemap.SetTile(ToCell(tilemap, x, y), tilebase);
        }
    }

    public static List<(Tilemap, TileBase)> GetTiles(Vector2Int pos, bool destructibleOnly)
    {
        return GetTiles(pos.x, pos.y, destructibleOnly);
    }

    public static List<(Tilemap, TileBase)> GetTiles(int x, int y, bool destructibleOnly)
    {
        var tiles = new List<(Tilemap, TileBase)>();

        List<Tilemap> mapList;
        if (destructibleOnly)
        {
            mapList = destructibleTilemaps;
        }
        else
        {
            mapList = tilemaps;
        }
        
        foreach (var map in mapList)
        {
            var tile = map.GetTile(ToCell(map, x, y));
            if (tile)
            {
                tiles.Add(
                    (map, tile)
                );
            }
        }

        return tiles;
    }
}
