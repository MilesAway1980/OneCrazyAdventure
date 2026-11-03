using System;
using System.Collections.Generic;
using UnityEngine;

// How a character's meter looks and sits in the view: the same settings for each one that has a meter.
[Serializable]
public class MeterLayout
{
    [Tooltip("Whether the meter is shown.")]
    public bool show = true;

    [Tooltip("The bar's width, as a fraction of the width of the view it's in.")]
    [Range(0.05f, 0.6f)]
    public float width = 0.2f;

    [Tooltip("The bar's height, as a fraction of the height of the view it's in.")]
    [Range(0.01f, 0.15f)]
    public float height = 0.04f;

    [Tooltip("How far the bar sits from the corner, as a fraction of the height of the view.")]
    [Range(0f, 0.2f)]
    public float margin = 0.03f;

    [Tooltip("How many seconds the bar stays after what it measures stops changing.")]
    public float showSeconds = 2f;
}

// A bar fixed in the upper right of a camera's view, showing how full something is: a battery, a jump charge, how far a
// ladder has stretched. It's drawn on top of the screen, so the scene's lighting doesn't darken it. Added to whatever
// owns the value, which tells it each frame how full it is, what colour to be and which camera to appear in. Bars in
// the same view stack downwards from the corner instead of covering each other.
public class ScreenMeter : MonoBehaviour
{
    private const float FadeSpeed = 4f;

    private static readonly List<ScreenMeter> all = new List<ScreenMeter>();

    private float widthFraction = 0.2f;
    private float heightFraction = 0.04f;
    private float marginFraction = 0.03f;

    private Camera view;
    private float fraction;
    private Color fillColor = Color.white;
    private string label;
    private float markFraction = -1f;
    private bool shown;

    private float alpha;
    private GUIStyle style;

    void OnEnable()
    {
        all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
    }

    // How big the bar is and how far it sits from the corner, as fractions of the camera's view: the width as a
    // fraction of its width, the height and the margin as fractions of its height.
    public void SetLayout(float width, float height, float margin)
    {
        widthFraction = width;
        heightFraction = height;
        marginFraction = margin;
    }

    public void SetLayout(MeterLayout layout)
    {
        SetLayout(layout.width, layout.height, layout.margin);
    }

    // fraction is how full the bar is. visible is whether it should be on screen, and it fades in and out. onCamera is
    // the camera whose view it appears in. label is text written across it, if any, and markFraction is where to draw
    // a white tick, such as the level a battery has to reach to work again; below 0 for no tick.
    public void Show(Camera onCamera, float value, bool visible, Color color, string text = null, float mark = -1f)
    {
        view = onCamera;
        fraction = Mathf.Clamp01(value);
        fillColor = color;
        label = text;
        markFraction = mark;
        shown = visible && onCamera;
    }

    // Red when nearly empty, through yellow, to green when full.
    public static Color RedToGreen(float value)
    {
        value = Mathf.Clamp01(value);

        return value < 0.5f
            ? Color.Lerp(Color.red, Color.yellow, value * 2f)
            : Color.Lerp(Color.yellow, Color.green, (value - 0.5f) * 2f);
    }

    // Flickers between two colours, for something that needs noticing.
    public static Color Pulse(Color first, Color second)
    {
        return Color.Lerp(first, second, Mathf.PingPong(Time.unscaledTime * 3f, 1f));
    }

    public static string Percent(float value)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }

    void Update()
    {
        alpha = Mathf.MoveTowards(alpha, shown ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || alpha <= 0f || !view)
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

        var area = view.pixelRect;
        var barWidth = area.width * widthFraction;
        var barHeight = area.height * heightFraction;
        var margin = area.height * marginFraction;

        // Under any other bars already showing in this view.
        var slot = 0;
        foreach (var other in all)
        {
            if (other == this)
            {
                break;
            }

            if (other.view == view && other.alpha > 0f)
            {
                slot++;
            }
        }

        // A camera's rect counts up from the bottom of the screen, while GUI counts down from the top.
        var bar = new Rect(
            area.xMax - margin - barWidth,
            Screen.height - area.yMax + margin + slot * barHeight * 1.4f,
            barWidth,
            barHeight);

        var border = Mathf.Max(2f, barHeight * 0.12f);

        GUI.color = new Color(0f, 0f, 0f, 0.65f * alpha);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);

        var inner = new Rect(bar.x + border, bar.y + border, bar.width - border * 2f, bar.height - border * 2f);

        var color = fillColor;
        color.a = alpha;

        GUI.color = color;
        GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * fraction, inner.height), Texture2D.whiteTexture);

        if (markFraction >= 0f)
        {
            var markWidth = Mathf.Max(2f, border);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(
                new Rect(inner.x + inner.width * Mathf.Clamp01(markFraction) - markWidth * 0.5f, inner.y, markWidth, inner.height),
                Texture2D.whiteTexture);
        }

        if (!string.IsNullOrEmpty(label))
        {
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(barHeight * 0.65f));

            GUI.color = new Color(0f, 0f, 0f, 0.8f * alpha);
            GUI.Label(new Rect(bar.x + 1f, bar.y + 1f, bar.width, bar.height), label, style);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(bar, label, style);
        }

        GUI.color = Color.white;
    }
}
