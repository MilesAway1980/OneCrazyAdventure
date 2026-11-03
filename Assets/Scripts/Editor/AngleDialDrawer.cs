using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AngleDialAttribute))]
public class AngleDialDrawer : PropertyDrawer
{
    public const float SnapDegrees = 15f;

    private const float DialSize = 56f;

    // Wraps any angle into 0 up to (but not including) 360.
    public static float Normalize(float angle)
    {
        return (angle % 360f + 360f) % 360f;
    }

    // The angle that points from the centre of a dial to an offset from it, where up is 0 and it grows clockwise.
    // The screen's y grows downwards, so the offset is flipped to match GameMath.GetMagnitudeFromAngle.
    public static float AngleFromOffset(float x, float up, bool snap)
    {
        var angle = Mathf.Atan2(x, up) * Mathf.Rad2Deg;

        if (snap)
        {
            angle = Mathf.Round(angle / SnapDegrees) * SnapDegrees;
        }

        return Normalize(angle);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Float)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + DialSize;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Float)
        {
            EditorGUI.LabelField(position, label.text, "AngleDial only works on a float.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        var field = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.BeginChangeCheck();
        var typed = EditorGUI.FloatField(field, label, property.floatValue);
        if (EditorGUI.EndChangeCheck())
        {
            property.floatValue = Normalize(typed);
        }

        // The dial sits under the number, lined up with the field rather than the label.
        var dial = new Rect(
            position.x + EditorGUIUtility.labelWidth,
            field.yMax + EditorGUIUtility.standardVerticalSpacing,
            DialSize,
            DialSize
        );

        DrawDial(dial, property);

        EditorGUI.EndProperty();
    }

    private static void DrawDial(Rect dial, SerializedProperty property)
    {
        var id = GUIUtility.GetControlID(FocusType.Passive);
        var e = Event.current;
        var centre = dial.center;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button == 0 && dial.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    SetFromMouse(property, centre, e);
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    SetFromMouse(property, centre, e);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;

            case EventType.Repaint:
                Draw(dial, property.floatValue, GUIUtility.hotControl == id);
                break;
        }
    }

    private static void SetFromMouse(SerializedProperty property, Vector2 centre, Event e)
    {
        var offset = e.mousePosition - centre;

        // Right on the centre there's no direction to point in.
        if (offset.sqrMagnitude < 1f)
        {
            return;
        }

        property.floatValue = AngleFromOffset(offset.x, -offset.y, e.shift);
        GUI.changed = true;
    }

    private static void Draw(Rect dial, float angle, bool dragging)
    {
        // Handles work in 3D, so the centre is a Vector3 to match Direction.
        Vector3 centre = dial.center;
        var radius = dial.width * 0.5f - 2f;

        var previous = Handles.color;

        Handles.color = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.15f) : new Color(0f, 0f, 0f, 0.1f);
        Handles.DrawSolidDisc(centre, Vector3.forward, radius);

        Handles.color = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.5f) : new Color(0f, 0f, 0f, 0.5f);
        Handles.DrawWireDisc(centre, Vector3.forward, radius);

        // A tick at each quarter turn, so up, right, down and left are easy to find.
        for (var quarter = 0; quarter < 4; quarter++)
        {
            var tick = Direction(quarter * 90f);
            Handles.DrawLine(centre + tick * (radius - 4f), centre + tick * radius);
        }

        Handles.color = dragging ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.6f, 0.1f);
        var tip = centre + Direction(angle) * (radius - 2f);
        Handles.DrawAAPolyLine(3f, centre, tip);
        Handles.DrawSolidDisc(tip, Vector3.forward, 3f);

        Handles.color = previous;
    }

    // A point on the dial for an angle, in screen space where y grows downwards.
    private static Vector3 Direction(float angle)
    {
        var radians = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), -Mathf.Cos(radians), 0f);
    }
}
