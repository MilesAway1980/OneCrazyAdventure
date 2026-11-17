using System.Linq;
using UnityEditor;
using UnityEngine;

// Tools > Movement > Movement Lab: starts the Lab in the scene that is open, and shows what it found. For each thing measured
// it shows what it really did, with what the formula said it should do in brackets, coloured by how far apart they are:
// green is within 15%, orange within 30%, and red further. Where they're far apart, either the formula isn't telling the whole
// story for that character (its shape, friction, a script of its own) or something is wrong, and that's worth a look.
public class MovementLabWindow : EditorWindow
{
    private const float NameWidth = 120f;
    private const float PassWidth = 48f;
    private const float CellWidth = 96f;

    private LabReport report;
    private Vector2 scroll;
    private float timeScale;
    private float bankHeight;

    [MenuItem("Tools/Movement/Movement Lab")]
    public static void Open()
    {
        GetWindow<MovementLabWindow>("Movement Lab").minSize = new Vector2(820f, 300f);
    }

    void OnEnable()
    {
        timeScale = EditorPrefs.GetFloat("MovementLab.TimeScale", 1f);
        bankHeight = EditorPrefs.GetFloat("MovementLab.BankHeight", 1f);
        report = LabReport.Load();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            report = LabReport.Load();
            Repaint();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Runs every character in the open scene through the same tests with the game's own physics, in lanes made for it high above " +
            "the level, and puts everything back after. Don't join with a keyboard or gamepad while it runs. It takes a couple of minutes.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        timeScale = EditorGUILayout.Slider(new GUIContent("Speed", "How fast it runs. Keep it at 1 for the most exact results."), timeScale, 1f, 4f);
        bankHeight = EditorGUILayout.FloatField(new GUIContent("Bank height", "How high above the water a bank is, in tiles, that a character has to get out onto."), bankHeight, GUILayout.MaxWidth(260f));

        if (EditorGUI.EndChangeCheck())
        {
            bankHeight = Mathf.Max(0f, bankHeight);
            EditorPrefs.SetFloat("MovementLab.TimeScale", timeScale);
            EditorPrefs.SetFloat("MovementLab.BankHeight", bankHeight);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Run the Lab in the open scene", GUILayout.Height(26f)))
            {
                EditorPrefs.SetBool("MovementLab.AutoRun", true);
                EditorApplication.EnterPlaymode();
            }
        }

        using (new EditorGUI.DisabledScope(report == null))
        {
            if (GUILayout.Button("Show the files", GUILayout.Height(26f), GUILayout.Width(120f)))
            {
                EditorUtility.RevealInFinder(LabReport.CsvPath);
            }
        }

        if (GUILayout.Button("Reload", GUILayout.Height(26f), GUILayout.Width(70f)))
        {
            report = LabReport.Load();
        }

        EditorGUILayout.EndHorizontal();

        if (report == null || report.entries.Count == 0)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("There are no results yet. Run the Lab.");
            return;
        }

        EditorGUILayout.LabelField($"Results from {report.when}. The bank was {report.bankHeight:0.0} tiles high. Measured, with the formula's in brackets.", EditorStyles.miniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawMovement();
        EditorGUILayout.Space(14f);
        DrawLiquids();

        EditorGUILayout.EndScrollView();
    }

    private void DrawMovement()
    {
        EditorGUILayout.LabelField("Running and jumping", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Character", EditorStyles.miniBoldLabel, GUILayout.Width(NameWidth));
        GUILayout.Label("Pass", EditorStyles.miniBoldLabel, GUILayout.Width(PassWidth));

        foreach (var title in new[] { "Top speed", "Time to top", "Stop tiles", "Jump height", "Tap jump", "Jump long", "Fall speed" })
        {
            GUILayout.Label(title, EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));
        }

        EditorGUILayout.EndHorizontal();

        foreach (var e in report.entries)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(e.character, GUILayout.Width(NameWidth));
            GUILayout.Label(e.pass == "base" ? "" : e.mode, GUILayout.Width(PassWidth));

            if (!e.moved)
            {
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = new Color(1f, 0.4f, 0.4f);
                GUILayout.Label("didn't run", style, GUILayout.Width(CellWidth));
            }
            else
            {
                Cell(e.topSpeed, e.expectedTopSpeed);
            }

            Cell(e.timeToTop, e.expectedTimeToTop, "0.00");
            Cell(e.stopDistance, e.expectedStopDistance, "0.00");
            Cell(e.jumpHeight, e.expectedJumpHeight);
            Cell(e.tapHeight, -1f);
            Cell(e.jumpDistance, e.expectedJumpDistance);
            Cell(e.terminalSpeed, e.expectedTerminalSpeed);

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawLiquids()
    {
        if (!report.entries.Any(e => e.fluids.Count > 0))
        {
            return;
        }

        EditorGUILayout.LabelField("In liquids", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Character", EditorStyles.miniBoldLabel, GUILayout.Width(NameWidth));
        GUILayout.Label("Liquid", EditorStyles.miniBoldLabel, GUILayout.Width(PassWidth + 20f));

        foreach (var title in new[] { "Settles", "Under", "Bobs", "Still after", "Sink speed", "Kick", "Gets out" })
        {
            GUILayout.Label(title, EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));
        }

        EditorGUILayout.EndHorizontal();

        foreach (var e in report.entries)
        {
            foreach (var f in e.fluids)
            {
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(e.character, GUILayout.Width(NameWidth));
                GUILayout.Label(f.fluid, GUILayout.Width(PassWidth + 20f));

                GUILayout.Label(f.floats ? "floats" : "sinks", GUILayout.Width(CellWidth));
                Cell(f.under * 100f, f.expectedUnder * 100f, "0");
                GUILayout.Label(f.floats ? f.bobs.ToString() : "", GUILayout.Width(CellWidth));
                GUILayout.Label(f.settleSeconds >= 0f ? $"{f.settleSeconds:0.0} s" : (f.floats ? "never still" : ""), GUILayout.Width(CellWidth));
                Cell(f.sinkSpeed, f.expectedSinkSpeed);
                Cell(f.kickRise, f.expectedKick);

                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = f.escaped ? new Color(0.4f, 0.85f, 0.4f) : new Color(1f, 0.4f, 0.4f);
                GUILayout.Label(f.escaped ? $"yes, {f.escapeSeconds:0.0} s" : "stuck", style, GUILayout.Width(CellWidth));

                EditorGUILayout.EndHorizontal();
            }
        }
    }

    // "measured (expected)", coloured by how far apart they are. Nothing measured, or nothing to compare, is left plain.
    private static void Cell(float measured, float expected, string format = "0.0")
    {
        var style = new GUIStyle(EditorStyles.label);

        if (measured < 0f)
        {
            GUILayout.Label("-", style, GUILayout.Width(CellWidth));
            return;
        }

        var text = measured.ToString(format);

        if (expected >= 0f)
        {
            var off = Mathf.Abs(measured - expected) / Mathf.Max(Mathf.Abs(expected), 0.5f);

            style.normal.textColor = off <= 0.15f ? new Color(0.4f, 0.85f, 0.4f) : off <= 0.3f ? new Color(1f, 0.7f, 0.25f) : new Color(1f, 0.4f, 0.4f);
            text += $"  ({expected.ToString(format)})";
        }

        GUILayout.Label(text, style, GUILayout.Width(CellWidth));
    }
}
