using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Every character's movement side by side: what it's made of, what it weighs, and what that comes to, so the odd ones out and
// the ones that don't go in the order they should show up at a glance. The weight, the material and the traits can be changed
// here; the rest is worked out from them. Changes while playing are kept.
public class MovementOverviewWindow : EditorWindow
{
    // The columns, left to right. Each has the least width it can be read at, and a share of whatever room is left over when the
    // window is wider than that, so the table grows to fit the window and the headings can be read.
    private struct Column
    {
        public string label;
        public string tip;
        public string sortKey;
        public float min;
        public float weight;

        public Column(string label, string tip, string sortKey, float min, float weight)
        {
            this.label = label;
            this.tip = tip;
            this.sortKey = sortKey;
            this.min = min;
            this.weight = weight;
        }
    }

    private const int NameColumn = 0;
    private const int MaterialColumn = 1;
    private const int MassColumn = 2;
    private const int RunColumn = 3;
    private const int AccelColumn = 4;
    private const int JumpTraitColumn = 5;
    private const int FloatColumn = 6;
    private const int SpeedColumn = 7;
    private const int ToTopColumn = 8;
    private const int StopColumn = 9;
    private const int JumpHeightColumn = 10;
    private const int JumpDistanceColumn = 11;
    private const int FallColumn = 12;

    private static readonly Column[] Columns =
    {
        new Column("Character", "The character's movement profile. Click a name to select it.", null, 100f, 1.2f),
        new Column("Material", "What it is made of. Its density decides whether it floats.", null, 110f, 1.2f),
        new Column("Mass", "How much it weighs. Heavier is slower to start, slower to stop, and jumps lower. Editable.", "mass", 52f, 0.6f),
        new Column("Run speed", "Trait: how fast it runs for its weight. 1 is usual. Editable.", null, 52f, 0.5f),
        new Column("Accel- eration", "Trait: how quickly it gets going and stops, for its weight. 1 is usual. Editable.", null, 52f, 0.5f),
        new Column("Jump trait", "Trait: how high it jumps for its weight. 1 is usual. Editable.", null, 52f, 0.5f),
        new Column("Float trait", "Trait: how floaty its fall is, for its weight. 1 is usual. Editable.", null, 52f, 0.5f),
        new Column("Top speed (tiles/s)", "How fast it runs flat out, in tiles per second.", "speed", 70f, 0.8f),
        new Column("Time to top speed (s)", "How long it takes to reach top speed from standing, in seconds.", "start", 78f, 0.8f),
        new Column("Stopping distance (tiles)", "How far it slides from top speed once let go, in tiles.", "stop", 82f, 0.8f),
        new Column("Jump height (tiles)", "How high a full jump goes, in tiles.", "jump", 70f, 0.8f),
        new Column("Jump distance (tiles)", "How far a full jump goes at top speed, in tiles.", "distance", 74f, 0.8f),
        new Column("Fall pull (x gravity)", "How hard it is pulled down when falling, as a multiple of gravity.", "fall", 70f, 0.8f),
    };

    // A liquid has a column of its own after these.
    private const float FluidMin = 120f;
    private const float FluidWeight = 1f;

    private const float ColumnGap = 4f;
    private const float HeaderHeight = 44f;

    private float[] widths = new float[Columns.Length];
    private float fluidWidth = FluidMin;
    private GUIStyle headerLabel;
    private GUIStyle headerButton;

    private Vector2 scroll;
    private string sortBy = "mass";
    private bool sortDescending;
    private readonly List<MovementProfile> profiles = new List<MovementProfile>();
    private readonly List<Fluid> fluids = new List<Fluid>();
    private CharacterMaterial[] materials = new CharacterMaterial[0];
    private string[] materialNames = new string[0];
    private readonly ResolvedMovement result = new ResolvedMovement();

    [MenuItem("Tools/Movement/Overview")]
    public static void Open()
    {
        GetWindow<MovementOverviewWindow>("Movement").minSize = new Vector2(420f, 200f);
    }

    void OnEnable()
    {
        Refresh();
    }

    void OnFocus()
    {
        Refresh();
    }

    private void Refresh()
    {
        profiles.Clear();
        fluids.Clear();

        foreach (var guid in AssetDatabase.FindAssets("t:MovementProfile"))
        {
            profiles.Add(AssetDatabase.LoadAssetAtPath<MovementProfile>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        fluids.AddRange(Fluid.Presets);

        materials = AssetDatabase.FindAssets("t:CharacterMaterial")
            .Select(guid => AssetDatabase.LoadAssetAtPath<CharacterMaterial>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(m => m.density)
            .ToArray();

        materialNames = materials.Select(m => $"{m.name} ({m.density:0.##})").ToArray();
    }

    void OnGUI()
    {
        if (profiles.Count == 0)
        {
            EditorGUILayout.HelpBox("There are no movement profiles yet. Use Tools > Movement > Create Default Assets, then Create Profiles For Characters.", MessageType.Info);

            if (GUILayout.Button("Look again"))
            {
                Refresh();
            }

            return;
        }

        var tuning = MovementTuning.Current;

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Tuning", EditorStyles.toolbarButton, GUILayout.Width(60f)))
        {
            Selection.activeObject = tuning;
        }

        GUILayout.Label("Click a heading to sort by it.", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
        {
            Refresh();
        }

        EditorGUILayout.EndHorizontal();

        // Computed once for the sort and the rows.
        var rows = profiles
            .Where(p => p)
            .Select(p =>
            {
                var r = new ResolvedMovement();
                p.Resolve("base", null, tuning, r);
                return (profile: p, r);
            })
            .ToList();

        rows.Sort((a, b) =>
        {
            var compare = Key(a.profile, a.r).CompareTo(Key(b.profile, b.r));
            return sortDescending ? -compare : compare;
        });

        LayoutColumns();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawHeader();

        foreach (var row in rows)
        {
            DrawRow(row.profile, row.r, tuning);
        }

        EditorGUILayout.EndScrollView();
    }

    private float Key(MovementProfile p, ResolvedMovement r)
    {
        switch (sortBy)
        {
            case "mass": return p.mass;
            case "density": return p.Density;
            case "speed": return r.maxMoveSpeed;
            case "start": return r.timeToTopSpeed;
            case "stop": return r.stopDistance;
            case "jump": return r.jumpHeight;
            case "distance": return r.jumpDistance;
            case "fall": return r.fallAcceleration;
            default: return 0f;
        }
    }

    // Works out how wide every column is: its least, and its share of the room the window has left over.
    private void LayoutColumns()
    {
        var minTotal = fluids.Count * FluidMin;
        var weightTotal = fluids.Count * FluidWeight;

        foreach (var column in Columns)
        {
            minTotal += column.min;
            weightTotal += column.weight;
        }

        var count = Columns.Length + fluids.Count;

        // What's left of the window's width, less the scroll bar, the margins and the gaps between columns.
        var spare = Mathf.Max(0f, position.width - 30f - count * ColumnGap - minTotal);

        for (var i = 0; i < Columns.Length; i++)
        {
            widths[i] = Columns[i].min + spare * Columns[i].weight / weightTotal;
        }

        fluidWidth = FluidMin + spare * FluidWeight / weightTotal;
    }

    private void MakeStyles()
    {
        if (headerLabel != null)
        {
            return;
        }

        headerLabel = new GUIStyle(EditorStyles.miniBoldLabel) { wordWrap = true, alignment = TextAnchor.LowerLeft };
        headerButton = new GUIStyle(EditorStyles.miniButton) { wordWrap = true };
    }

    // A heading, that wraps onto a second or third line instead of being cut off, with a tip saying what it means. A heading that
    // has a key can be clicked to sort by it.
    private void Heading(int column)
    {
        var spec = Columns[column];
        var content = new GUIContent(spec.label.Replace("- ", ""), spec.tip);

        if (spec.sortKey == null)
        {
            GUILayout.Label(content, headerLabel, GUILayout.Width(widths[column]), GUILayout.Height(HeaderHeight));
            return;
        }

        if (sortBy == spec.sortKey)
        {
            content.text += sortDescending ? "  v" : "  ^";
        }

        if (GUILayout.Button(content, headerButton, GUILayout.Width(widths[column]), GUILayout.Height(HeaderHeight)))
        {
            if (sortBy == spec.sortKey)
            {
                sortDescending = !sortDescending;
            }
            else
            {
                sortBy = spec.sortKey;
                sortDescending = false;
            }
        }
    }

    private void DrawHeader()
    {
        MakeStyles();

        EditorGUILayout.BeginHorizontal();

        for (var i = 0; i < Columns.Length; i++)
        {
            Heading(i);
        }

        foreach (var fluid in fluids)
        {
            GUILayout.Label(
                new GUIContent(fluid.name + " (in it)", "Whether it floats or sinks in " + fluid.name + ", and how much of it is under, or how fast it sinks."),
                headerLabel,
                GUILayout.Width(fluidWidth),
                GUILayout.Height(HeaderHeight));
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(MovementProfile profile, ResolvedMovement r, MovementTuning tuning)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(profile.name, EditorStyles.label, GUILayout.Width(widths[NameColumn])))
        {
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        EditorGUI.BeginChangeCheck();

        var materialIndex = System.Array.IndexOf(materials, profile.material);
        var newMaterial = EditorGUILayout.Popup(materialIndex, materialNames, GUILayout.Width(widths[MaterialColumn]));
        var mass = EditorGUILayout.FloatField(profile.mass, GUILayout.Width(widths[MassColumn]));
        var run = EditorGUILayout.FloatField(profile.traits.runSpeed, GUILayout.Width(widths[RunColumn]));
        var accel = EditorGUILayout.FloatField(profile.traits.acceleration, GUILayout.Width(widths[AccelColumn]));
        var jump = EditorGUILayout.FloatField(profile.traits.jump, GUILayout.Width(widths[JumpTraitColumn]));
        var floatiness = EditorGUILayout.FloatField(profile.traits.floatiness, GUILayout.Width(widths[FloatColumn]));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(profile, "Change movement");

            if (newMaterial >= 0 && newMaterial < materials.Length)
            {
                profile.material = materials[newMaterial];
            }

            profile.mass = Mathf.Max(0.01f, mass);
            profile.traits.runSpeed = Mathf.Max(run, 0.2f);
            profile.traits.acceleration = Mathf.Max(accel, 0.2f);
            profile.traits.jump = Mathf.Max(jump, 0.2f);
            profile.traits.floatiness = Mathf.Max(floatiness, 0.2f);

            EditorUtility.SetDirty(profile);
            profile.Resolve("base", null, tuning, r);
        }

        GUILayout.Label($"{r.maxMoveSpeed:0.0}", GUILayout.Width(widths[SpeedColumn]));
        GUILayout.Label($"{r.timeToTopSpeed:0.00}", GUILayout.Width(widths[ToTopColumn]));
        GUILayout.Label($"{r.stopDistance:0.00}", GUILayout.Width(widths[StopColumn]));
        GUILayout.Label($"{r.jumpHeight:0.0}", GUILayout.Width(widths[JumpHeightColumn]));
        GUILayout.Label($"{r.jumpDistance:0.0}", GUILayout.Width(widths[JumpDistanceColumn]));
        GUILayout.Label($"{r.fallAcceleration:0.0}", GUILayout.Width(widths[FallColumn]));

        foreach (var fluid in fluids)
        {
            profile.DescribeIn(fluid, tuning, "base", out var floats, out var under, out var speed);

            GUILayout.Label(floats ? $"floats ({under * 100f:0}% under)" : $"sinks {speed:0.0}/s", GUILayout.Width(fluidWidth));
        }

        EditorGUILayout.EndHorizontal();
    }
}
