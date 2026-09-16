using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DamageableTile : DamageableTileOptions
{
    public Vector2Int position;

    private List<(Tilemap, TileBase)> tiles;
    
    private float destroyedTime;
    private bool destroyed;

    private bool touched = false;
    private float timeFirstTouched;

    //private bool hitUp;
    //private bool hitRight;
    //private bool hitDown;
    //private bool hitLeft;

    private static Dictionary<DamageableTile, Vector2Int> dynamiteTiles;
    private Rigidbody2D rigidBody;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        tiles = Map.GetTiles(position, true);
        destroyed = false;
        if (dynamiteTiles == null)
        {
            dynamiteTiles = new Dictionary<DamageableTile, Vector2Int>();
        }

        if (dynamite)
        {
            dynamiteTiles.Add(this, position);
        }
        
        rigidBody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (destroyed)
        {
            if (timeStayDestroyed == 0)
            {
                //Do nothing, remains destroyed for duration of gameplay.
            }
            else if (timeStayDestroyed < 0)
            {
                //TODO stay destroyed forever.  Save to save file.
            }
            else
            {
                var elapsed = Time.time - destroyedTime;

                if (elapsed >= timeStayDestroyed)
                {
                    
                    Map.CreateTile(position, tiles);
                    destroyed = false;
                    touched = false;
                }
            }
        }
        else
        {
            if (standOn && touched && Time.time - timeFirstTouched > standOnTimeNeeded)
            {
                destroyedTime = Time.time;
                destroyed = true;
                Map.DestroyTile(position);
            }
        }
    }

    //private void OnTriggerStay2D(Collider2D other)
    private void OnTriggerEnter2D(Collider2D collider)
    {
        HandleCollision(collider);
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        HandleCollision(collider);
    }

    private void HandleCollision(Collider2D other)
    {
        if (destroyed)
        {
            return;
        }
        
        var angleOfHit = GameMath.GetAngle((Vector2)gameObject.transform.position, (Vector2)other.transform.position);
        if ((angleOfHit < 0 && angleOfHit > 315) || (angleOfHit >= 0 && angleOfHit < 45))
        {
            if (Up == false)
            {
                return;
            }
            //hitUp = true;
            //hitRight = hitDown = hitLeft = false;
        }
        else if (angleOfHit >= 45 && angleOfHit < 135)
        {
            if (Right == false)
            {
                return;
            }
            //hitRight = true;
            //hitUp = hitDown = hitLeft = false;
        }
        else if (angleOfHit >= 135 && angleOfHit < 225)
        {
            if (Down == false)
            {
                return;
            }
            //hitDown = true;
            //hitUp = hitRight = hitLeft = false;
        }
        else
        {
            if (Left == false)
            {
                return;
            }
            //hitLeft = true;
            //hitUp = hitRight = hitDown = false;
        }

        if (roll)
        {
            var rd = other.GetComponent<RollDamage>();
            if (rd)
            {
                if (rd.enabled && rd.GetSpeed() >= rollSpeedNeeded)
                {
                    destroyedTime = Time.time;
                    destroyed = true;
                    Map.DestroyTile(position);
                    //print($"{rd.GetSpeed()}");
                }
                
                
                /*else if (rd.enabled && rd.GetSpeed() < rollSpeedNeeded)
                {
                    print($"{rd.GetSpeed()} < {rollSpeedNeeded}");
                }*/
            }
        }

        if (standOn)
        {
            if (touched == false)
            {
                timeFirstTouched = Time.time;
                touched = true;
            }
        }
    }

    public Rigidbody2D GetRigidbody2D()
    {
        return rigidBody;
    }

    public void EnableSpriteRenderer(bool enable)
    {
        if (spriteRenderer)
        {
            spriteRenderer.enabled = enable;
        }
    }

    public void SetSprite(Sprite sprite)
    {
        if (spriteRenderer)
        {
            spriteRenderer.sprite = sprite;
        }
    }

    public static Dictionary<DamageableTile, Vector2Int> GetDynamiteTiles()
    {
        return dynamiteTiles;
    }

    public void SetDestroyed()
    {
        destroyedTime = Time.time;
        destroyed = true;
    }
}