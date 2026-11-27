using UnityEditor;
using UnityEngine;

// Sets the Balloon's Movement Profile to how he's meant to move: falling very slowly while he's left alone, and, with the action held, inflating and
// rising, so the player steers him up and down. It changes the profile asset the proper way (not by editing its file), so it's safe with Unity open.
// The numbers are only a start: tune them in the profile.
public static class BalloonMovementMenu
{
    private const string ProfilePath = "Assets/Movement/Profiles/Balloon.asset";

    [MenuItem("Tools/Movement/Set Up Balloon Floating")]
    private static void SetUpBalloon()
    {
        var profile = AssetDatabase.LoadAssetAtPath<MovementProfile>(ProfilePath);

        if (!profile)
        {
            EditorUtility.DisplayDialog("Set Up Balloon Floating", $"There's no Movement Profile at {ProfilePath}. Run Tools > Movement > Create Profiles For Characters first.", "OK");
            return;
        }

        Undo.RecordObject(profile, "Set Up Balloon Floating");

        // Left alone: no jump, a very slow fall, and a slow drift sideways that's slow to stop, like something filled with air.
        profile.overrides.topSpeed = 2f;
        profile.overrides.timeToTopSpeed = 2f;
        profile.overrides.timeToStop = 5f;
        profile.overrides.jumpHeight = 0f;
        profile.overrides.fallGravity = 0.15f;
        profile.overrides.terminalSpeed = 1.5f;
        profile.overrides.jumpCut = 1f;

        // With the action held he's let air in and rises. Much lighter than the air, so what lifts him is more than his string weighs (the string
        // is a chain of heavy-ish pieces hung from him, and it has to be lifted too), and no faster than the terminal speed times its multiple.
        var mode = profile.FindMode("float");

        if (mode == MovementMode.Plain)
        {
            mode = new MovementMode { name = "float" };
            profile.modes.Add(mode);
        }

        mode.speed = 0.75f;
        mode.acceleration = 0.7f;
        mode.braking = 1f;
        mode.jumpHeight = 1f;
        mode.riseGravity = 1f;
        mode.fallGravity = 1f;
        mode.terminalSpeed = 2f;
        mode.density = 0.1f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);

        Debug.Log("The Balloon's profile is set up: left alone he falls at most 1.5 tiles a second and can't jump; holding the action he rises at up to 3. " +
                  "Tune it in the profile: Overrides (Terminal Speed, Fall Gravity, Top Speed) and the float mode (Density, Terminal Speed).", profile);
    }
}
