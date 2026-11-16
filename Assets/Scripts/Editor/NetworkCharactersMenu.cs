using Characters;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Network > Make Characters Networked: puts every character in the open scene on the network, and Remove Networking from
// Characters takes it off again. Each is one step in the Undo history.
//
// What each character gets:
//  - a Network Identity, so Mirror knows it's the same character on every machine,
//  - a Network Rigidbody 2D (Unreliable) that sends where it is, and keeps its physics on the host only, and
//  - a Character Network, which sends the rest of what the other machines need to show it.
//
// The characters are objects in each scene, not prefabs, so this has to be done once for every scene that's played over a network.
public static class NetworkCharactersMenu
{
    [MenuItem("Tools/Network/Make Characters Networked")]
    private static void MakeNetworked()
    {
        var characters = Object.FindObjectsByType<Character>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (characters.Length == 0)
        {
            EditorUtility.DisplayDialog("Make Characters Networked", "There are no characters in the open scene.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Make Characters Networked");

        var changed = 0;

        foreach (var character in characters)
        {
            var go = character.gameObject;

            if (!go.GetComponent<Rigidbody2D>())
            {
                Debug.LogWarning($"{go.name} has no Rigidbody2D, so it was left alone.", go);
                continue;
            }

            if (!go.GetComponent<NetworkIdentity>())
            {
                Undo.AddComponent<NetworkIdentity>(go);
                changed++;
            }

            var sync = go.GetComponent<NetworkRigidbodyUnreliable2D>();

            if (!sync)
            {
                sync = Undo.AddComponent<NetworkRigidbodyUnreliable2D>(go);
                changed++;
            }

            // Where it is and which way it's turned. A rolling character needs the turn, and none of them change size.
            Undo.RecordObject(sync, "Make Characters Networked");
            sync.target = go.transform;
            sync.syncDirection = SyncDirection.ServerToClient;
            sync.syncPosition = true;
            sync.syncRotation = true;
            sync.syncScale = false;
            sync.coordinateSpace = CoordinateSpace.World;

            if (!go.GetComponent<CharacterNetwork>())
            {
                Undo.AddComponent<CharacterNetwork>(go);
                changed++;
            }

            EditorUtility.SetDirty(go);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Made {characters.Length} characters networked ({changed} components added). Save the scene.");
    }

    // The object the Network Manager makes for each connection. Done once for the project, not for each scene.
    [MenuItem("Tools/Network/Create Connection Player Prefab")]
    private static void CreateConnectionPlayerPrefab()
    {
        const string path = "Assets/Resources/ConnectionPlayer.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path))
        {
            EditorUtility.DisplayDialog("Create Connection Player Prefab", $"{path} is already there.", "OK");
            return;
        }

        var made = new GameObject("ConnectionPlayer");
        made.AddComponent<NetworkIdentity>();
        made.AddComponent<ConnectionPlayer>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(made, path);
        Object.DestroyImmediate(made);

        AssetDatabase.SaveAssets();
        Debug.Log($"Made {path}. The game uses it for each connection.", prefab);
    }

    [MenuItem("Tools/Network/Remove Networking from Characters")]
    private static void RemoveNetworking()
    {
        var characters = Object.FindObjectsByType<Character>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Networking from Characters");

        foreach (var character in characters)
        {
            var go = character.gameObject;

            // The scripts that need the identity go first.
            RemoveIfThere<CharacterNetwork>(go);
            RemoveIfThere<NetworkRigidbodyUnreliable2D>(go);
            RemoveIfThere<NetworkIdentity>(go);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Took the networking off {characters.Length} characters. Save the scene.");
    }

    private static void RemoveIfThere<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();

        if (component)
        {
            Undo.DestroyObjectImmediate(component);
        }
    }
}
