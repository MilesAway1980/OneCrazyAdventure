using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The Inspector and scene view for a Liquid Area. The outline's points are dragged in the scene view, like a Polygon Collider 2D's:
// drag a point to move it, click the dot in the middle of an edge to add a point there, Shift-click a point to delete it.
// GameObject > Liquid > Liquid Area makes one.
[CustomEditor(typeof(LiquidArea))]
[CanEditMultipleObjects]
public class LiquidAreaEditor : Editor
{
    // How close the mouse has to be to a point, in screen pixels, to pick it for deleting.
    private const float PickPixels = 12f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var area = (LiquidArea)target;

        // Typing in the list changes the points without going through the scene view.
        if (GUI.changed)
        {
            foreach (var each in targets)
            {
                ((LiquidArea)each).Refresh();
            }
        }

        EditorGUILayout.Space(6f);

        if (targets.Length != 1)
        {
            return;
        }

        if (GUILayout.Button("Reset To Box"))
        {
            Undo.RecordObject(area, "Reset Liquid Area");
            area.points = new List<Vector2>
            {
                new Vector2(-2f, -1f),
                new Vector2(-2f, 1f),
                new Vector2(2f, 1f),
                new Vector2(2f, -1f)
            };
            Changed(area);
        }

        EditorGUILayout.HelpBox(
            "In the scene view: drag a point to move it (hold Ctrl to snap), click the small dot in the middle of an edge to add a point, " +
            "and Shift-click a point to delete it.",
            MessageType.None);

        if (area.points.Count < 3)
        {
            EditorGUILayout.HelpBox("It needs at least three points to be an area.", MessageType.Warning);
        }
        else if (!area.HasValidShape)
        {
            EditorGUILayout.HelpBox(
                "The outline crosses itself, so the overlay can't be drawn. Anything is still found inside it as the outline says, " +
                "but it's best to uncross it.",
                MessageType.Warning);
        }
    }

    void OnSceneGUI()
    {
        var area = (LiquidArea)target;
        var transform = area.transform;
        var points = area.points;
        var current = Event.current;

        // The inside, lightly filled so it's clear what's liquid.
        if (current.type == EventType.Repaint && area.HasValidShape)
        {
            var triangles = area.Triangles;
            Handles.color = new Color(0.2f, 0.7f, 1f, 0.15f);

            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                Handles.DrawAAConvexPolygon(
                    transform.TransformPoint(points[triangles[i]]),
                    transform.TransformPoint(points[triangles[i + 1]]),
                    transform.TransformPoint(points[triangles[i + 2]]));
            }
        }

        // Shift-click on a point deletes it. This is done before the handles, which would otherwise take the click.
        if (current.type == EventType.MouseDown && current.button == 0 && current.shift && !current.alt)
        {
            var nearest = NearestPoint(area, current.mousePosition);

            if (nearest >= 0)
            {
                if (points.Count > 3)
                {
                    Undo.RecordObject(area, "Remove Liquid Point");
                    points.RemoveAt(nearest);
                    Changed(area);
                }

                current.Use();
                return;
            }
        }

        // The points, to drag.
        Handles.color = Color.cyan;

        for (var i = 0; i < points.Count; i++)
        {
            var world = transform.TransformPoint(points[i]);
            var size = HandleUtility.GetHandleSize(world) * 0.07f;

            EditorGUI.BeginChangeCheck();
            var moved = Handles.FreeMoveHandle(world, size, Vector3.one * 0.25f, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                var local = transform.InverseTransformPoint(moved);

                Undo.RecordObject(area, "Move Liquid Point");
                points[i] = new Vector2(local.x, local.y);
                Changed(area);
            }
        }

        // A dot in the middle of each edge, to click to add a point there.
        Handles.color = new Color(1f, 1f, 1f, 0.7f);

        for (var i = 0; i < points.Count; i++)
        {
            var next = (i + 1) % points.Count;
            var world = transform.TransformPoint((points[i] + points[next]) * 0.5f);
            var size = HandleUtility.GetHandleSize(world) * 0.04f;

            if (Handles.Button(world, Quaternion.identity, size, size * 1.6f, Handles.DotHandleCap))
            {
                Undo.RecordObject(area, "Add Liquid Point");
                points.Insert(i + 1, (points[i] + points[next]) * 0.5f);
                Changed(area);
                break;
            }
        }
    }

    // The point closest to the mouse, if it's close enough, or -1.
    private static int NearestPoint(LiquidArea area, Vector2 mouse)
    {
        var nearest = -1;
        var nearestDistance = PickPixels;

        for (var i = 0; i < area.points.Count; i++)
        {
            var onScreen = HandleUtility.WorldToGUIPoint(area.transform.TransformPoint(area.points[i]));
            var distance = Vector2.Distance(onScreen, mouse);

            if (distance <= nearestDistance)
            {
                nearest = i;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static void Changed(LiquidArea area)
    {
        EditorUtility.SetDirty(area);
        area.Refresh();
    }

    [MenuItem("GameObject/Liquid/Liquid Area", false, 10)]
    private static void MakeArea(MenuCommand command)
    {
        var parent = command.context as GameObject;
        var made = new GameObject("Liquid Area");

        GameObjectUtility.SetParentAndAlign(made, parent);
        Undo.RegisterCreatedObjectUndo(made, "Create Liquid Area");
        made.AddComponent<LiquidArea>();

        // At the middle of what's being looked at, if it's not under something.
        if (!parent && SceneView.lastActiveSceneView)
        {
            var at = SceneView.lastActiveSceneView.pivot;
            made.transform.position = new Vector3(at.x, at.y, 0f);
        }

        Selection.activeGameObject = made;
    }
}
