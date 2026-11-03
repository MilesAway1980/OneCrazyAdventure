using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// What shows when a tile is destroyed: an effect where it was, and its own sprite broken into four pieces that fly off.
public static class TileBreakEffects
{
    // The layer the bodies of the pieces are on. It should collide with the ground and nothing else that can be bumped
    // into. Without it, the pieces have no colliders and fall away through everything.
    private const string DebrisLayerName = "Debris";

    // How long an effect with nothing to say how long it lasts is kept.
    private const float DefaultEffectSeconds = 3f;

    private static readonly Dictionary<Sprite, Sprite[]> quarters = new Dictionary<Sprite, Sprite[]>();
    private static bool warnedAboutLayer;

    // What debris is made from. It's taken while the tile is still there, as it's gone when the debris is made, which
    // can't be until the physics has caught up with the tile being taken away.
    public struct DebrisPlan
    {
        public Sprite[] pieces;
        public Vector3 centre;
        public Vector3 quarterSize;
        public Vector3 scale;
        public Color color;
        public bool sorted;
        public int sortingLayerID;
        public int sortingOrder;
    }

    // An effect where the tile was, removed once it has finished playing.
    public static void Particles(GameObject prefab, Vector3 centre)
    {
        if (!prefab)
        {
            return;
        }

        var effect = Object.Instantiate(prefab, centre, Quaternion.identity);

        var seconds = 0f;
        foreach (var system in effect.GetComponentsInChildren<ParticleSystem>())
        {
            var main = system.main;
            seconds = Mathf.Max(seconds, main.duration + main.startLifetime.constantMax);
        }

        Object.Destroy(effect, seconds > 0f ? seconds : DefaultEffectSeconds);
    }

    // A sound where the tile was. It's played flat, as the listener is a long way from the 2D game.
    public static void Sound(AudioClip clip, Vector3 centre)
    {
        if (!clip)
        {
            return;
        }

        var soundObject = new GameObject("Tile Break Sound");
        soundObject.transform.position = centre;

        var source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 0f;
        source.Play();

        Object.Destroy(soundObject, clip.length + 0.1f);
    }

    // What is needed to make the debris of a tile on a tilemap, or false if there is nothing to make it from.
    public static bool PlanDebris(Tilemap tilemap, Vector3Int cell, out DebrisPlan plan)
    {
        plan = new DebrisPlan();

        var sprite = tilemap.GetSprite(cell);
        if (!sprite)
        {
            Debug.LogWarning($"There is no sprite on {tilemap.name} at {cell} to make debris from.", tilemap);
            return false;
        }

        var scale = tilemap.transform.lossyScale;

        // Where the picture is: it hangs from its pivot, which is the middle of the cell unless the tilemap says otherwise.
        var pivot = tilemap.CellToWorld(cell) + tilemap.tileAnchor;

        plan.pieces = QuartersOf(sprite);
        plan.scale = scale;
        plan.centre = pivot + Vector3.Scale(sprite.bounds.center, scale);
        plan.quarterSize = new Vector3(sprite.bounds.size.x * 0.25f * scale.x, sprite.bounds.size.y * 0.25f * scale.y, 0f);
        plan.color = tilemap.GetColor(cell) * tilemap.color;

        var tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer)
        {
            plan.sorted = true;
            plan.sortingLayerID = tilemapRenderer.sortingLayerID;
            plan.sortingOrder = tilemapRenderer.sortingOrder + 1;
        }

        return true;
    }

    // The tile's own sprite as four pieces. If something hit it, they are thrown the way it was going, a little either way.
    // If nothing did, they are thrown out from the middle of the tile in random directions.
    public static void ThrowDebris(DebrisPlan plan, DamageableTileOptions options, Vector2 impact)
    {
        var layer = LayerMask.NameToLayer(DebrisLayerName);

        if (layer < 0 && !warnedAboutLayer)
        {
            warnedAboutLayer = true;
            Debug.Log($"There is no layer called \"{DebrisLayerName}\", so pieces of broken tiles have no colliders. Add it " +
                      "in Project Settings > Tags and Layers, and set what it collides with in the Physics 2D layer matrix.");
        }

        for (int i = 0; i < plan.pieces.Length; i++)
        {
            // Left or right, and bottom or top, of the middle.
            var side = new Vector3(i % 2 == 0 ? -1f : 1f, i / 2 == 0 ? -1f : 1f, 0f);
            var offset = Vector3.Scale(side, plan.quarterSize);

            var pieceObject = new GameObject("Tile Debris");
            pieceObject.transform.position = plan.centre + offset;

            // The picture is a child of its own, on the default layer, so that every camera draws it however its culling
            // mask is set. Only the body, which the physics sees, is on the debris layer.
            var pictureObject = new GameObject("Picture");
            pictureObject.transform.SetParent(pieceObject.transform, false);
            pictureObject.transform.localScale = plan.scale;

            var pieceRenderer = pictureObject.AddComponent<SpriteRenderer>();
            pieceRenderer.sprite = plan.pieces[i];
            pieceRenderer.color = plan.color;

            if (plan.sorted)
            {
                pieceRenderer.sortingLayerID = plan.sortingLayerID;
                pieceRenderer.sortingOrder = plan.sortingOrder;
            }

            if (layer >= 0)
            {
                pieceObject.layer = layer;

                var box = pieceObject.AddComponent<BoxCollider2D>();
                box.size = Vector2.Scale(plan.pieces[i].bounds.size, plan.scale);
            }

            var body = pieceObject.AddComponent<Rigidbody2D>();
            body.gravityScale = options.debrisGravityScale;
            body.angularVelocity = Random.Range(-options.debrisSpin, options.debrisSpin);

            // A small piece thrown fast at something thin could go straight through it.
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // The way it was hit, or out from the middle of the tile if it wasn't, a little either way, and always with a
            // kick upward, so that a piece from the bottom doesn't just drop. When it was hit, some of the speed it hit
            // with is added to the speed of the throw, so a harder hit throws them further. It goes the way that piece is
            // thrown, rather than the way it was hit, or it would swamp the angle that is chosen and they'd all go together.
            var hit = impact.sqrMagnitude > 0.0001f;
            var centreHeading = hit
                ? Mathf.Atan2(impact.y, impact.x) * Mathf.Rad2Deg
                : Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            var heading = centreHeading + Random.Range(-options.debrisSpread, options.debrisSpread);

            var speed = Random.Range(options.debrisMinSpeed, options.debrisMaxSpeed);
            var direction = new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));
            var thrown = speed + impact.magnitude * options.debrisImpactShare;
            body.linearVelocity = direction * thrown + Vector2.up * speed * 0.5f;

            pieceObject.AddComponent<TileDebris>().Begin(pieceRenderer, options.debrisSeconds);
        }
    }

    // A sprite cut into four: bottom left, bottom right, top left, top right. Each is turned about its own middle. They
    // are made once for each sprite, and kept.
    private static Sprite[] QuartersOf(Sprite sprite)
    {
        if (quarters.TryGetValue(sprite, out var made) && made[0])
        {
            return made;
        }

        var rect = sprite.textureRect;
        var halfWidth = Mathf.Floor(rect.width * 0.5f);
        var halfHeight = Mathf.Floor(rect.height * 0.5f);

        var pieces = new Sprite[4];
        for (int i = 0; i < pieces.Length; i++)
        {
            var part = new Rect(rect.x + (i % 2) * halfWidth, rect.y + (i / 2) * halfHeight, halfWidth, halfHeight);
            pieces[i] = Sprite.Create(sprite.texture, part, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
        }

        quarters[sprite] = pieces;

        return pieces;
    }
}
