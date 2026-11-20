using System.Collections.Generic;
using Mirror;
using UnityEngine;

// An area of liquid, in any shape. Anything inside it is in that liquid: characters, and what has a FloatsInFluid, respond to it as
// they do to a liquid tilemap.
//
// How it works:
//  - Make a Liquid Area, and it starts as a box. Its outline is a list of points, which are in the object's own space. They're drawn
//    in the scene view and can be dragged there, like the points of a Polygon Collider 2D: drag a point to move it, click the small
//    dot in the middle of an edge to add a point there, and Shift-click a point to delete it. Moving, turning or scaling the object
//    moves, turns or scales the area, so a body of liquid can ride a boat, or a platform, or whatever it's a child of.
//  - Density and Viscosity say what the liquid is: water is 1 and 1, oil about 0.9 and 4. A pool of oil in a lake is its own Liquid
//    Area, over the lake's. Where areas
//    overlap, the one with the higher Priority is in force, and if they're the same, the smaller one is, so a small area inside a big
//    one wins without anything being set. An area over a liquid tilemap wins over the tilemap.
//
// The overlay is a picture drawn over the area, stretched to fit it or repeated across it, as see-through as Transparency says: 0
// percent is solid, and 100 is invisible. It's made while playing, and in the editor, and isn't saved in the scene.
[ExecuteAlways]
[DisallowMultipleComponent]
public class LiquidArea : MonoBehaviour
{
    public enum OverlayStyle
    {
        // The picture is stretched to fit the area.
        Stretch,
        // The picture is repeated across it, at its own size. The picture's texture needs its Wrap Mode set to Repeat, and the sprite
        // should be the whole texture.
        Tile
    }

    [Header("The shape")]
    [Tooltip("The corners of the outline, in the object's own space, in order round it. Drag them in the scene view.")]
    public List<Vector2> points = new List<Vector2>
    {
        new Vector2(-2f, -1f),
        new Vector2(-2f, 1f),
        new Vector2(2f, 1f),
        new Vector2(2f, -1f)
    };

    [Header("What it is")]
    [Min(0.01f)]
    [Tooltip("How dense the liquid is, compared with water, which is 1. A character denser than this sinks in it, and a lighter one floats. Oil is about 0.9.")]
    public float density = 1f;

    [Min(0f)]
    [Tooltip("How thick the liquid is, compared with water, which is 1. Oil is a few times that and honey far more. The thicker it is, the slower things move through it and the more slowly a floating thing settles.")]
    public float viscosity = 1f;

    [Min(0f)]
    [Tooltip("The least a kick or hop in this liquid lifts a character, in tiles, whatever its jump and the liquid's drag come to, so that every character can at least jump off the bottom and not be stuck. A deeper or thicker liquid can ask for more.")]
    public float minKickHeight = Fluid.DefaultMinKickHeight;

    [Tooltip("Where areas overlap, the higher one is in force. If they're the same, the smaller area is.")]
    public int priority;

    [Header("How it looks")]
    [Tooltip("The picture drawn over the area. Nothing is drawn if it's empty.")]
    public Sprite overlay;

    [Tooltip("Stretch makes the picture fit the area; Tile repeats it across it.")]
    public OverlayStyle overlayStyle = OverlayStyle.Stretch;

    [Tooltip("The colour the picture is tinted. White leaves it as it is.")]
    public Color tint = Color.white;

    [Range(0f, 100f)]
    [Tooltip("How see-through the overlay is, as a percentage: 0 is solid, and 100 is invisible.")]
    public float transparency = 50f;

    [Tooltip("Which things the overlay is drawn in front of. Higher is in front.")]
    public int sortingOrder = 5;

    [Header("Tide")]
    [Range(0f, 100f)]
    [Tooltip("How far the surface rises and falls by itself, as a percentage of how full the area is: 5 means it slowly drops up to 5 percent below the top and comes back up. The area as drawn is the top, so it never goes above it. 0 keeps it still.")]
    public float tideRange = 0f;

    [Min(0.5f)]
    [Tooltip("How long one whole rise and fall takes, in seconds.")]
    public float tidePeriod = 20f;

    [Range(0f, 1f)]
    [Tooltip("How uneven the tide is: 0 is a smooth, regular rise and fall, and 1 drifts about without a pattern.")]
    public float tideRandomness = 0f;

    [Range(0f, 1f)]
    [Tooltip("Where in its cycle the tide starts, from 0 to 1, so that areas next to each other aren't in step. It's the same on every machine in a network game.")]
    public float tidePhase = 0f;

    private const string OverlayName = "Liquid Overlay";

    // Every liquid area that's switched on, which is what's asked where a liquid is.
    private static readonly List<LiquidArea> all = new List<LiquidArea>();

    // What's worked out from the points: the triangles that fill the outline, its box, and its area. Redone when the points change.
    private readonly List<int> triangles = new List<int>();
    private Rect localBounds;
    private float localArea;
    private bool validShape;
    private int shapeHash;
    private int shapeVersion;
    private bool shapeReady;

    // What the overlay was last made from, so it's only made again when something has changed.
    private int drawnVersion = -1;
    private Sprite drawnSprite;
    private OverlayStyle drawnStyle;
    private Color drawnTint;
    private float drawnTransparency;

    // How full the area is, from 0 (none) to 1 (all of it, as drawn), by height: the liquid is the part of the outline below a level surface
    // that is that fraction of the way up it. Raised and lowered by a SwitchLiquid. It isn't saved: the area is full to begin with.
    private float fill = 1f;
    private float drawnLevel = 1f;

    // The tide's drop for the frame it was last worked out in.
    private int tideFrame = -1;
    private float tideDropNow;

    private Fluid fluid;
    private GameObject drawing;
    private Mesh mesh;
    private Material material;

    // The liquid this area is of, from its density, viscosity and minimum kick. It's only made again when one of them changes.
    public Fluid EffectiveFluid
    {
        get
        {
            if (fluid == null || fluid.density != Mathf.Max(0.01f, density) || fluid.viscosity != Mathf.Max(0f, viscosity)
                || fluid.minKickHeight != Mathf.Max(0f, minKickHeight))
            {
                fluid = new Fluid("Liquid", density, viscosity, minKickHeight);
            }

            return fluid;
        }
    }

    // Whether the outline is a shape that can be filled: three or more points, not crossing itself.
    public bool HasValidShape
    {
        get
        {
            EnsureShape();
            return validShape;
        }
    }

    // The triangles that fill the outline, as indexes into the points, three to a triangle. Empty if the shape isn't valid.
    public IReadOnlyList<int> Triangles
    {
        get
        {
            EnsureShape();
            return triangles;
        }
    }

    // How full it is, from 0 to 1, as set from outside (a SwitchLiquid). The tide takes a little off it: see Level.
    public float Fill
    {
        get { return fill; }
        set { fill = Mathf.Clamp01(value); }
    }

    // How full it is now, from 0 to 1: Fill, less the tide. The liquid is the part of the area under a level surface that far up from the
    // lowest point of the outline to the highest, in the world, so the surface is level however the area is turned.
    public float Level
    {
        get { return fill * (1f - TideDrop); }
    }

    // How far the tide has taken the surface down, as a fraction of how full it is, now: from 0 to Tide Range. It's worked out from the
    // time, so it needs nothing run each frame, and (with the time that's shared in a network game) is the same on every machine.
    public float TideDrop
    {
        get
        {
            if (tideRange <= 0f)
            {
                return 0f;
            }

            if (tideFrame != Time.frameCount)
            {
                tideFrame = Time.frameCount;
                tideDropNow = WorkOutTide();
            }

            return tideDropNow;
        }
    }

    private float WorkOutTide()
    {
        var now = NetworkClient.active || NetworkServer.active ? NetworkTime.time : Time.timeAsDouble;
        var cycles = (float)(now / Mathf.Max(0.5f, tidePeriod)) + tidePhase;

        // A smooth rise and fall, 0 at the top of the tide and 1 at the bottom, or the same drifting about without a pattern.
        var regular = 0.5f - 0.5f * Mathf.Cos(cycles * Mathf.PI * 2f);
        var drifting = Mathf.PerlinNoise(cycles * 0.9f, tidePhase * 100f + 0.37f);

        return Mathf.Clamp01(Mathf.Lerp(regular, drifting, tideRandomness)) * tideRange / 100f;
    }

    // The box the outline covers in the world, however it's moved, turned or scaled.
    public Rect WorldRect
    {
        get
        {
            if (points.Count == 0)
            {
                return new Rect(transform.position, Vector2.zero);
            }

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            foreach (var point in points)
            {
                var world = transform.TransformPoint(point);

                minX = Mathf.Min(minX, world.x);
                minY = Mathf.Min(minY, world.y);
                maxX = Mathf.Max(maxX, world.x);
                maxY = Mathf.Max(maxY, world.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }

    // How high the surface is in the world at the level it's filled to now.
    public float SurfaceY
    {
        get
        {
            var rect = WorldRect;
            return Mathf.Lerp(rect.yMin, rect.yMax, Level);
        }
    }

    // The area it covers in the world, however it's scaled.
    private float WorldArea
    {
        get
        {
            var scale = transform.lossyScale;
            return localArea * Mathf.Abs(scale.x * scale.y);
        }
    }

    // The liquid at a point in the world, or null if it's in none of the areas. Where there are several, the one with the highest
    // priority, and then the smallest.
    public static Fluid FluidAt(Vector2 point)
    {
        LiquidArea best = null;
        var bestArea = 0f;

        foreach (var area in all)
        {
            if (!area.Contains(point))
            {
                continue;
            }

            var areaSize = area.WorldArea;

            if (!best || area.priority > best.priority || (area.priority == best.priority && areaSize < bestArea))
            {
                best = area;
                bestArea = areaSize;
            }
        }

        return best ? best.EffectiveFluid : null;
    }

    // Whether a point in the world is inside the outline, however the object is moved, turned or scaled.
    public bool Contains(Vector2 point)
    {
        EnsureShape();

        if (points.Count < 3)
        {
            return false;
        }

        var local = (Vector2)transform.InverseTransformPoint(point);

        if (!localBounds.Contains(local))
        {
            return false;
        }

        // Not above the surface, if it's not full.
        var level = Level;

        if (level < 1f && (level <= 0f || point.y > SurfaceY))
        {
            return false;
        }

        // Even-odd: a ray to the right crosses the outline an odd number of times if the point is inside.
        var inside = false;

        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            var a = points[i];
            var b = points[j];

            if ((a.y > local.y) != (b.y > local.y) && local.x < (b.x - a.x) * (local.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    // Tells it the points have been changed from outside, and has the overlay made again.
    public void Refresh()
    {
        shapeReady = false;
        Draw();
    }

    void OnEnable()
    {
        all.Add(this);
        RemoveStrayOverlays();
        EnsureShape();
    }

    void OnDisable()
    {
        all.Remove(this);
        DestroyDrawing();
    }

    void Reset()
    {
        points = new List<Vector2>
        {
            new Vector2(-2f, -1f),
            new Vector2(-2f, 1f),
            new Vector2(2f, 1f),
            new Vector2(2f, -1f)
        };
    }

    void OnValidate()
    {
        shapeReady = false;
    }

    // After everything has moved, so the picture is where the area is.
    void LateUpdate()
    {
        Draw();
    }

    // Works out the triangles, box and area from the points, if they've changed since last time.
    private void EnsureShape()
    {
        unchecked
        {
            var hash = points.Count;

            foreach (var point in points)
            {
                hash = hash * 31 + point.x.GetHashCode();
                hash = hash * 31 + point.y.GetHashCode();
            }

            if (shapeReady && hash == shapeHash)
            {
                return;
            }

            shapeHash = hash;
        }

        shapeReady = true;
        shapeVersion++;

        triangles.Clear();
        localArea = 0f;
        localBounds = new Rect();
        validShape = false;

        if (points.Count < 3)
        {
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

        foreach (var point in points)
        {
            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
        }

        localBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        localArea = Mathf.Abs(SignedArea(points));
        validShape = Triangulate(points, triangles);

        if (!validShape)
        {
            triangles.Clear();
        }
    }

    private static float SignedArea(List<Vector2> shape)
    {
        var sum = 0f;

        for (int i = 0, j = shape.Count - 1; i < shape.Count; j = i++)
        {
            sum += shape[j].x * shape[i].y - shape[i].x * shape[j].y;
        }

        return sum * 0.5f;
    }

    // Cuts a shape, which can be concave, into triangles by taking off one corner at a time ("ears"). Returns false if it can't, which
    // is when the outline crosses itself.
    public static bool Triangulate(List<Vector2> shape, List<int> result)
    {
        if (shape.Count < 3)
        {
            return false;
        }

        var remaining = new List<int>(shape.Count);

        for (var i = 0; i < shape.Count; i++)
        {
            remaining.Add(i);
        }

        // The corners are tested as going anticlockwise.
        if (SignedArea(shape) < 0f)
        {
            remaining.Reverse();
        }

        while (remaining.Count > 3)
        {
            var count = remaining.Count;
            var cut = false;

            for (var k = 0; k < count && !cut; k++)
            {
                var ia = remaining[(k + count - 1) % count];
                var ib = remaining[k];
                var ic = remaining[(k + 1) % count];
                Vector2 a = shape[ia], b = shape[ib], c = shape[ic];

                var turn = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);

                if (Mathf.Abs(turn) < 1e-7f)
                {
                    // In a straight line with its neighbours (or doubled back on them): it adds nothing, so it's just dropped.
                    remaining.RemoveAt(k);
                    cut = true;
                    continue;
                }

                if (turn < 0f)
                {
                    // Turns the wrong way: a dent in the outline, not an ear.
                    continue;
                }

                var ear = true;

                for (var m = 0; m < count && ear; m++)
                {
                    var im = remaining[m];

                    if (im == ia || im == ib || im == ic)
                    {
                        continue;
                    }

                    var p = shape[im];

                    if (p != a && p != b && p != c && InTriangle(p, a, b, c))
                    {
                        ear = false;
                    }
                }

                if (!ear)
                {
                    continue;
                }

                result.Add(ia);
                result.Add(ib);
                result.Add(ic);
                remaining.RemoveAt(k);
                cut = true;
            }

            if (!cut)
            {
                return false;
            }
        }

        if (remaining.Count == 3)
        {
            result.Add(remaining[0]);
            result.Add(remaining[1]);
            result.Add(remaining[2]);
        }

        return result.Count >= 3;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        var d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        var d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);

        var negative = d1 < 0f || d2 < 0f || d3 < 0f;
        var positive = d1 > 0f || d2 > 0f || d3 > 0f;

        return !(negative && positive);
    }

    private void Draw()
    {
        EnsureShape();

        if (!overlay || !validShape)
        {
            DestroyDrawing();
            return;
        }

        if (!drawing)
        {
            // Made while playing or in the editor, and never saved or shown in the hierarchy.
            drawing = new GameObject(OverlayName) { hideFlags = HideFlags.HideAndDontSave };
            drawing.transform.SetParent(transform, false);
            drawing.AddComponent<MeshFilter>();
            drawing.AddComponent<MeshRenderer>();
            drawnVersion = -1;
        }

        var meshRenderer = drawing.GetComponent<MeshRenderer>();

        if (!material)
        {
            var shader = Shader.Find("Sprites/Default");

            if (!shader)
            {
                return;
            }

            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        if (!mesh)
        {
            mesh = new Mesh { name = "Liquid Overlay", hideFlags = HideFlags.HideAndDontSave };
            drawnVersion = -1;
        }

        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingOrder = sortingOrder;
        drawing.GetComponent<MeshFilter>().sharedMesh = mesh;

        // Not full, the overlay is cut by the surface, which is level in the world, so it's made again whenever the area could have moved.
        if (drawnVersion != shapeVersion || drawnSprite != overlay || drawnStyle != overlayStyle || drawnTint != tint
            || drawnTransparency != transparency || drawnLevel != Level || Level < 1f)
        {
            BuildMesh();
            drawnLevel = Level;
            drawnVersion = shapeVersion;
            drawnSprite = overlay;
            drawnStyle = overlayStyle;
            drawnTint = tint;
            drawnTransparency = transparency;
        }
    }

    private readonly List<Vector2> clipFrom = new List<Vector2>();
    private readonly List<Vector2> clipTo = new List<Vector2>();

    private void BuildMesh()
    {
        var texture = overlay.texture;
        material.mainTexture = texture;

        var colour = tint;
        colour.a *= 1f - Mathf.Clamp01(transparency / 100f);

        var picture = overlay.rect;
        var tileWidth = Mathf.Max(0.0001f, picture.width / overlay.pixelsPerUnit);
        var tileHeight = Mathf.Max(0.0001f, picture.height / overlay.pixelsPerUnit);
        var window = overlay.textureRect;

        if (overlayStyle == OverlayStyle.Tile && texture.wrapMode != TextureWrapMode.Repeat)
        {
            Debug.LogWarning("The overlay on " + name + " is set to Tile, but its texture's Wrap Mode isn't Repeat, so it won't repeat.", this);
        }

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colours = new List<Color>();
        var indexes = new List<int>();

        // A vertex at a point of the outline, with where it is in the picture. The picture stays where it is on the area as the level
        // goes up and down, so the level uncovers it, and doesn't squash it.
        int Add(Vector2 point)
        {
            vertices.Add(new Vector3(point.x, point.y, 0f));
            colours.Add(colour);

            if (overlayStyle == OverlayStyle.Tile)
            {
                uvs.Add(new Vector2(point.x / tileWidth, point.y / tileHeight));
            }
            else
            {
                var across = localBounds.width > 0f ? (point.x - localBounds.xMin) / localBounds.width : 0f;
                var up = localBounds.height > 0f ? (point.y - localBounds.yMin) / localBounds.height : 0f;

                uvs.Add(new Vector2(
                    (window.xMin + across * window.width) / texture.width,
                    (window.yMin + up * window.height) / texture.height));
            }

            return vertices.Count - 1;
        }

        var level = Level;

        if (level >= 1f)
        {
            foreach (var point in points)
            {
                Add(point);
            }

            indexes.AddRange(triangles);
        }
        else if (level > 0f)
        {
            // Each triangle of the outline, cut by the surface: what's under it is kept.
            var surface = SurfaceY;

            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                clipFrom.Clear();
                clipFrom.Add(points[triangles[i]]);
                clipFrom.Add(points[triangles[i + 1]]);
                clipFrom.Add(points[triangles[i + 2]]);

                ClipBelow(surface);

                if (clipTo.Count < 3)
                {
                    continue;
                }

                var first = Add(clipTo[0]);

                for (var k = 1; k + 1 < clipTo.Count; k++)
                {
                    indexes.Add(first);
                    indexes.Add(Add(clipTo[k]));
                    indexes.Add(Add(clipTo[k + 1]));
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colours);
        mesh.SetTriangles(indexes, 0);
        mesh.RecalculateBounds();
    }

    // Cuts the shape in clipFrom (in the area's own space) to what is under a level in the world, into clipTo: each side that crosses
    // the level is cut where it does.
    private void ClipBelow(float surface)
    {
        clipTo.Clear();

        for (var i = 0; i < clipFrom.Count; i++)
        {
            var current = clipFrom[i];
            var previous = clipFrom[(i + clipFrom.Count - 1) % clipFrom.Count];
            var currentY = transform.TransformPoint(current).y;
            var previousY = transform.TransformPoint(previous).y;
            var currentUnder = currentY <= surface;
            var previousUnder = previousY <= surface;

            if (currentUnder != previousUnder)
            {
                var across = (surface - previousY) / (currentY - previousY);
                clipTo.Add(Vector2.Lerp(previous, current, across));
            }

            if (currentUnder)
            {
                clipTo.Add(current);
            }
        }
    }

    // Overlays left behind by an earlier version of this component, such as before the scripts were recompiled.
    private void RemoveStrayOverlays()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);

            if (child.name == OverlayName && (child.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                DestroyObject(child.gameObject);
            }
        }
    }

    private void DestroyDrawing()
    {
        if (drawing)
        {
            DestroyObject(drawing);
        }

        if (mesh)
        {
            DestroyObject(mesh);
        }

        if (material)
        {
            DestroyObject(material);
        }

        drawing = null;
        mesh = null;
        material = null;
        drawnVersion = -1;
    }

    private static void DestroyObject(Object thing)
    {
        if (Application.isPlaying)
        {
            Destroy(thing);
        }
        else
        {
            DestroyImmediate(thing);
        }
    }

    // The outline, in the scene view: faint always, and stronger when it's selected.
    void OnDrawGizmos()
    {
        DrawOutline(new Color(0.2f, 0.7f, 1f, 0.5f));
    }

    void OnDrawGizmosSelected()
    {
        DrawOutline(new Color(0.2f, 0.7f, 1f, 1f));
    }

    private void DrawOutline(Color colour)
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = colour;

        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            Gizmos.DrawLine(points[j], points[i]);
        }

        // How far down the tide takes the surface at its lowest, across the area, when it has one.
        if (tideRange > 0f)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(colour.r, colour.g, colour.b, colour.a * 0.6f);

            var rect = WorldRect;
            var low = Mathf.Lerp(rect.yMin, rect.yMax, fill * (1f - tideRange / 100f));

            Gizmos.DrawLine(new Vector3(rect.xMin, low, 0f), new Vector3(rect.xMax, low, 0f));
        }
    }
}
