using System.Collections.Generic;
using Characters;
using UnityEngine;
using UnityEngine.Tilemaps;

// The Flashlight beams that are shining, worked out once each frame and sent to the shader that shows hidden tiles, which
// cuts them out of the beam pixel by pixel. Every section reads the same list, so they agree on which beam is which.
public static class HiddenTileBeams
{
    // How many beams the shader can take. More flashlights than this shining at once are left out.
    public const int MaxBeams = 4;

    private static readonly List<Flashlight.Beam> beams = new List<Flashlight.Beam>();
    private static readonly List<EntityId> owners = new List<EntityId>();

    private static readonly Vector4[] beamA = new Vector4[MaxBeams];
    private static readonly Vector4[] beamB = new Vector4[MaxBeams];
    private static readonly Vector4[] beamC = new Vector4[MaxBeams];

    private static readonly int BeamAId = Shader.PropertyToID("_HiddenBeamA");
    private static readonly int BeamBId = Shader.PropertyToID("_HiddenBeamB");
    private static readonly int BeamCId = Shader.PropertyToID("_HiddenBeamC");
    private static readonly int BeamCountId = Shader.PropertyToID("_HiddenBeamCount");

    private static int refreshedFrame = -1;

    public static IReadOnlyList<Flashlight.Beam> Beams
    {
        get { return beams; }
    }

    // Which flashlight each beam is from.
    public static IReadOnlyList<EntityId> Owners
    {
        get { return owners; }
    }

    // Finds the beams that are shining and gives them to the shader. Only the first time in a frame does anything.
    public static void Refresh()
    {
        if (refreshedFrame == Time.frameCount)
        {
            return;
        }

        refreshedFrame = Time.frameCount;

        beams.Clear();
        owners.Clear();

        foreach (var flashlight in Flashlight.Active)
        {
            if (beams.Count >= MaxBeams)
            {
                break;
            }

            if (flashlight && flashlight.TryGetBeam(out var beam))
            {
                beams.Add(beam);
                owners.Add(flashlight.GetEntityId());
            }
        }

        for (int i = 0; i < MaxBeams; i++)
        {
            if (i < beams.Count)
            {
                var beam = beams[i];

                beamA[i] = new Vector4(beam.origin.x, beam.origin.y, beam.direction.x, beam.direction.y);
                beamB[i] = new Vector4(
                    beam.innerHalfAngle * Mathf.Deg2Rad, beam.outerHalfAngle * Mathf.Deg2Rad, beam.reach, beam.strength);
                beamC[i] = new Vector4(beam.fadeFraction, 0f, 0f, 0f);
            }
            else
            {
                beamA[i] = Vector4.zero;
                beamB[i] = Vector4.zero;
                beamC[i] = Vector4.zero;
            }
        }

        Shader.SetGlobalVectorArray(BeamAId, beamA);
        Shader.SetGlobalVectorArray(BeamBId, beamB);
        Shader.SetGlobalVectorArray(BeamCId, beamC);
        Shader.SetGlobalFloat(BeamCountId, beams.Count);
    }
}

// The tiles in an area that can't be seen until the Flashlight shines on them. The area is the Box Collider 2D on this
// object, as it is for a DamageableTileSection, and every tile on the tilemaps inside it is hidden. Only the Flashlight shows
// them: other lights don't. How far it has to be, and how completely it shows them, depends on how bright it is, and they
// flicker when its battery is low.
//
// The tiles themselves are made see-through where they are, so that they still collide, and a picture of each is put over
// it, drawn by a shader that shows only the part of it that the beam falls on. So the beam cuts a tile through at any
// angle, with soft edges, and it is shaded by the beam like any other tile.
//
//  - The beam is blocked by tiles that collide, which aren't part of the area. Shadow Casters on anything else don't
//    block it, and a Shadow Caster on a tilemap casts the shadow of the hidden tiles whether or not they are shown.
//  - Whether a wall is in the way is worked out for each tile as a whole, not each pixel of it.
//  - A tile that is flipped or turned is drawn upright. Tiles with animated pictures are drawn as they are at the start.
[RequireComponent(typeof(BoxCollider2D))]
public class HiddenTileSection : MonoBehaviour
{
    public enum Way
    {
        [Tooltip("Always there and solid, but only seen while the beam is on them.")]
        SolidButUnseen,
        [Tooltip("Only there, and solid, while the beam is on them. When it moves away they can be walked through again.")]
        SolidOnlyWhileLit,
        [Tooltip("Always there and solid. Once the beam has shown them, they stay visible, as bright as they were at their brightest.")]
        StaysRevealed
    }

    [Tooltip("What the beam does to the tiles. See each choice.")]
    public Way way = Way.SolidButUnseen;

    [Tooltip("Only tiles on tilemaps that collide are hidden. Turn it off to hide the tiles on every tilemap in the area, which will include any backgrounds and decorations in it.")]
    public bool onlyOnCollidableTilemaps = true;

    [Tooltip("What draws the revealed tiles: a material made with the Hidden Tiles/Sprite Lit shader. Leave it empty and one is made when the game starts, which works while playing in the editor, but might not be in a build. To be sure, make a material from the shader and put it here.")]
    public Material revealMaterial;

    [Tooltip("How far in front of, or behind, the tilemap's own sorting order the revealed tiles are drawn. 0 is exactly where the tilemap is.")]
    public int sortingOrderOffset;

    [Min(0.01f)]
    [Tooltip("Seconds a tile takes to come into view when the beam gets to it past whatever was in the way.")]
    public float fadeInSeconds = 0.1f;

    [Min(0.01f)]
    [Tooltip("Seconds a tile takes to go out of view when something comes between it and the beam.")]
    public float fadeOutSeconds = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("Only for Solid Only While Lit. How brightly lit a tile has to be, on average, before it is solid. The steady beam is used for this, not the flicker of a low battery, so it doesn't come and go.")]
    public float solidAbove = 0.5f;

    [Min(0f)]
    [Tooltip("Only for Solid Only While Lit. Seconds a tile stays solid after the beam has left it, so that the beam passing over its edge doesn't make it come and go.")]
    public float solidOffDelaySeconds = 0.2f;

    // How often, in seconds, whether a tile can be seen past the tiles in between is worked out again.
    private const float SightInterval = 0.1f;

    // How far apart, in units, the points along the way from the beam to a tile are looked at.
    private const float SightStep = 0.25f;

    // How far from the middle of the tile, in each direction, the points are that its brightness is taken from, for
    // whether it is solid, or has been found. Taking several makes the edge of the beam come across a tile gradually.
    private const float SampleOffset = 0.33f;

    // A change in what a picture is told that is smaller than this isn't sent.
    private const float SendTolerance = 0.002f;

    private static readonly int BeamVisibleId = Shader.PropertyToID("_BeamVisible");
    private static readonly int FloorId = Shader.PropertyToID("_Floor");

    private static Material madeMaterial;

    private class MapTile
    {
        public Tilemap tilemap;
        public Tile.ColliderType collider;
    }

    private class Cell
    {
        public Vector2Int position;
        public readonly List<MapTile> tiles = new List<MapTile>();
        public readonly List<SpriteRenderer> pictures = new List<SpriteRenderer>();

        // How much of each beam the tile can see, from 0 to 1.
        public readonly float[] visible = new float[HiddenTileBeams.MaxBeams];

        // The most that the tile has been lit, which a tile that stays revealed goes on showing.
        public float found;

        // What the pictures were last told, so that they're only told when it changes.
        public Vector4 sentVisible;
        public float sentFound = -1f;

        public bool solid = true;
        public float lastLit = float.NegativeInfinity;

        // For each flashlight, whether the tile could be seen last time it was worked out, and when.
        public readonly Dictionary<EntityId, (float time, bool seen)> sight = new Dictionary<EntityId, (float, bool)>();
    }

    private readonly Dictionary<Vector2Int, Cell> cells = new Dictionary<Vector2Int, Cell>();
    private readonly List<Cell> cellList = new List<Cell>();
    // Made when the section starts, as Unity doesn't allow one to be made while a component is being constructed.
    private MaterialPropertyBlock block;
    private Material material;
    private bool initialized;

    void Update()
    {
        if (!initialized && Map.IsInitialized())
        {
            Initialize();
            initialized = true;
        }
    }

    // After the Flashlight has had its Update, so the beam is where it is aimed this frame.
    void LateUpdate()
    {
        if (!initialized || !material)
        {
            return;
        }

        HiddenTileBeams.Refresh();

        foreach (var cell in cellList)
        {
            UpdateCell(cell);
        }
    }

    private Material FindMaterial()
    {
        if (revealMaterial)
        {
            return revealMaterial;
        }

        if (!madeMaterial)
        {
            var shader = Shader.Find("Hidden Tiles/Sprite Lit");

            if (!shader)
            {
                return null;
            }

            madeMaterial = new Material(shader) { name = "Hidden Tiles (made when the game started)" };
        }

        return madeMaterial;
    }

    private void Initialize()
    {
        material = FindMaterial();

        if (!material)
        {
            Debug.LogError($"{name}: can't find the Hidden Tiles/Sprite Lit shader, so the tiles in this area are left as they " +
                           "are. Make a material from it and put it on the Reveal Material.", this);
            return;
        }

        block = new MaterialPropertyBlock();

        var box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;

        var bounds = box.bounds;

        // The tiles whose cells the area covers over some of their width, so a box a little off the grid still takes in
        // the tiles it is mostly on.
        var minX = Mathf.FloorToInt(bounds.min.x + 0.01f);
        var minY = Mathf.FloorToInt(bounds.min.y + 0.01f);
        var maxX = Mathf.CeilToInt(bounds.max.x - 0.01f) - 1;
        var maxY = Mathf.CeilToInt(bounds.max.y - 0.01f) - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var found = Map.GetTiles(x, y, onlyOnCollidableTilemaps);
                if (found.Count == 0)
                {
                    continue;
                }

                var cell = new Cell { position = new Vector2Int(x, y) };
                var position = new Vector3Int(x, y, 0);

                foreach (var (tilemap, _) in found)
                {
                    // Without this the tilemap won't let its colour, or whether it collides, be changed for this tile.
                    tilemap.SetTileFlags(position, TileFlags.None);

                    // The picture is made from the tile as it is, so before the tile is made see-through.
                    var picture = MakePicture(tilemap, position);
                    if (picture)
                    {
                        cell.pictures.Add(picture);
                    }

                    cell.tiles.Add(new MapTile { tilemap = tilemap, collider = tilemap.GetColliderType(position) });

                    // The tile stays where it is, to collide, but isn't drawn: the picture is.
                    tilemap.SetColor(position, new Color(1f, 1f, 1f, 0f));
                }

                cells.Add(cell.position, cell);
                cellList.Add(cell);

                Send(cell);

                // Not there to walk through until the beam is on it.
                if (way == Way.SolidOnlyWhileLit)
                {
                    SetSolid(cell, false);
                }
            }
        }
    }

    // A sprite just like the tile, drawn by the shader that shows only what the beam is on, in front of where the tile is.
    private SpriteRenderer MakePicture(Tilemap tilemap, Vector3Int position)
    {
        var overlay = TileBreakEffects.CreateOverlay(tilemap, position);
        if (overlay == null)
        {
            return null;
        }

        overlay.transform.name = "Hidden Tile";
        overlay.transform.SetParent(transform, true);

        var picture = overlay.renderer;
        picture.sharedMaterial = material;

        // Exactly where the tilemap is drawn, rather than one step in front of it, as a picture over a tile that is
        // warning of what's coming is.
        var tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer)
        {
            picture.sortingLayerID = tilemapRenderer.sortingLayerID;
            picture.sortingOrder = tilemapRenderer.sortingOrder + sortingOrderOffset;
        }

        return picture;
    }

    private void UpdateCell(Cell cell)
    {
        var beams = HiddenTileBeams.Beams;
        var owners = HiddenTileBeams.Owners;
        var centre = new Vector2(cell.position.x + 0.5f, cell.position.y + 0.5f);

        // How much of the tile the beams show on average, as it is now, and with the battery steady, so that what the
        // tile is solid by doesn't flicker. This is for whether it is solid and whether it has been found. What is drawn
        // is worked out for every pixel by the shader.
        var target = 0f;
        var steady = 0f;

        for (int i = 0; i < HiddenTileBeams.MaxBeams; i++)
        {
            var seen = false;

            if (i < beams.Count)
            {
                var beam = beams[i];

                // Well out of reach can't be lit, and nothing needs working out for it.
                if (Vector2.Distance(beam.origin, centre) <= beam.steadyReach + 1f && CanSee(cell, beam.origin, owners[i]))
                {
                    seen = true;
                    target = Mathf.Max(target, Brightness(cell, beam, false));
                    steady = Mathf.Max(steady, Brightness(cell, beam, true));
                }
            }

            var seconds = seen ? fadeInSeconds : fadeOutSeconds;
            cell.visible[i] = Mathf.MoveTowards(cell.visible[i], seen ? 1f : 0f, Time.deltaTime / Mathf.Max(seconds, 0.01f));
        }

        if (way == Way.StaysRevealed)
        {
            cell.found = Mathf.Max(cell.found, target);
        }

        var visible = new Vector4(cell.visible[0], cell.visible[1], cell.visible[2], cell.visible[3]);

        if ((visible - cell.sentVisible).sqrMagnitude > SendTolerance * SendTolerance ||
            Mathf.Abs(cell.found - cell.sentFound) > SendTolerance)
        {
            Send(cell);
        }

        if (way == Way.SolidOnlyWhileLit)
        {
            UpdateSolid(cell, steady);
        }
    }

    // Tells the pictures how much of each beam the tile can see, and how much of it to keep showing.
    private void Send(Cell cell)
    {
        cell.sentVisible = new Vector4(cell.visible[0], cell.visible[1], cell.visible[2], cell.visible[3]);
        cell.sentFound = cell.found;

        block.Clear();
        block.SetVector(BeamVisibleId, cell.sentVisible);
        block.SetFloat(FloorId, cell.found);

        foreach (var picture in cell.pictures)
        {
            picture.SetPropertyBlock(block);
        }
    }

    // Solid once the beam has brought the tile far enough into view, and not again until it's been away a moment.
    private void UpdateSolid(Cell cell, float steady)
    {
        if (steady >= solidAbove)
        {
            cell.lastLit = Time.time;
        }

        var shouldBeSolid = Time.time - cell.lastLit <= solidOffDelaySeconds;

        if (shouldBeSolid != cell.solid)
        {
            SetSolid(cell, shouldBeSolid);
        }
    }

    // Turns whether the tile collides on or off, as it was before it was hidden. Anything in its space when it comes on is
    // moved out of it first, as for a tile that returns.
    private void SetSolid(Cell cell, bool solid)
    {
        cell.solid = solid;

        if (solid)
        {
            Map.AnnounceSolidTile(cell.position);
        }

        foreach (var tile in cell.tiles)
        {
            tile.tilemap.SetColliderType(
                new Vector3Int(cell.position.x, cell.position.y, 0),
                solid ? tile.collider : Tile.ColliderType.None);
        }
    }

    // How much of a tile a beam lights, from 0 to 1 at the most that it shows. It is taken from several points on the tile,
    // so a tile the edge of the beam is on is partly lit.
    private static float Brightness(Cell cell, Flashlight.Beam beam, bool steady)
    {
        var total = 0f;

        for (int ix = -1; ix <= 1; ix++)
        {
            for (int iy = -1; iy <= 1; iy++)
            {
                var point = new Vector2(
                    cell.position.x + 0.5f + ix * SampleOffset,
                    cell.position.y + 0.5f + iy * SampleOffset);

                total += BrightnessAt(point, beam, steady);
            }
        }

        return total / 9f;
    }

    // How much a beam shows at a point: its strength, less the further the point is toward the end of its reach and the
    // further it is to the side of where it points. Nothing is shown outside either. The shader works it out the same way.
    private static float BrightnessAt(Vector2 point, Flashlight.Beam beam, bool steady)
    {
        var reach = steady ? beam.steadyReach : beam.reach;
        var strength = steady ? beam.steadyStrength : beam.strength;

        if (reach <= 0f || strength <= 0f)
        {
            return 0f;
        }

        var offset = point - beam.origin;
        var distance = offset.magnitude;

        if (distance > reach)
        {
            return 0f;
        }

        var fadeStart = reach * (1f - beam.fadeFraction);
        var distanceFactor = distance <= fadeStart || reach <= fadeStart
            ? 1f
            : 1f - Smooth((distance - fadeStart) / (reach - fadeStart));

        var angleFactor = 1f;

        if (distance > 0.0001f)
        {
            var angle = Vector2.Angle(beam.direction, offset);

            if (angle >= beam.outerHalfAngle)
            {
                return 0f;
            }

            if (angle > beam.innerHalfAngle)
            {
                angleFactor = 1f - Smooth((angle - beam.innerHalfAngle) / (beam.outerHalfAngle - beam.innerHalfAngle));
            }
        }

        return strength * distanceFactor * angleFactor;
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // Whether the beam can get to the tile: nothing that collides is in the way, except other tiles of this area, which are
    // shown by the beam the same way and don't hide each other. Worked out every so often, not every frame.
    private bool CanSee(Cell cell, Vector2 origin, EntityId owner)
    {
        if (cell.sight.TryGetValue(owner, out var last) && Time.time - last.time < SightInterval)
        {
            return last.seen;
        }

        var seen = ClearPath(cell, origin);
        cell.sight[owner] = (Time.time, seen);

        return seen;
    }

    private bool ClearPath(Cell cell, Vector2 origin)
    {
        var target = new Vector2(cell.position.x + 0.5f, cell.position.y + 0.5f);
        var offset = target - origin;
        var distance = offset.magnitude;

        if (distance < 0.0001f)
        {
            return true;
        }

        var direction = offset / distance;
        var start = new Vector2Int(Mathf.FloorToInt(origin.x), Mathf.FloorToInt(origin.y));

        for (var along = 0f; along < distance; along += SightStep)
        {
            var point = origin + direction * along;
            var space = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));

            // The space it is shining from, and the tile itself, don't block it, and nor do the area's other tiles.
            if (space == start || space == cell.position || cells.ContainsKey(space))
            {
                continue;
            }

            if (Map.HasSolidTile(space.x, space.y))
            {
                return false;
            }
        }

        return true;
    }
}
