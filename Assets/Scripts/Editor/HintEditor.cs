using UnityEditor;
using UnityEngine;

// The Inspector for a Hint. It makes any collider on the object a trigger as soon as it's seen, whichever was added first, says so if
// there is no collider yet, and checks the topic: it shows the words it would say, or warns if no hint in the master text has the
// topic. GameObject > Hint makes a whole one: an object with a Box Collider 2D for the area, and the Hint.
[CustomEditor(typeof(Hint))]
[CanEditMultipleObjects]
public class HintEditor : Editor
{
    public override void OnInspectorGUI()
    {
        foreach (var each in targets)
        {
            var hint = (Hint)each;

            foreach (var area in hint.GetComponents<Collider2D>())
            {
                if (!area.isTrigger)
                {
                    Undo.RecordObject(area, "Make Hint Area a Trigger");
                    hint.MakeTriggers();
                }
            }
        }

        DrawDefaultInspector();

        if (targets.Length != 1)
        {
            return;
        }

        var single = (Hint)target;

        EditorGUILayout.Space(4f);
        DrawTopicCheck(single);
        EditorGUILayout.Space(4f);

        if (!single.GetComponent<Collider2D>())
        {
            EditorGUILayout.HelpBox("A Hint shows while a character is in its area, so it needs a collider on this object. A Box Collider 2D is easiest; it's made a trigger for you.", MessageType.Warning);

            if (GUILayout.Button("Add Box Collider 2D"))
            {
                var box = Undo.AddComponent<BoxCollider2D>(single.gameObject);
                box.size = new Vector2(4f, 3f);
                single.MakeTriggers();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Shown while hints are turned on (from the hub menu) and a character in the party is inside the collider.", MessageType.None);
        }
    }

    // Says what the topic comes to: the words, or a warning that nothing has that topic, with a way to pick one that does.
    private static void DrawTopicCheck(Hint hint)
    {
        var words = hint.FindWords();
        var usingCustom = !string.IsNullOrWhiteSpace(hint.customText);

        if (words != null)
        {
            EditorGUILayout.HelpBox((usingCustom ? "Shows (its own text): " : "Shows: ") + words, MessageType.Info);
        }
        else if (string.IsNullOrWhiteSpace(hint.TopicKey))
        {
            EditorGUILayout.HelpBox("This Hint has no topic, so it shows nothing. Type the topic of a hint in the master text, or pick one.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox($"No hint in the master text has the topic \"{hint.TopicKey.Trim()}\", so this shows nothing. Check the spelling, or add a hint with that topic to the master text.", MessageType.Warning);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Pick a Topic..."))
        {
            var menu = new GenericMenu();
            var hints = GameText.Current.hints;

            if (hints != null)
            {
                foreach (var entry in hints)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.topic))
                    {
                        continue;
                    }

                    var chosen = entry.topic;
                    menu.AddItem(new GUIContent(chosen), string.Equals(chosen, hint.topic, System.StringComparison.OrdinalIgnoreCase), () =>
                    {
                        Undo.RecordObject(hint, "Change Hint Topic");
                        hint.topic = chosen;
                        EditorUtility.SetDirty(hint);
                    });
                }
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("There are no hints in the master text"));
            }

            menu.ShowAsContext();
        }

        if (GUILayout.Button("Open Master Text"))
        {
            GameTextMenu.OpenMasterText();
        }

        EditorGUILayout.EndHorizontal();
    }

    [MenuItem("GameObject/Hint", false, 12)]
    private static void MakeHint(MenuCommand command)
    {
        var parent = command.context as GameObject;
        var made = new GameObject("Hint");

        GameObjectUtility.SetParentAndAlign(made, parent);
        Undo.RegisterCreatedObjectUndo(made, "Create Hint");

        // Adding the Hint gives it its area, and makes it a trigger.
        made.AddComponent<Hint>();

        // At the middle of what's being looked at, if it's not under something.
        if (!parent && SceneView.lastActiveSceneView)
        {
            var at = SceneView.lastActiveSceneView.pivot;
            made.transform.position = new Vector3(at.x, at.y, 0f);
        }

        Selection.activeGameObject = made;
    }
}
