using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Every character's movement side by side: what it's made of, what it weighs, and what that comes to, so the odd ones out and
// the ones that don't go in the order they should show up at a glance. The weight, the material and the traits can be changed
// here; the rest is worked out from them. Changes while playing are kept.
public class MovementOverviewWindow : EditorWindow
{
    private const float NameWidth = 120f;
    private const float EditWidth = 56f;
    private const float ReadWidth = 62f;

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
        GetWindow<MovementOverviewWindow>("Movement").minSize = new Vector2(760f, 200f);
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

        foreach (var guid in AssetDatabase.FindAssets("t:Fluid"))
        {
            fluids.Add(AssetDatabase.LoadAssetAtPath<Fluid>(AssetDatabase.GUIDToAssetPath(guid)));
        }

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

    private void HeaderButton(string label, string key, float width)
    {
        var text = sortBy == key ? label + (sortDescending ? " v" : " ^") : label;

        if (GUILayout.Button(text, EditorStyles.miniButton, GUILayout.Width(width)))
        {
            if (sortBy == key)
            {
                sortDescending = !sortDescending;
            }
            else
            {
                sortBy = key;
                sortDescending = false;
            }
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Character", EditorStyles.miniBoldLabel, GUILayout.Width(NameWidth));
        GUILayout.Label("Material", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
        HeaderButton("Mass", "mass", EditWidth);
        GUILayout.Label("Run", EditorStyles.miniBoldLabel, GUILayout.Width(EditWidth - 12f));
        GUILayout.Label("Accel", EditorStyles.miniBoldLabel, GUILayout.Width(EditWidth - 12f));
        GUILayout.Label("Jump", EditorStyles.miniBoldLabel, GUILayout.Width(EditWidth - 12f));
        GUILayout.Label("Float", EditorStyles.miniBoldLabel, GUILayout.Width(EditWidth - 12f));
        HeaderButton("Speed", "speed", ReadWidth);
        HeaderButton("To top s", "start", ReadWidth);
        HeaderButton("Stop tiles", "stop", ReadWidth);
        HeaderButton("Jump tiles", "jump", ReadWidth);
        HeaderButton("Jump long", "distance", ReadWidth);
        HeaderButton("Fall x", "fall", ReadWidth);

        foreach (var fluid in fluids)
        {
            GUILayout.Label(fluid.name, EditorStyles.miniBoldLabel, GUILayout.Width(110f));
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(MovementProfile profile, ResolvedMovement r, MovementTuning tuning)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(profile.name, EditorStyles.label, GUILayout.Width(NameWidth)))
        {
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        EditorGUI.BeginChangeCheck();

        var materialIndex = System.Array.IndexOf(materials, profile.material);
        var newMaterial = EditorGUILayout.Popup(materialIndex, materialNames, GUILayout.Width(110f));
        var mass = EditorGUILayout.FloatField(profile.mass, GUILayout.Width(EditWidth));
        var run = EditorGUILayout.FloatField(profile.traits.runSpeed, GUILayout.Width(EditWidth - 12f));
        var accel = EditorGUILayout.FloatField(profile.traits.acceleration, GUILayout.Width(EditWidth - 12f));
        var jump = EditorGUILayout.FloatField(profile.traits.jump, GUILayout.Width(EditWidth - 12f));
        var floatiness = EditorGUILayout.FloatField(profile.traits.floatiness, GUILayout.Width(EditWidth - 12f));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(profile, "Change movement");

            if (newMaterial >= 0 && newMaterial < materials.Length)
            {
                profile.material = materials[newMaterial];
            }

            profile.mass = Mathf.Max(0.01f, mass);
            profile.traits.runSpeed = Mathf.Clamp(run, 0.2f, 3f);
            profile.traits.acceleration = Mathf.Clamp(accel, 0.2f, 3f);
            profile.traits.jump = Mathf.Clamp(jump, 0.2f, 3f);
            profile.traits.floatiness = Mathf.Clamp(floatiness, 0.2f, 3f);

            EditorUtility.SetDirty(profile);
            profile.Resolve("base", null, tuning, r);
        }

        GUILayout.Label($"{r.maxMoveSpeed:0.0}", GUILayout.Width(ReadWidth));
        GUILayout.Label($"{r.timeToTopSpeed:0.00}", GUILayout.Width(ReadWidth));
        GUILayout.Label($"{r.stopDistance:0.00}", GUILayout.Width(ReadWidth));
        GUILayout.Label($"{r.jumpHeight:0.0}", GUILayout.Width(ReadWidth));
        GUILayout.Label($"{r.jumpDistance:0.0}", GUILayout.Width(ReadWidth));
        GUILayout.Label($"{r.fallAcceleration:0.0}", GUILayout.Width(ReadWidth));

        foreach (var fluid in fluids)
        {
            profile.DescribeIn(fluid, tuning, "base", out var floats, out var under, out var speed);

            GUILayout.Label(floats ? $"floats ({under * 100f:0}% under)" : $"sinks {speed:0.0}/s", GUILayout.Width(110f));
        }

        EditorGUILayout.EndHorizontal();
    }
}
