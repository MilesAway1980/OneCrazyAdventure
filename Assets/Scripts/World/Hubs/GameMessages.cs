using System.Collections.Generic;
using UnityEngine;

// Short messages for the players, drawn over the game along the top of the screen for a few seconds: a character found, a
// hub reached. There's nothing to set up in the scene: the first message makes the object that shows them.
public class GameMessages : MonoBehaviour
{
    private static GameMessages instance;

    private readonly List<(string text, float until)> messages = new List<(string, float)>();
    private GUIStyle style;

    public static void Show(string text, float seconds = 3.5f)
    {
        if (!instance)
        {
            instance = new GameObject("Game Messages").AddComponent<GameMessages>();
        }

        instance.messages.Add((text, Time.unscaledTime + seconds));
    }

    // A message about something that happened in the game, for the players on every machine in it, not only this one: a character found,
    // a hub reached. It's the host that knows what happened, so it's called there, and tells the others. Where there is no network to
    // tell, it's just shown here. A message for one player only, such as telling them what they can't do, is Show.
    public static void ShowEverywhere(string text, float seconds = 3.5f)
    {
        if (!ConnectionPlayer.Broadcast(text, seconds))
        {
            Show(text, seconds);
        }
    }

    // Hints that stay on the screen for as long as whatever asked for them says so, such as the tutorial's: along the bottom,
    // one line for each. Asking again for the same owner changes its text, and clearing it takes it away.
    private readonly List<(Object owner, string text)> hints = new List<(Object, string)>();

    public static void SetHint(Object owner, string text)
    {
        if (!instance)
        {
            instance = new GameObject("Game Messages").AddComponent<GameMessages>();
        }

        instance.hints.RemoveAll(h => h.owner == owner);

        if (!string.IsNullOrEmpty(text))
        {
            instance.hints.Add((owner, text));
        }
    }

    public static void ClearHint(Object owner)
    {
        if (instance)
        {
            instance.hints.RemoveAll(h => h.owner == owner);
        }
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        messages.RemoveAll(m => Time.unscaledTime >= m.until);
        hints.RemoveAll(h => !h.owner);

        if (messages.Count == 0 && hints.Count == 0)
        {
            return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        var fontSize = Mathf.Max(14, Screen.height / 28);
        style.fontSize = fontSize;

        var lineHeight = fontSize * 2f;
        var width = Screen.width * 0.6f;
        var y = Screen.height * 0.12f;

        foreach (var message in messages)
        {
            var box = new Rect((Screen.width - width) * 0.5f, y, width, lineHeight);

            // Fades away over the last second.
            var alpha = Mathf.Clamp01(message.until - Time.unscaledTime);

            GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
            GUI.DrawTexture(box, Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(box, message.text, style);

            y += lineHeight + fontSize * 0.3f;
        }

        // The hints, up from the bottom of the screen.
        var hintY = Screen.height * 0.88f - hints.Count * (lineHeight + fontSize * 0.3f);

        foreach (var hint in hints)
        {
            var box = new Rect((Screen.width - width) * 0.5f, hintY, width, lineHeight);

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(box, hint.text, style);

            hintY += lineHeight + fontSize * 0.3f;
        }

        GUI.color = Color.white;
    }
}
