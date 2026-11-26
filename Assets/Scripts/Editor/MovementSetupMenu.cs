using System.Collections.Generic;
using System.IO;
using Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sets the movement system up: the shared assets (the tuning, the materials), a starting profile for every
// character in the open scenes, and the old per-character movement settings taken off. Run these with the scene open, in the
// editor, so the scene is changed the proper way rather than by editing its file.
public static class MovementSetupMenu
{
    private const string Root = "Assets/Movement";

    // How dense things are, compared with water, which is 1.
    private static readonly (string name, float density)[] Materials =
    {
        ("Balloon", 0.004f), // Filled with air: lighter than the air itself, when it's let fill.
        ("Foam", 0.1f),
        ("Cork", 0.25f),
        ("Fabric", 0.4f),
        ("Wood", 0.6f),
        ("Plastic", 1.05f),
        ("Rubber", 1.15f),
        ("Resin", 1.3f),
        ("Brick", 1.9f),
        ("Glass", 2.5f),
        ("Stone", 2.7f),
        ("Steel", 7.8f),
    };

    [MenuItem("Tools/Movement/Create Default Assets")]
    public static void CreateDefaultAssets()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(Root);
        EnsureFolder(Root + "/Materials");
        EnsureFolder(Root + "/Profiles");

        var created = 0;

        created += LoadOrCreate<MovementTuning>("Assets/Resources/MovementTuning.asset", null, out _);

        foreach (var (name, density) in Materials)
        {
            created += LoadOrCreate<CharacterMaterial>($"{Root}/Materials/{name}.asset", m => m.density = density, out _);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Movement: {created} new assets made, {Materials.Length + 1 - created} that were already there left alone.");
    }

    [MenuItem("Tools/Movement/Create Profiles For Characters")]
    public static void CreateProfilesForCharacters()
    {
        CreateDefaultAssets();

        var made = 0;
        var assigned = 0;

        foreach (var character in Object.FindObjectsByType<Character>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (character.movementProfile)
            {
                continue;
            }

            var path = $"{Root}/Profiles/{Sanitise(character.gameObject.name)}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<MovementProfile>(path);

            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<MovementProfile>();
                Seed(profile, character);
                AssetDatabase.CreateAsset(profile, path);
                made++;
            }

            var serialized = new SerializedObject(character);
            serialized.FindProperty("movementProfile").objectReferenceValue = profile;
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
            assigned++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Movement: {made} profiles made, {assigned} characters given one. " +
                  "Save the scene to keep them. Tools > Movement > Overview shows them side by side.");
    }

    // A profile is shown by the name stored inside it (the Overview does), which is set when it's made or renamed in Unity. Renamed outside
    // Unity, the file has the new name and the profile still has the old one. This puts them right, as Unity would have.
    [MenuItem("Tools/Movement/Fix Profile Names")]
    public static void FixProfileNames()
    {
        var fixedNames = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:MovementProfile", new[] { Root + "/Profiles" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var profile = AssetDatabase.LoadAssetAtPath<MovementProfile>(path);
            var fileName = Path.GetFileNameWithoutExtension(path);

            if (profile && profile.name != fileName)
            {
                Debug.Log($"Movement: the profile {path} was called \"{profile.name}\" inside. It is now \"{fileName}\".", profile);
                profile.name = fileName;
                EditorUtility.SetDirty(profile);
                fixedNames++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Movement: {fixedNames} profile names put right.");
    }

    [MenuItem("Tools/Movement/Remove Old Movement Settings")]
    public static void RemoveOldMovementSettings()
    {
        var removed = 0;
        var skipped = 0;

        foreach (var old in Object.FindObjectsByType<MovementSettings>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // On a prefab instance, the component belongs to the prefab: it has to come off there.
            if (PrefabUtility.IsPartOfPrefabInstance(old))
            {
                skipped++;
                continue;
            }

            var scene = old.gameObject.scene;
            Undo.DestroyObjectImmediate(old);
            EditorSceneManager.MarkSceneDirty(scene);
            removed++;
        }

        Debug.Log($"Movement: {removed} old movement settings removed" +
                  (skipped > 0 ? $", {skipped} left because they're part of a prefab (take them off the prefab itself)." : "."));
    }

    // A starting point for a character, by what it is. These are guesses to tune from, not decisions.
    private static void Seed(MovementProfile profile, Character character)
    {
        var body = character.GetComponent<Rigidbody2D>();
        profile.mass = body ? Mathf.Max(0.01f, body.mass) : 1f;

        var material = "Plastic";

        switch (character.GetType().Name)
        {
            case "Balloon":
                material = "Balloon";

                // Held, he lets the air in and rises.
                profile.modes.Add(new MovementMode { name = "float", density = 0.2f, fallGravity = 0.5f, terminalSpeed = 0.4f });
                break;

            case "BouncyBall":
                material = "Rubber";

                // Inflated he's full of air, so he floats and bounces; charging a jump he is squashed and slow.
                profile.modes.Add(new MovementMode { name = "bouncy", density = 0.15f, jumpHeight = 1.5f, fallGravity = 0.7f });
                profile.modes.Add(new MovementMode { name = "charging", speed = 0.3f, acceleration = 0.5f });
                break;

            case "BowlingBall":
                material = "Resin";

                // Rolling, he's fast, but slow to get going and slow to stop.
                profile.modes.Add(new MovementMode { name = "rolling", speed = 1.6f, acceleration = 0.4f, braking = 0.3f, jumpHeight = 0.5f });
                break;

            case "Umbrella":
                material = "Fabric";

                // Open, he drifts down, and a jump goes nowhere; in a wind with nothing pulling him down, he's carried up.
                profile.modes.Add(new MovementMode { name = "float", fallGravity = 0.1f, terminalSpeed = 0.2f, jumpHeight = 0.3f, riseGravity = 8f });
                profile.modes.Add(new MovementMode { name = "updraft", fallGravity = 0.1f, terminalSpeed = 0.2f, jumpHeight = 0.3f });
                break;

            case "Brick":
                material = "Brick";
                break;

            case "Drill":
            case "Magnet":
            case "Piton":
                material = "Steel";
                break;

            case "Pebble":
                material = "Stone";
                break;

            case "Bulb":
                material = "Glass";
                break;

            case "Ladder":
            case "Torch":
                material = "Wood";
                break;

            case "SuctionCup":
                material = "Rubber";
                break;

            default:
                var lower = character.gameObject.name.ToLowerInvariant();
                if (lower.Contains("pencil") || lower.Contains("wood"))
                {
                    material = "Wood";
                }
                break;
        }

        profile.material = AssetDatabase.LoadAssetAtPath<CharacterMaterial>($"{Root}/Materials/{material}.asset");
    }

    private static string Sanitise(string name)
    {
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(bad, '_');
        }

        return name.Trim();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // Makes the asset if it isn't there, and returns 1 if it did.
    private static int LoadOrCreate<T>(string path, System.Action<T> initialise, out T asset) where T : ScriptableObject
    {
        asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset)
        {
            return 0;
        }

        asset = ScriptableObject.CreateInstance<T>();
        initialise?.Invoke(asset);
        AssetDatabase.CreateAsset(asset, path);
        return 1;
    }
}
