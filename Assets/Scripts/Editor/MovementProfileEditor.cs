using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The Inspector for a movement profile: its settings, and below them, what they come to, in words and numbers you can judge
// without playing: how fast, how quickly it gets going and stops, how high and far it jumps (drawn), and what each liquid does.
[CustomEditor(typeof(MovementProfile))]
public class MovementProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var profile = (MovementProfile)target;
        var tuning = MovementTuning.Current;
        var result = new ResolvedMovement();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("What that comes to", EditorStyles.boldLabel);

        profile.Resolve("base", null, tuning, result);
        DrawNumbers(result);
        DrawJumpArc(result);

        foreach (var mode in profile.modes)
        {
            profile.Resolve(mode.name, null, tuning, result);
            EditorGUILayout.LabelField($"Mode \"{mode.name}\"", Summary(result), EditorStyles.wordWrappedLabel);
        }

        var fluids = Fluid.Presets;

        if (fluids.Length > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("In liquids", EditorStyles.boldLabel);

            foreach (var fluid in fluids)
            {
                profile.Resolve("base", fluid, tuning, result);
                profile.DescribeIn(fluid, tuning, "base", out var floats, out var under, out var speed);

                var held = floats
                    ? $"floats, with about {under * 100f:0}% of it under, rising at up to {speed:0.0} tiles/s from deep"
                    : $"sinks at about {speed:0.0} tiles/s";

                EditorGUILayout.LabelField(
                    fluid.name,
                    $"{held}. Walks at {result.maxMoveSpeed:0.0} tiles/s; a kick lifts it {result.jumpHeight:0.0} tiles.",
                    EditorStyles.wordWrappedLabel);
            }
        }

        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private static string Summary(ResolvedMovement r)
    {
        return $"top speed {r.maxMoveSpeed:0.0}, jump {r.jumpHeight:0.0} high, fall {r.fallAcceleration:0.0}x, terminal {r.maxFallSpeed:0}";
    }

    private static void DrawNumbers(ResolvedMovement r)
    {
        EditorGUILayout.LabelField("Running",
            $"top speed {r.maxMoveSpeed:0.0} tiles/s, {r.timeToTopSpeed:0.00} s to get there, " +
            $"{r.timeToStop:0.00} s ({r.stopDistance:0.00} tiles) to stop", EditorStyles.wordWrappedLabel);

        EditorGUILayout.LabelField("Jumping",
            $"{r.jumpHeight:0.0} tiles high (with the button held) and {r.jumpDistance:0.0} long at top speed; " +
            $"{r.apexTime:0.00} s up and {r.fallTime:0.00} s down", EditorStyles.wordWrappedLabel);

        EditorGUILayout.LabelField("Falling",
            $"{r.fallAcceleration:0.0}x gravity, no faster than {r.maxFallSpeed:0.0} tiles/s", EditorStyles.wordWrappedLabel);
    }

    // The path of a jump at top speed, from taking off to landing at the same height. It's drawn to scale, as a path.
    private static void DrawJumpArc(ResolvedMovement r)
    {
        var rect = GUILayoutUtility.GetRect(10f, 110f, GUILayout.ExpandWidth(true));

        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.18f));

        var gravity = Mathf.Abs(Physics2D.gravity.y) * r.gravity;
        var up = gravity * r.heldJumpDecay;
        var down = gravity * r.fallAcceleration;

        if (up <= 0f || down <= 0f || r.jumpForce <= 0f)
        {
            return;
        }

        var apexTime = r.jumpForce / up;
        var height = r.jumpForce * r.jumpForce / (2f * up);
        var fallTime = Mathf.Sqrt(2f * height / down);
        var total = apexTime + fallTime;
        var speed = Mathf.Max(r.maxMoveSpeed, 0.01f);

        var padding = 10f;
        var scale = Mathf.Min((rect.width - 2f * padding) / (speed * total), (rect.height - 2f * padding) / Mathf.Max(height, 0.01f));
        var origin = new Vector2(rect.x + padding, rect.yMax - padding);

        var points = new Vector3[41];

        for (var i = 0; i < points.Length; i++)
        {
            var time = total * i / (points.Length - 1);
            var y = time < apexTime
                ? r.jumpForce * time - 0.5f * up * time * time
                : height - 0.5f * down * (time - apexTime) * (time - apexTime);

            points[i] = new Vector3(origin.x + speed * time * scale, origin.y - y * scale, 0f);
        }

        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        Handles.DrawLine(new Vector3(origin.x, origin.y), new Vector3(rect.xMax - padding, origin.y));

        Handles.color = new Color(1f, 0.85f, 0.2f);
        Handles.DrawAAPolyLine(3f, points);
    }
}
