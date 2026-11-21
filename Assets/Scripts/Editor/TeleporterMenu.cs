using UnityEditor;
using UnityEngine;

// GameObject > Teleporter Pair: makes two pads, linked, under one object: the first has the second as its Destination, as a Two Way pair
// that can be used again and again. Change the settings on the first pad. Give the pads their own pictures.
public static class TeleporterMenu
{
    [MenuItem("GameObject/Teleporter Pair", false, 14)]
    private static void MakePair(MenuCommand command)
    {
        var parent = command.context as GameObject;
        var pair = new GameObject("Teleporter Pair");

        GameObjectUtility.SetParentAndAlign(pair, parent);
        Undo.RegisterCreatedObjectUndo(pair, "Create Teleporter Pair");

        // At the middle of what's being looked at, if it's not under something.
        if (!parent && SceneView.lastActiveSceneView)
        {
            var at = SceneView.lastActiveSceneView.pivot;
            pair.transform.position = new Vector3(at.x, at.y, 0f);
        }

        var first = MakePad(pair.transform, "Teleporter A", -3f);
        var second = MakePad(pair.transform, "Teleporter B", 3f);

        first.destination = second;
        EditorUtility.SetDirty(first);

        Selection.activeGameObject = first.gameObject;
    }

    private static Teleporter MakePad(Transform parent, string name, float x)
    {
        var pad = new GameObject(name);
        pad.transform.SetParent(parent, false);
        pad.transform.localPosition = new Vector3(x, 0f, 0f);
        Undo.RegisterCreatedObjectUndo(pad, "Create Teleporter Pair");

        // Adding the Teleporter gives it its collider, and makes it a trigger.
        return pad.AddComponent<Teleporter>();
    }
}
