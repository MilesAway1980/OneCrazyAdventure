using UnityEditor;
using UnityEngine;

// Shows a lever's settings without the ones that don't apply: an ordinary lever has Seconds To Move and not the two Hold To Operate times, and a
// lever that has to be held has those and not Seconds To Move. Everything else is drawn as it always was.
[CustomEditor(typeof(LeverSwitch))]
[CanEditMultipleObjects]
public class LeverSwitchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var hold = serializedObject.FindProperty(nameof(LeverSwitch.holdToOperate));

        // With several levers selected that differ, everything is shown, so that nothing is hidden from one of them.
        var showOrdinary = hold.hasMultipleDifferentValues || !hold.boolValue;
        var showHold = hold.hasMultipleDifferentValues || hold.boolValue;

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

            if (property.name == nameof(LeverSwitch.secondsToMove) && !showOrdinary)
            {
                continue;
            }

            if ((property.name == nameof(LeverSwitch.secondsToHold) || property.name == nameof(LeverSwitch.secondsToReturn) ||
                 property.name == nameof(LeverSwitch.springLoaded) || property.name == nameof(LeverSwitch.logHolding)) && !showHold)
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
