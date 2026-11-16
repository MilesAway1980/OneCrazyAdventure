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

            // What a character does that the other machines need to be sent, beyond where it is, goes in a script of its own.
            if (go.GetComponent<Flashlight>() && !go.GetComponent<FlashlightNetwork>())
            {
                Undo.AddComponent<FlashlightNetwork>(go);
                changed++;
            }

            if (go.GetComponent<Bulb>() && !go.GetComponent<BulbNetwork>())
            {
                Undo.AddComponent<BulbNetwork>(go);
                changed++;
            }

            changed += AddCharacterNetwork<Torch, TorchNetwork>(go);
            changed += AddCharacterNetwork<Magnet, MagnetNetwork>(go);
            changed += AddCharacterNetwork<BowlingBall, BowlingBallNetwork>(go);
            changed += AddCharacterNetwork<Brick, BrickNetwork>(go);
            changed += AddCharacterNetwork<SuctionCup, SuctionCupNetwork>(go);
            changed += AddCharacterNetwork<Drill, DrillNetwork>(go);
            changed += AddCharacterNetwork<BouncyBall, BouncyBallNetwork>(go);
            changed += AddCharacterNetwork<Dynamite, DynamiteNetwork>(go);
            changed += AddCharacterNetwork<Ladder, LadderNetwork>(go);
            changed += AddCharacterNetwork<Piton, PitonNetwork>(go);
            changed += AddCharacterNetwork<Balloon, BalloonNetwork>(go);

            EditorUtility.SetDirty(go);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Made {characters.Length} characters networked ({changed} components added). Save the scene.");
    }

    // Gives a character the script that sends what is special to it, if it is that kind of character and hasn't got it. 1 if added.
    private static int AddCharacterNetwork<TCharacter, TNetwork>(GameObject go)
        where TCharacter : Component
        where TNetwork : Component
    {
        if (go.GetComponent<TCharacter>() && !go.GetComponent<TNetwork>())
        {
            Undo.AddComponent<TNetwork>(go);
            return 1;
        }

        return 0;
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
            RemoveIfThere<FlashlightNetwork>(go);
            RemoveIfThere<BulbNetwork>(go);
            RemoveIfThere<TorchNetwork>(go);
            RemoveIfThere<MagnetNetwork>(go);
            RemoveIfThere<BowlingBallNetwork>(go);
            RemoveIfThere<BrickNetwork>(go);
            RemoveIfThere<SuctionCupNetwork>(go);
            RemoveIfThere<DrillNetwork>(go);
            RemoveIfThere<BouncyBallNetwork>(go);
            RemoveIfThere<DynamiteNetwork>(go);
            RemoveIfThere<LadderNetwork>(go);
            RemoveIfThere<PitonNetwork>(go);
            RemoveIfThere<BalloonNetwork>(go);
            RemoveIfThere<CharacterNetwork>(go);
            RemoveIfThere<NetworkRigidbodyUnreliable2D>(go);
            RemoveIfThere<NetworkIdentity>(go);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Took the networking off {characters.Length} characters. Save the scene.");
    }

    // Tools > Network > Make World Networked: the doors, buttons, levers and character pickups in the open scene.
    //  - A door, and a button's pad, send where they are. The rest of what a button and a door do is worked out on the host.
    //  - A lever sends whether it's thrown, and a pickup whether it's been found.
    // Done once for every scene that's played over a network, and again after adding more.
    [MenuItem("Tools/Network/Make World Networked")]
    private static void MakeWorldNetworked()
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Make World Networked");

        var count = 0;

        foreach (var door in Object.FindObjectsByType<SwitchDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            count += NetworkPosition(door.gameObject, door.door ? door.door : door.transform) ? 1 : 0;
        }

        foreach (var button in Object.FindObjectsByType<ButtonSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!button.joint || !button.joint.connectedBody)
            {
                Debug.LogWarning($"{button.name}: the button has no spring joint with a pad, so it was left alone.", button);
                continue;
            }

            var pad = button.joint.connectedBody.gameObject;
            count += NetworkPosition(pad, pad.transform) ? 1 : 0;
        }

        foreach (var lever in Object.FindObjectsByType<LeverSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            count += NetworkWith<LeverNetwork>(lever.gameObject) ? 1 : 0;
        }

        foreach (var pickup in Object.FindObjectsByType<CharacterPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            count += NetworkWith<PickupNetwork>(pickup.gameObject) ? 1 : 0;
        }

        // Things that move, which only need to send where they are.
        foreach (var go in FindMovers())
        {
            count += NetworkPosition(go, go.transform) ? 1 : 0;
        }

        // Rope bridges: the host builds them, and the links they're made of are sent.
        foreach (var builder in Object.FindObjectsByType<SwitchRopeBridgeBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            count += NetworkWith<BridgeNetwork>(builder.gameObject) ? 1 : 0;
        }

        // Enemies: where they are, and which way they face. Their thinking stays on the host.
        foreach (var enemy in FindEnemies())
        {
            if (NetworkPosition(enemy, enemy.transform))
            {
                if (!enemy.GetComponent<EnemyNetwork>())
                {
                    Undo.AddComponent<EnemyNetwork>(enemy);
                }

                count++;
            }
        }

        // One object for all the tiles that can be destroyed in the scene.
        if (Object.FindFirstObjectByType<DamageableTileSection>(FindObjectsInactive.Include) &&
            !Object.FindFirstObjectByType<TileNetwork>(FindObjectsInactive.Include))
        {
            var tiles = new GameObject("Tile Network");
            Undo.RegisterCreatedObjectUndo(tiles, "Make World Networked");
            tiles.AddComponent<NetworkIdentity>();
            tiles.AddComponent<TileNetwork>();
            count++;
        }

        foreach (var hub in Object.FindObjectsByType<CharacterHub>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            count += NetworkWith<HubNetwork>(hub.gameObject) ? 1 : 0;
        }

        foreach (var darkness in Object.FindObjectsByType<DarkTileMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            count += NetworkWith<DarkTileMapNetwork>(darkness.gameObject) ? 1 : 0;
        }

        // Lights that aren't a character's, such as a sconce's: a character's own light goes with the character.
        foreach (var source in Object.FindObjectsByType<LightTileSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!source.GetComponentInParent<Character>())
            {
                count += NetworkWith<LightSourceNetwork>(source.gameObject) ? 1 : 0;
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Made {count} things in the world networked. Save the scene.");
    }

    // Every object in the open scene that is an enemy, which is anything with one of the scripts that makes one move and decide.
    private static System.Collections.Generic.IEnumerable<GameObject> FindEnemies()
    {
        var found = new System.Collections.Generic.HashSet<GameObject>();

        foreach (var script in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(script.gameObject);
        }

        foreach (var script in Object.FindObjectsByType<EnemyMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(script.gameObject);
        }

        foreach (var script in Object.FindObjectsByType<EnemyGroundPath>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(script.gameObject);
        }

        foreach (var script in Object.FindObjectsByType<EnemyPathFollow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(script.gameObject);
        }

        return found;
    }

    // The things in the open scene that move and need nothing but their position sent: crates, things that can be pushed, platforms
    // on a spring, and things that follow a path or sweep from side to side.
    private static System.Collections.Generic.IEnumerable<GameObject> FindMovers()
    {
        var found = new System.Collections.Generic.HashSet<GameObject>();

        foreach (var crate in Object.FindObjectsByType<CarryableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(crate.gameObject);
        }

        foreach (var pushable in Object.FindObjectsByType<PushableSolid>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(pushable.gameObject);
        }

        foreach (var platform in Object.FindObjectsByType<FallingTileSection>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(platform.gameObject);
        }

        foreach (var follower in Object.FindObjectsByType<FollowPath>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(follower.gameObject);
        }

        foreach (var magnet in Object.FindObjectsByType<MagnetBox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            found.Add(magnet.gameObject);
        }

        return found;
    }

    // Tools > Network > Network Selected Objects: for anything that moves that the other tool doesn't know about. Select it in the
    // scene and it sends where the object is. If the object has a Rigidbody2D, its physics is done on the host only.
    [MenuItem("Tools/Network/Network Selected Objects")]
    private static void NetworkSelected()
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Network Selected Objects");

        var count = 0;

        foreach (var go in Selection.gameObjects)
        {
            if (go.scene.IsValid())
            {
                count += NetworkPosition(go, go.transform) ? 1 : 0;
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Made {count} selected objects networked. Save the scene.");
    }

    [MenuItem("Tools/Network/Remove Networking from Selected Objects")]
    private static void RemoveNetworkingFromSelected()
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Networking from Selected Objects");

        foreach (var go in Selection.gameObjects)
        {
            RemoveIfThere<NetworkRigidbodyUnreliable2D>(go);
            RemoveIfThere<NetworkTransformUnreliable>(go);
            RemoveIfThere<NetworkIdentity>(go);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Network/Remove Networking from World")]
    private static void RemoveWorldNetworking()
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Networking from World");

        var objects = new System.Collections.Generic.List<GameObject>();

        foreach (var door in Object.FindObjectsByType<SwitchDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            objects.Add(door.gameObject);
        }

        foreach (var button in Object.FindObjectsByType<ButtonSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.joint && button.joint.connectedBody)
            {
                objects.Add(button.joint.connectedBody.gameObject);
            }
        }

        foreach (var lever in Object.FindObjectsByType<LeverSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            objects.Add(lever.gameObject);
        }

        foreach (var pickup in Object.FindObjectsByType<CharacterPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            objects.Add(pickup.gameObject);
        }

        objects.AddRange(FindMovers());
        objects.AddRange(FindEnemies());

        foreach (var builder in Object.FindObjectsByType<SwitchRopeBridgeBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            objects.Add(builder.gameObject);
        }

        foreach (var tiles in Object.FindObjectsByType<TileNetwork>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(tiles.gameObject);
        }

        foreach (var hub in Object.FindObjectsByType<CharacterHub>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            objects.Add(hub.gameObject);
        }

        foreach (var darkness in Object.FindObjectsByType<DarkTileMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            objects.Add(darkness.gameObject);
        }

        foreach (var source in Object.FindObjectsByType<LightTileSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!source.GetComponentInParent<Character>())
            {
                objects.Add(source.gameObject);
            }
        }

        foreach (var go in objects)
        {
            // The scripts that need the identity go first.
            RemoveIfThere<EnemyNetwork>(go);
            RemoveIfThere<BridgeNetwork>(go);
            RemoveIfThere<HubNetwork>(go);
            RemoveIfThere<DarkTileMapNetwork>(go);
            RemoveIfThere<LightSourceNetwork>(go);
            RemoveIfThere<LeverNetwork>(go);
            RemoveIfThere<PickupNetwork>(go);
            RemoveIfThere<NetworkRigidbodyUnreliable2D>(go);
            RemoveIfThere<NetworkTransformUnreliable>(go);
            RemoveIfThere<NetworkIdentity>(go);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Took the networking off {objects.Count} things in the world. Save the scene.");
    }

    // Gives an object a Network Identity, unless it's inside another that has one, which Mirror doesn't allow.
    private static bool AddIdentity(GameObject go)
    {
        var parent = go.transform.parent;

        if (parent && parent.GetComponentInParent<NetworkIdentity>())
        {
            Debug.LogWarning($"{go.name} is inside another networked object, so it was left alone.", go);
            return false;
        }

        if (!go.GetComponent<NetworkIdentity>())
        {
            Undo.AddComponent<NetworkIdentity>(go);
        }

        return true;
    }

    // Makes an object send where one of its transforms is. If it has a Rigidbody2D, the physics is done on the host only.
    private static bool NetworkPosition(GameObject go, Transform target)
    {
        if (!AddIdentity(go))
        {
            return false;
        }

        var sync = go.GetComponent<NetworkTransformBase>();

        if (!sync)
        {
            sync = target.GetComponent<Rigidbody2D>()
                ? (NetworkTransformBase)Undo.AddComponent<NetworkRigidbodyUnreliable2D>(go)
                : Undo.AddComponent<NetworkTransformUnreliable>(go);
        }

        Undo.RecordObject(sync, "Make World Networked");
        sync.target = target;
        sync.syncDirection = SyncDirection.ServerToClient;
        sync.syncPosition = true;
        sync.syncRotation = true;
        sync.syncScale = false;
        sync.coordinateSpace = CoordinateSpace.World;

        EditorUtility.SetDirty(go);
        return true;
    }

    // Gives an object a Network Identity and a script that sends what it needs.
    private static bool NetworkWith<T>(GameObject go) where T : Component
    {
        if (!AddIdentity(go))
        {
            return false;
        }

        if (!go.GetComponent<T>())
        {
            Undo.AddComponent<T>(go);
        }

        EditorUtility.SetDirty(go);
        return true;
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
