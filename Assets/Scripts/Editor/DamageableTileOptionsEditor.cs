using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The inspector for the options of a damageable tile, and of a section of them. It is laid out by what damages the tile:
// each kind is a tick box, and the settings for it appear under it when it is ticked. The sides it can be damaged from are a
// square with a clickable bar along each side, and only appear for the kinds of damage that depend on where they come
// from. The settings that belong to the component itself come last.
[CustomEditor(typeof(DamageableTileOptions), true)]
public class DamageableTileOptionsEditor : Editor
{
    // Everything this draws itself, so that what is left over can be drawn after it.
    private static readonly HashSet<string> Drawn = new HashSet<string>
    {
        "m_Script",
        "roll", /*"wood",*/ "standOn", "touch", "dig", "fire", "dynamite",
        "rollSpeedNeeded", "standOnTimeNeeded", "dynamiteResult", "dynamiteMass", "dynamiteForceNeeded",
        "standOnWeightNeeded", "standOnWeightsAdd", "touchTimeNeeded",
        "crumbleWarning", "crumbleWarningType", "warningDarkness", "warningShake",
        "fireIgniteSeconds", "fireSpreadSeconds", "fireBurnSeconds", "fireCharAmount", "fireFlames",
        "fireWarning", "fireGlowAmount", "fireReach", "fireCoolSeconds", "fireIntensityNeeded", "fireBurnIntensity", "digHardness", "digRadius",
        "All", "Up", "Down", "Left", "Right",
        "timeStayDestroyed",
        "breakEffect", "breakParticles", "breakSound",
        "debrisMinSpeed", "debrisMaxSpeed", "debrisSpread", "debrisImpactShare", "debrisSpin", "debrisGravityScale", "debrisSeconds"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        EditorGUILayout.LabelField("Damaged By", EditorStyles.boldLabel);

        DamageType("roll", "rollSpeedNeeded");
        //DamageType("wood");
        DrawStandOn();
        DamageType("touch", "touchTimeNeeded");
        DamageType("dig", "digHardness", "digRadius");
        DrawFire();
        DrawDynamite();

        // Dynamite and fire don't care which side they are on, and standing on a tile is always the top of it. The rest are
        // things that come from somewhere.
        if (IsOn("roll") || /*IsOn("wood") ||*/ IsOn("touch") || IsOn("dig"))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("From Which Sides", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Click the sides it can be damaged from.", EditorStyles.miniLabel);

            DrawSides();
        }

        // The kinds that wait to crumble can show it coming, and digging shows how far through it is the same way.
        if (IsOn("standOn") || IsOn("touch") || IsOn("dig"))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Before It Crumbles", EditorStyles.boldLabel);

            DrawCrumbleWarning();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("When Destroyed", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeStayDestroyed"));

        // What shows as it breaks. The settings are for the choice, so only that choice's are shown.
        var effect = serializedObject.FindProperty("breakEffect");
        EditorGUILayout.PropertyField(effect);

        // A blast that breaks a tile shows nothing but what is chosen here, so with none chosen it just vanishes.
        if (effect.enumValueIndex == (int)DamageableTileOptions.BreakEffect.None && IsOn("dynamite") &&
            serializedObject.FindProperty("dynamiteResult").enumValueIndex == (int)DamageableTileOptions.DynamiteResult.Break)
        {
            EditorGUILayout.HelpBox(
                "Dynamite is set to Break, and Break Effect is None, so a blasted tile just vanishes. Choose Particles or " +
                "Debris to show it breaking, or set Dynamite to Fly.",
                MessageType.Info);
        }

        EditorGUI.indentLevel++;

        if (effect.enumValueIndex == (int)DamageableTileOptions.BreakEffect.Particles)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("breakParticles"));
        }

        // A block blown loose by a blast falls as debris does, so it uses the same gravity, spin, and time.
        var flies = IsOn("dynamite") &&
                    serializedObject.FindProperty("dynamiteResult").enumValueIndex == (int)DamageableTileOptions.DynamiteResult.Fly;

        var debris = effect.enumValueIndex == (int)DamageableTileOptions.BreakEffect.Debris;

        if (debris || flies)
        {
            // How the pieces are thrown is for debris. The block that is blown loose is thrown by the blast itself.
            if (debris)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisMinSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisMaxSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisSpread"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisImpactShare"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisSpin"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisGravityScale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisSeconds"));
        }

        EditorGUI.indentLevel--;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("breakSound"));

        DrawRemaining();

        serializedObject.ApplyModifiedProperties();
    }

    // The block's mass is only for a tile that flies, and the debris settings below are for a tile that flies or breaks into
    // debris, so only the settings for what it does are shown.
    private void DrawDynamite()
    {
        var toggle = serializedObject.FindProperty("dynamite");
        EditorGUILayout.PropertyField(toggle);

        if (!toggle.boolValue)
        {
            return;
        }

        EditorGUI.indentLevel++;

        var result = serializedObject.FindProperty("dynamiteResult");
        EditorGUILayout.PropertyField(result);

        if (result.enumValueIndex == (int)DamageableTileOptions.DynamiteResult.Fly)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dynamiteMass"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("dynamiteForceNeeded"));

        EditorGUI.indentLevel--;
    }

    private bool IsOn(string flag)
    {
        return serializedObject.FindProperty(flag).boolValue;
    }

    private static readonly Color SideOn = new Color(0.25f, 0.7f, 0.35f);
    private static readonly Color SideOff = new Color(0.3f, 0.3f, 0.3f);

    // A square for the tile, with a bar along each side that is clicked to turn that side on or off. A side that is on is
    // green. A tile that is set to be damaged from every side shows all four on, whatever its separate sides say, and
    // clicking one gives it the four sides to go by instead.
    private void DrawSides()
    {
        const float width = 160f;
        const float height = 100f;
        const float bar = 22f;
        const float gap = 3f;

        var all = serializedObject.FindProperty("All");
        var up = serializedObject.FindProperty("Up");
        var down = serializedObject.FindProperty("Down");
        var left = serializedObject.FindProperty("Left");
        var right = serializedObject.FindProperty("Right");

        var row = EditorGUILayout.GetControlRect(false, height);
        var area = new Rect(row.x + EditorGUIUtility.labelWidth, row.y, Mathf.Min(width, row.width - EditorGUIUtility.labelWidth), height);

        var middle = new Rect(area.x + bar + gap, area.y + bar + gap, area.width - 2f * (bar + gap), area.height - 2f * (bar + gap));
        var tile = new Rect(middle.x, middle.y, middle.width, middle.height);

        var upRect = new Rect(middle.x, area.y, middle.width, bar);
        var downRect = new Rect(middle.x, area.yMax - bar, middle.width, bar);
        var leftRect = new Rect(area.x, middle.y, bar, middle.height);
        var rightRect = new Rect(area.xMax - bar, middle.y, bar, middle.height);

        EditorGUI.DrawRect(tile, new Color(0.2f, 0.2f, 0.2f));
        GUI.Label(tile, "Tile", CenteredLabel);

        var sides = new[]
        {
            all.boolValue || up.boolValue,
            all.boolValue || down.boolValue,
            all.boolValue || left.boolValue,
            all.boolValue || right.boolValue
        };

        var changed = SideBar(upRect, "Up", ref sides[0]);
        changed |= SideBar(downRect, "Down", ref sides[1]);
        changed |= SideBar(leftRect, "L", ref sides[2], "Left");
        changed |= SideBar(rightRect, "R", ref sides[3], "Right");

        if (changed)
        {
            all.boolValue = false;
            up.boolValue = sides[0];
            down.boolValue = sides[1];
            left.boolValue = sides[2];
            right.boolValue = sides[3];
        }

        // Two buttons under the square, to turn on every side or turn them all off in one click.
        var buttons = EditorGUILayout.GetControlRect();
        var buttonWidth = (area.width - gap) * 0.5f;
        var allButton = new Rect(area.x, buttons.y, buttonWidth, buttons.height);
        var clearButton = new Rect(allButton.xMax + gap, buttons.y, buttonWidth, buttons.height);

        if (GUI.Button(allButton, new GUIContent("All", "Damaged from every side.")))
        {
            SetSides(true);
        }

        if (GUI.Button(clearButton, new GUIContent("Clear", "Damaged from no side.")))
        {
            SetSides(false);
        }
    }

    // Every side on or every side off, whatever way it was set before.
    private void SetSides(bool on)
    {
        serializedObject.FindProperty("All").boolValue = false;
        serializedObject.FindProperty("Up").boolValue = on;
        serializedObject.FindProperty("Down").boolValue = on;
        serializedObject.FindProperty("Left").boolValue = on;
        serializedObject.FindProperty("Right").boolValue = on;
    }

    private static GUIStyle centeredLabel;

    private static GUIStyle CenteredLabel
    {
        get
        {
            if (centeredLabel == null)
            {
                centeredLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            }

            return centeredLabel;
        }
    }

    // One side of the square. Returns whether it was clicked, and if so flips whether it is on.
    private static bool SideBar(Rect rect, string text, ref bool on, string tooltip = null)
    {
        EditorGUI.DrawRect(rect, on ? SideOn : SideOff);
        GUI.Label(rect, new GUIContent(text, tooltip ?? text), CenteredLabel);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            on = !on;
            return true;
        }

        return false;
    }

    // Stand On has settings of its own under it, and the warning has some under that, for the kinds of warning that use them.
    private void DrawStandOn()
    {
        var standOn = serializedObject.FindProperty("standOn");
        EditorGUILayout.PropertyField(standOn);

        if (!standOn.boolValue)
        {
            return;
        }

        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("standOnTimeNeeded"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("standOnWeightNeeded"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("standOnWeightsAdd"));

        EditorGUI.indentLevel--;
    }

    // Fire has its times, and a warning of its own, with the amount it glows under that.
    private void DrawFire()
    {
        var fire = serializedObject.FindProperty("fire");
        EditorGUILayout.PropertyField(fire);

        if (!fire.boolValue)
        {
            return;
        }

        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireIntensityNeeded"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireReach"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireIgniteSeconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireCoolSeconds"));

        var warning = serializedObject.FindProperty("fireWarning");
        EditorGUILayout.PropertyField(warning);

        if (warning.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fireGlowAmount"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireSpreadSeconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireBurnIntensity"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireBurnSeconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireCharAmount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fireFlames"));

        EditorGUI.indentLevel--;
    }

    // The warning that it is about to give way, which Stand On and Touch share, with the settings for the kinds of warning
    // that use them.
    private void DrawCrumbleWarning()
    {
        var warning = serializedObject.FindProperty("crumbleWarning");
        EditorGUILayout.PropertyField(warning);

        if (!warning.boolValue)
        {
            return;
        }

        EditorGUI.indentLevel++;

        var type = serializedObject.FindProperty("crumbleWarningType");
        EditorGUILayout.PropertyField(type);

        var kind = (DamageableTileOptions.WarningType)type.enumValueIndex;

        if (kind != DamageableTileOptions.WarningType.Shake)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("warningDarkness"));
        }

        if (kind != DamageableTileOptions.WarningType.Darken)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("warningShake"));
        }

        EditorGUI.indentLevel--;
    }

    // A tick box for a kind of damage, with its settings under it while it is ticked.
    private void DamageType(string flag, params string[] settings)
    {
        var toggle = serializedObject.FindProperty(flag);
        EditorGUILayout.PropertyField(toggle);

        if (!toggle.boolValue)
        {
            return;
        }

        EditorGUI.indentLevel++;

        foreach (var setting in settings)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(setting));
        }

        EditorGUI.indentLevel--;
    }

    // The settings that the component adds to the options, drawn as they would be anyway.
    private void DrawRemaining()
    {
        var property = serializedObject.GetIterator();
        var heading = false;

        for (var enter = true; property.NextVisible(enter); enter = false)
        {
            if (Drawn.Contains(property.name))
            {
                continue;
            }

            if (!heading)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("This Component", EditorStyles.boldLabel);
                heading = true;
            }

            EditorGUILayout.PropertyField(property, true);
        }
    }
}
