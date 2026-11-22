using UnityEditor;
using UnityEngine;

// GameObject > Walk Switch: makes an area that switches things when a character walks into it, or out of it. Adding the switch gives it
// its Box Collider 2D, and makes it a trigger. Give it the switchable objects to work.
public static class WalkSwitchMenu
{
    [MenuItem("GameObject/Walk Switch", false, 14)]
    private static void Make(MenuCommand command)
    {
        var parent = command.context as GameObject;
        var made = new GameObject("Walk Switch");

        GameObjectUtility.SetParentAndAlign(made, parent);
        Undo.RegisterCreatedObjectUndo(made, "Create Walk Switch");

        // At the middle of what's being looked at, if it's not under something.
        if (!parent && SceneView.lastActiveSceneView)
        {
            var at = SceneView.lastActiveSceneView.pivot;
            made.transform.position = new Vector3(at.x, at.y, 0f);
        }

        made.AddComponent<WalkSwitch>();
        Selection.activeGameObject = made;
    }
}
