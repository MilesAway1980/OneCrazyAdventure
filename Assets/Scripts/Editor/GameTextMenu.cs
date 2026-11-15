using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Game Text > Open Master Text: makes the master text asset, in a Resources folder where the game finds it, if there
// isn't one yet, and selects it so its words can be changed in the inspector.
public static class GameTextMenu
{
    private const string Folder = "Assets/Resources";
    private const string AssetPath = "Assets/Resources/GameText.asset";

    [MenuItem("Tools/Game Text/Open Master Text")]
    public static void OpenMasterText()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameText>(AssetPath);

        if (!asset)
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            asset = ScriptableObject.CreateInstance<GameText>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Made the master text at {AssetPath}.", asset);
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
