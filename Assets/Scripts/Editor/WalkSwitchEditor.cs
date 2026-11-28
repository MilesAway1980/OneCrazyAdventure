using UnityEditor;
using UnityEngine;

// Shows a walk switch's settings without the ones that don't apply: a Timer goes by how long someone is in the area, so it has no Activate On, and the
// timer's times are only shown for it. Everything else is drawn as it always was.
[CustomEditor(typeof(WalkSwitch))]
[CanEditMultipleObjects]
public class WalkSwitchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var mode = serializedObject.FindProperty(nameof(WalkSwitch.mode));
        var timer = (int)WalkMode.Timer;

        // With several switches selected that differ, everything is shown, so that nothing is hidden from one of them.
        var differ = mode.hasMultipleDifferentValues;
        var showTimer = differ || mode.enumValueIndex == timer;
        var showTrigger = differ || mode.enumValueIndex != timer;

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

            if (property.name == nameof(WalkSwitch.activateOn) && !showTrigger)
            {
                continue;
            }

            if ((property.name == nameof(WalkSwitch.secondsToComplete) || property.name == nameof(WalkSwitch.secondsToReset) ||
                 property.name == nameof(WalkSwitch.staysCompleted)) && !showTimer)
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
