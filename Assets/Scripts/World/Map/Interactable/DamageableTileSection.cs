using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(BoxCollider2D))]
public class DamageableTileSection : DamageableTileOptions
{
    // How far, in units, the trigger of a tile reaches past the tile on every side. Whatever stands on, or touches, a tile
    // only touches its edge, which a trigger just the size of the tile doesn't overlap.
    private const float ContactReach = 0.1f;

    //private Dictionary<Vector2, List<(Tilemap, TileBase)>> tiles;

    [Tooltip("Only tiles on tilemaps that have a collider are made damageable.")]
    public bool onlyOnCollidableTilemaps = true;
    
    [Tooltip("Extra padding around the tile to extend the destruction trigger.")]
    public Vector2 triggerAreaBuffer;

    private bool initialized;
    private List<DamageableTile> damageableTiles;
    
    void Start()
    {
        /*tiles = new Dictionary<Vector2, List<(Tilemap, TileBase)>>();
        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;*/
        damageableTiles = new List<DamageableTile>();
        initialized = false;
    }

    void Update()
    {
        if (!initialized && Map.IsInitialized())
        {
            //Make sure the map is initialized first
            Initialize();
            initialized = true;
        }
    }

    private void Initialize()
    {
        /*
         *      position = -10, -5
         *      size = 2 x 2
         *
         *      Contains (-10, -5), (-11, -5), (-11, -5), (-11, -6)
         *
         *
         *      position = 20, 20
         *      size = 4 x 4
         *
         *      Contains (18, 21) => (21, 18)   (18, 19, 20, 21) x (18, 19, 20, 21)
         *
         *
         *      position = -10.5, -8.5
         *      size = 3 x 5
         *
         *      Contains (-12, -7) => (-10, -11)
         */
        
        var boxCollider = GetComponent<BoxCollider2D>();
        if (!boxCollider)
        {
            print("Damageable Tile Section does not have a box collider!");
            return;
        }
        
        // Rounded down, which is the same as dropping the fraction except left of 0 and below it, where dropping it would
        // take the area a tile too far in.
        var min = new Vector2Int(Mathf.FloorToInt(boxCollider.bounds.min.x), Mathf.FloorToInt(boxCollider.bounds.min.y));
        var max = new Vector2Int(Mathf.FloorToInt(boxCollider.bounds.max.x) - 1, Mathf.FloorToInt(boxCollider.bounds.max.y) - 1);
        
        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                var tiles = Map.GetTiles(x, y, onlyOnCollidableTilemaps);
                if (tiles.Count == 0)
                {
                    continue;
                }

                //TileBase tileBase = null;

                var damageableTile = new GameObject
                {
                    transform =
                    {
                        parent = transform,
                        position = new Vector3(x, y, 0)
                    }
                };

                var bc = damageableTile.AddComponent<BoxCollider2D>();
                bc.isTrigger = true;
                bc.offset = new Vector2(0.5f, 0.5f);
                bc.size = new Vector2(1 + triggerAreaBuffer.x * 2, 1 + triggerAreaBuffer.y * 2);

                // The trigger reaches a little past the tile on every side, whatever the tile is set to, so that the sides
                // and kinds of damage can be changed while playing and still be felt.
                bc.size += Vector2.one * (ContactReach * 2f);

                var dt = damageableTile.AddComponent<DamageableTile>();
                dt.roll = roll;
                dt.standOn = standOn;
                dt.touch = touch;

                dt.rollSpeedNeeded = rollSpeedNeeded;
                dt.standOnTimeNeeded = standOnTimeNeeded;

                dt.timeStayDestroyed = timeStayDestroyed;

                // What shows when it breaks is read from the section as it is then, rather than copied now, so it can be
                // changed while playing.
                dt.section = this;

                // Dynamite is read from the section as it is when a blast comes, like the rest, so it isn't copied.

                dt.position = new Vector2Int(x, y);

                if (All)
                {
                    Up = Down = Left = Right = true;
                }

                dt.All = All;
                dt.Up = Up;
                dt.Down = Down;
                dt.Left = Left;
                dt.Right = Right;
                
                damageableTiles.Add(dt);
            }
        }

        boxCollider.enabled = false;
    }
}
