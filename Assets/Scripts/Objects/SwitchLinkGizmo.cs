using System.Collections.Generic;
using UnityEngine;

// Arrows in the scene view from something that switches things (a lever, a button) to each of the things it switches, so it's clear what
// is wired to what without opening anything. They're only drawn while the switch is selected, as the range of a swaying light is, so the
// scene isn't covered in them. They can all be turned off at once from Tools > Gizmos > Show Switch Arrows.
public static class SwitchLinkGizmo
{
    private const string PreferenceKey = "ShowSwitchArrows";

    // How far in from each end the arrow starts and stops, and how big the head is, in units.
    private const float StartGap = 0.35f;
    private const float EndGap = 0.35f;
    private const float HeadLength = 0.3f;
    private const float HeadAngle = 25f;

    private static bool known;
    private static bool shown = true;

    // Whether the arrows are drawn at all. Remembered by the editor between sessions.
    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR
            if (!known)
            {
                shown = UnityEditor.EditorPrefs.GetBool(PreferenceKey, true);
                known = true;
            }
#endif
            return shown;
        }

        set
        {
            shown = value;
            known = true;
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(PreferenceKey, value);
#endif
        }
    }

    // Draws an arrow from a place to each of the things it switches, skipping any that are missing.
    public static void Draw(Vector3 from, IEnumerable<SwitchableObject> targets)
    {
        if (!Enabled || targets == null)
        {
            return;
        }

        var colour = new Color(1f, 0.9f, 0.2f, 1f);

        foreach (var target in targets)
        {
            if (target)
            {
                Arrow(from, target.GizmoPoint, colour);
            }
        }
    }

    // One arrow, from a place to another, shortened at both ends so it doesn't cover what it joins.
    private static void Arrow(Vector3 from, Vector3 to, Color colour)
    {
        to.z = from.z;

        var along = to - from;
        var length = along.magnitude;

        // Too close to show a direction: a ring where it is instead, so it's still seen.
        if (length < StartGap + EndGap + 0.05f)
        {
            Gizmos.color = colour;
            Gizmos.DrawWireSphere(from, 0.2f);
            return;
        }

        along /= length;

        var start = from + along * StartGap;
        var tip = to - along * EndGap;

        Gizmos.color = colour;
        Gizmos.DrawLine(start, tip);

        // The head: two short lines folded back from the tip.
        var left = Quaternion.Euler(0f, 0f, HeadAngle) * -along;
        var right = Quaternion.Euler(0f, 0f, -HeadAngle) * -along;

        Gizmos.DrawLine(tip, tip + left * HeadLength);
        Gizmos.DrawLine(tip, tip + right * HeadLength);
    }
}
