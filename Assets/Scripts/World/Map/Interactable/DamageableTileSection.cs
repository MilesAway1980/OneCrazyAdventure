using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// TODO Tiles that are damaged by standing on them (standOn) should have an optional weight to go with it: how heavy what
// stands on them has to be. A character of a mass of 20 could then cross, where a mass of 50 would break them.
public class DamageableTileSection : DamageableTileOptions
{
    //private Dictionary<Vector2, List<(Tilemap, TileBase)>> tiles;

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
        
        var min = new Vector2Int((int)boxCollider.bounds.min.x, (int)boxCollider.bounds.min.y);
        var max = new Vector2Int((int)boxCollider.bounds.max.x - 1, (int)boxCollider.bounds.max.y - 1);
        
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
                
                var dt = damageableTile.AddComponent<DamageableTile>();
                dt.roll = roll;
                dt.standOn = standOn;
                
                dt.rollSpeedNeeded = rollSpeedNeeded;
                dt.standOnTimeNeeded = standOnTimeNeeded;

                dt.timeStayDestroyed = timeStayDestroyed;
                
                dt.dynamite = dynamite;
                dt.dynamiteMass = dynamiteMass;
                dt.dynamiteForceNeeded = dynamiteForceNeeded;

                if (dt.dynamite)
                {
                    var rb = dt.gameObject.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    var spriteObj = new GameObject();
                    spriteObj.transform.SetParent(damageableTile.transform);
                    spriteObj.transform.localPosition = new Vector3(0.5f, 0.5f, 0);
                    var spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();

                    rb.mass = dynamiteMass;

                    foreach (var t in tiles)
                    {
                        var tile = (Tile)t.Item2;
                        if (tile)
                        {
                            spriteRenderer.sprite = tile.sprite;
                            spriteRenderer.enabled = false;
                        }
                    }

                    //spriteObj.SetActive(false);
                }
                
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
