using UnityEditor;
using UnityEngine;

// Draws a field as it normally is, with the name its InspectorLabel gives it. Its tooltip is kept.
[CustomPropertyDrawer(typeof(InspectorLabelAttribute))]
public class InspectorLabelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var named = new GUIContent(((InspectorLabelAttribute)attribute).label, label.tooltip);
        EditorGUI.PropertyField(position, property, named, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
