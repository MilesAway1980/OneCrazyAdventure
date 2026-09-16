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
            tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None).ToList();
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

    public static float? DistanceFromDirection(Vector3 point, Vector2 direction, float distance = 20)
    {
        var tile = Physics2D.Raycast(point, direction, distance, LayerMask.GetMask("Ground"));

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

    public static TileDirection GetTilePositionInDirection(Vector3 point, Vector2 direction, float distance = 20)
    {
        var hit = Physics2D.Raycast(point, direction, distance, LayerMask.GetMask("Ground"));
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

    public static (bool, float) IsInLiquid(Vector3 point)
    {
        var inWater = false;
        float density = 1;

        foreach (var tilemap in tilemaps)
        {
            var liquid = liquidMaps[tilemap];
            if (liquid)
            {
                var tilePos = TilePositionFromWorldPoint(point);
                var tileAtLocation = tilemap.GetTile(tilePos);
                if (tileAtLocation)
                {
                    inWater = true;
                    density = liquid.densityMultiplier;
                }
            }
        }

        return (inWater, density);
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
        var mapPosition = Vector3Int.zero;
        
        mapPosition = new Vector3Int((int)x, (int)y);
        
        if (x < 0)
        {
            mapPosition.x -= 1;
        }

        if (y < 0)
        {
            mapPosition.y -= 1;
        }

        return mapPosition;
    }
    
    public static bool DestroyTile(Vector2Int pos)
    {
        return DestroyTile(pos.x, pos.y);
    }
    
    public static bool DestroyTile(int x, int y)
    {
        foreach (var map in destructibleTilemaps)
        {
            map.SetTile(new Vector3Int(x, y, 0), null);
        }

        return true;
    }

    public static void CreateTile(Vector2Int tilePosition, List<(Tilemap, TileBase)> tiles)
    {
        CreateTile(tilePosition.x, tilePosition.y, tiles);
    }

    public static void CreateTile(int x, int y, List<(Tilemap, TileBase)> tiles)
    {
        foreach (var tile in tiles)
        {
            var tilemap = tile.Item1;
            var tilebase = tile.Item2;
            
            tilemap.SetTile(new Vector3Int(x, y, 0), tilebase);
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
            var tile = map.GetTile(new Vector3Int(x, y, 0));
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
