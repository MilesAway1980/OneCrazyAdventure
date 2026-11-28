using UnityEditor;
using UnityEngine;

// Shows a door's settings without the ones that don't apply: a door that goes exactly with its switch has no speed of its own, so Speed is hidden then.
// Everything else is drawn as it always was.
[CustomEditor(typeof(SwitchDoor))]
[CanEditMultipleObjects]
public class SwitchDoorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var own = serializedObject.FindProperty(nameof(SwitchDoor.useOwnSpeed));

        // With several doors selected that differ, Speed is shown, so that nothing is hidden from one of them.
        var showSpeed = own.hasMultipleDifferentValues || own.boolValue;

        var property = serializedObject.GetIterator();
        var enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(property);
                }

                continue;
            }

            if (property.name == nameof(SwitchDoor.speed) && !showSpeed)
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
