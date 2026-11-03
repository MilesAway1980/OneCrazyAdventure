using UnityEngine;
using UnityEngine.Rendering.Universal;

// How dark the scene is: the colour of its Global Light 2D, which can slowly pulse between two colours. Everything
// lit by other Light2Ds (LightTileSource, or any you add) shows up against this. Only one of these is needed in a
// scene.
public class DarkTileMap : MonoBehaviour
{
    [Tooltip("The colour of the ambient light at the dark end of the pulse.")]
    public Color darkest;

    [Tooltip("The colour of the ambient light at the light end of the pulse.")]
    public Color lightest;

    [Tooltip("Seconds for the ambient light to fade from Darkest to Lightest and back. 0 turns the pulsing off and holds it at Lightest.")]
    public float colorChangeSpeed;

    [Tooltip("The scene's Global Light 2D. Leave empty to find it, or add one, automatically.")]
    public Light2D globalLight;

    private Color originalDarkest;
    private Color originalLightest;
    private float startTime;

    void Awake()
    {
        // What SwitchBrighten puts back when it switches off.
        originalDarkest = darkest;
        originalLightest = lightest;
    }

    void Start()
    {
        startTime = Time.time;

        if (!globalLight)
        {
            globalLight = FindOrCreateGlobalLight();
        }

        if (FindObjectsByType<DarkTileMap>().Length > 1)
        {
            Debug.LogWarning($"{name}: there's more than one DarkTileMap in the scene. They all set the same Global Light 2D, so they'll fight over it.", this);
        }
    }

    // Applied after everything else has had its Update, so a switch flipped this frame shows this frame.
    void LateUpdate()
    {
        if (!globalLight)
        {
            return;
        }

        var fade = colorChangeSpeed > 0f ? Mathf.PingPong((Time.time - startTime) / colorChangeSpeed, 1f) : 1f;
        var color = Color.Lerp(darkest, lightest, fade);

        globalLight.color = new Color(color.r, color.g, color.b, 1f);
    }

    // Puts the colours back to what they were when the scene started.
    public void RestoreOriginalColors()
    {
        darkest = originalDarkest;
        lightest = originalLightest;
    }

    private static Light2D FindOrCreateGlobalLight()
    {
        foreach (var candidate in FindObjectsByType<Light2D>())
        {
            if (candidate.lightType == Light2D.LightType.Global)
            {
                return candidate;
            }
        }

        var created = new GameObject("Global Light 2D").AddComponent<Light2D>();
        created.lightType = Light2D.LightType.Global;

        return created;
    }
}
