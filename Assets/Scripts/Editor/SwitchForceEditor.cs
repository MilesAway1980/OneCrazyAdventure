using UnityEditor;
using UnityEngine;

// Draws the force's direction in the Scene view as an arrow whose tip can be dragged to turn it.
[CustomEditor(typeof(SwitchForce))]
public class SwitchForceEditor : Editor
{
    // How far out the arrow reaches, in units of the handle's size so it looks the same at any zoom.
    private const float ArrowLength = 1.5f;

    void OnSceneGUI()
    {
        var switchForce = (SwitchForce)target;

        var origin = switchForce.transform.position;
        var size = HandleUtility.GetHandleSize(origin);
        var direction = (Vector3)GameMath.GetMagnitudeFromAngle(switchForce.forceDirection);
        var tip = origin + direction * size * ArrowLength;

        Handles.color = new Color(1f, 0.6f, 0.1f);
        Handles.DrawWireDisc(origin, Vector3.forward, size * ArrowLength);
        Handles.DrawAAPolyLine(3f, origin, tip);

        EditorGUI.BeginChangeCheck();
        var moved = Handles.FreeMoveHandle(tip, size * 0.12f, Vector3.zero, Handles.CircleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            var offset = moved - origin;

            if (offset.sqrMagnitude > 0.0001f)
            {
                Undo.RecordObject(switchForce, "Change Force Direction");
                switchForce.forceDirection = AngleDialDrawer.AngleFromOffset(offset.x, offset.y, Event.current.shift);
            }
        }

        Handles.Label(tip + Vector3.up * size * 0.2f, $"{switchForce.forceDirection:0}°");
    }
}
