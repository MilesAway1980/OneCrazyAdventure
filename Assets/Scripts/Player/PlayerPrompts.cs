using UnityEngine;

// Messages for the players, drawn over the game: who's leaving and how long until they have, and that a free device
// can be used to join. Added by the PlayerManager, so there's nothing to set up in the scene.
public class PlayerPrompts : MonoBehaviour
{
    [Tooltip("Shows \"Press any button to join...\" while there's room for another player.")]
    public bool showJoinPrompt = true;

    [Tooltip("Shows who is holding the leave button, with a bar that fills up as they get closer to leaving.")]
    public bool showLeavePrompts = true;

    private GUIStyle style;

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
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

        var fontSize = Mathf.Max(12, Screen.height / 32);

        if (showLeavePrompts)
        {
            DrawLeavePrompts(fontSize);
        }

        if (showJoinPrompt && PlayerManager.CanJoin())
        {
            DrawJoinPrompt(fontSize);
        }
    }

    // Down the top of the screen, one line for each player holding the leave button.
    private void DrawLeavePrompts(int fontSize)
    {
        if (PlayerManager.players == null)
        {
            return;
        }

        var lineHeight = fontSize * 2.4f;
        var width = Screen.width * 0.3f;
        var y = fontSize * 0.5f;

        foreach (var player in PlayerManager.players)
        {
            if (!player || !player.characterMove || player.characterMove.LeaveProgress <= 0f)
            {
                continue;
            }

            var progress = Mathf.Clamp01(player.characterMove.LeaveProgress);
            var area = new Rect((Screen.width - width) * 0.5f, y, width, lineHeight);

            DrawText(new Rect(area.x, area.y, area.width, fontSize * 1.5f), $"{player.name} leaving...", fontSize, 1f);

            var bar = new Rect(area.x, area.y + fontSize * 1.6f, area.width, fontSize * 0.4f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.6f, 0.1f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            y += lineHeight;
        }
    }

    // Big in the middle while nobody has joined, and out of the way along the bottom once someone is playing.
    private void DrawJoinPrompt(int fontSize)
    {
        var nobodyYet = PlayerManager.GetJoinedPlayerCount() == 0;
        var size = nobodyYet ? fontSize * 2 : fontSize;
        var area = nobodyYet
            ? new Rect(0f, 0f, Screen.width, Screen.height)
            : new Rect(0f, Screen.height - size * 2.5f, Screen.width, size * 1.5f);

        // A gentle pulse so it's noticed without being distracting.
        var alpha = nobodyYet ? 1f : Mathf.Lerp(0.5f, 0.9f, Mathf.PingPong(Time.unscaledTime, 1f));

        DrawText(area, "Press any button to join...", size, alpha);
    }

    // Text with a dark shadow so it can be read over any part of the level.
    private void DrawText(Rect rect, string text, int size, float alpha)
    {
        style.fontSize = size;

        var shadow = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
        GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
        GUI.Label(shadow, text, style);

        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(rect, text, style);

        GUI.color = Color.white;
    }
}
