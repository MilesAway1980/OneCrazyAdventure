using UnityEditor;
using UnityEngine;

// Tools > Buttons > Upgrade Button Prefab: sets the project's Button prefab up for the ButtonSwitch that moves its own pad.
//  - The pad's body is made kinematic, so the script moves it and nothing else can,
//  - the Spring Joint 2D that used to push it back up is removed,
//  - and the Pad field is pointed at the pad.
// Every button in every scene that's made from the prefab gets the change. Safe to run again.
public static class ButtonPrefabUpgrade
{
    private const string PrefabPath = "Assets/Prefabs/Button.prefab";

    [MenuItem("Tools/Buttons/Upgrade Button Prefab")]
    private static void Upgrade()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
        {
            EditorUtility.DisplayDialog("Upgrade Button Prefab", $"There is no prefab at {PrefabPath}.", "OK");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            var button = root.GetComponent<ButtonSwitch>();
            var padTransform = root.transform.Find("Pad");
            var padBody = padTransform ? padTransform.GetComponent<Rigidbody2D>() : null;

            if (!button || !padBody)
            {
                EditorUtility.DisplayDialog("Upgrade Button Prefab",
                    "The prefab needs a ButtonSwitch on its root and a child called Pad with a Rigidbody2D. Nothing was changed.", "OK");
                return;
            }

            padBody.bodyType = RigidbodyType2D.Kinematic;
            padBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            padBody.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var removed = 0;

            foreach (var spring in root.GetComponentsInChildren<SpringJoint2D>(true))
            {
                Object.DestroyImmediate(spring);
                removed++;
            }

            var serialized = new SerializedObject(button);
            serialized.FindProperty("pad").objectReferenceValue = padBody;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"Button prefab upgraded: the pad is kinematic, {removed} spring joint(s) removed, and Pad is set.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
