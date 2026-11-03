using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DamageableTile : DamageableTileOptions
{
    public Vector2Int position;

    // The section this tile is a part of, which is where what shows when it breaks is taken from.
    public DamageableTileOptions section;

    private List<(Tilemap, TileBase)> tiles;
    
    private float destroyedTime;
    private bool destroyed;

    private bool touched = false;
    private float timeFirstTouched;

    //private bool hitUp;
    //private bool hitRight;
    //private bool hitDown;
    //private bool hitLeft;

    // Always there, even in a scene with no dynamite tiles. Each tile takes itself out when it is destroyed, so a scene
    // that is left doesn't leave its tiles behind in it.
    private static readonly Dictionary<DamageableTile, Vector2Int> dynamiteTiles =
        new Dictionary<DamageableTile, Vector2Int>();
    private Rigidbody2D rigidBody;
    private SpriteRenderer spriteRenderer;
    private Collider2D triggerCollider;

    // The space of every tile that can be destroyed, whether or not it is at the moment. Somewhere a tile may come back
    // to isn't a place to leave anyone.
    private static readonly HashSet<Vector2Int> destroyableTileSpaces = new HashSet<Vector2Int>();

    void Start()
    {
        tiles = Map.GetTiles(position, true);
        destroyed = false;
        destroyableTileSpaces.Add(position);

        if (dynamite)
        {
            dynamiteTiles[this] = position;
        }

        rigidBody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        triggerCollider = GetComponent<Collider2D>();
    }

    void OnDestroy()
    {
        dynamiteTiles.Remove(this);
        destroyableTileSpaces.Remove(position);
    }

    public static bool IsDestroyableTileSpace(Vector2Int cell)
    {
        return destroyableTileSpaces.Contains(cell);
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
                Break(Vector2.zero);
            }
        }

        // Nothing can damage a tile that's gone, so while it is, it isn't listening for anything touching it. A tile blown
        // loose by dynamite is left as it is, as that is a piece falling.
        if (triggerCollider && !dynamite)
        {
            var listening = !destroyed;

            if (triggerCollider.enabled != listening)
            {
                triggerCollider.enabled = listening;
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

    // Destroys the tile, with whatever shows that it has been. The velocity is that of whatever hit it, so that the debris
    // goes the way it was hit, or none if nothing did.
    private void Break(Vector2 impact)
    {
        // Before the tile is taken away, as the debris is made from its picture.
        ShowBreak(impact);

        destroyedTime = Time.time;
        destroyed = true;
        Map.DestroyTile(position);
    }

    private void ShowBreak(Vector2 impact)
    {
        var centre = new Vector3(position.x + 0.5f, position.y + 0.5f, 0f);

        // The section's settings as they are now, if there is one.
        var options = section ? section : this;

        TileBreakEffects.Sound(options.breakSound, centre);

        if (options.breakEffect == BreakEffect.Particles)
        {
            TileBreakEffects.Particles(options.breakParticles, centre);
        }
        else if (options.breakEffect == BreakEffect.Debris)
        {
            if (tiles.Count == 0)
            {
                Debug.LogWarning($"The tile at {position} has no tiles on tilemaps that can be destroyed, so it has no debris.", this);
            }

            // The pictures are taken now, while the tile is still there, but the pieces aren't made until the physics has
            // caught up with the tile being taken away.
            var plans = new List<TileBreakEffects.DebrisPlan>();

            foreach (var (tilemap, _) in tiles)
            {
                if (TileBreakEffects.PlanDebris(tilemap, new Vector3Int(position.x, position.y, 0), out var plan))
                {
                    plans.Add(plan);
                }
            }

            StartCoroutine(ThrowDebrisWhenClear(plans, options, impact));
        }
    }

    // The solid part of a tile is only taken away by the physics a step after the tile is. Pieces made before then would be
    // inside it, and pushed out of it, in the same direction, instead of flying off the way they are thrown.
    private IEnumerator ThrowDebrisWhenClear(List<TileBreakEffects.DebrisPlan> plans, DamageableTileOptions options, Vector2 impact)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        foreach (var plan in plans)
        {
            TileBreakEffects.ThrowDebris(plan, options, impact);
        }
    }

    private enum Side
    {
        Up,
        Down,
        Left,
        Right
    }

    // The side of the tile that something is touching: the face of the tile that is nearest to it, which is where the
    // contact is. That's not the same as which way it is from the middle of the tile, as something rolling along the top of
    // a row of tiles is touching the top of the next one while it is still off to the side of it. If it is inside the
    // tile, it's the face it is nearest to.
    private Side SideTouched(Collider2D other)
    {
        var centre = (Vector2)other.bounds.center;

        // How far past each face of the tile it is, which is negative if it's not yet past that face.
        var up = centre.y - (position.y + 1);
        var down = position.y - centre.y;
        var left = position.x - centre.x;
        var right = centre.x - (position.x + 1);

        // The face it is furthest past. If it's past none of them, that's the one it is nearest to. A tie, which is
        // a corner, goes to up or down.
        var side = Side.Up;
        var furthest = up;

        if (down > furthest)
        {
            side = Side.Down;
            furthest = down;
        }

        if (left > furthest)
        {
            side = Side.Left;
            furthest = left;
        }

        if (right > furthest)
        {
            side = Side.Right;
        }

        return side;
    }

    private bool IsDamagedFrom(Side side)
    {
        switch (side)
        {
            case Side.Up:
                return Up;

            case Side.Down:
                return Down;

            case Side.Left:
                return Left;

            default:
                return Right;
        }
    }

    private void HandleCollision(Collider2D other)
    {
        if (destroyed)
        {
            return;
        }
        
        if (!IsDamagedFrom(SideTouched(other)))
        {
            return;
        }

        if (roll)
        {
            if (other.TryGetComponent<RollDamage>(out var rd) && rd.CanBreak(rollSpeedNeeded))
            {
                Break(rd.GetImpactVelocity());
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