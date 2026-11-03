using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// How a pulse moves between its low and high ends.
public enum PulseShape
{
    Smooth,
    Triangle,
    Strobe,
    Flicker
}

// A value that moves between two ends over time: a light's radius or its intensity.
[Serializable]
public class LightPulse
{
    [Tooltip("The low end of the pulse.")]
    public float min;

    [Tooltip("The high end of the pulse. With no Period Seconds, this is the value it stays at.")]
    public float max;

    [Tooltip("Seconds for one whole cycle, from low to high and back. For Flicker, about how long between changes. 0 turns the pulsing off.")]
    public float periodSeconds;

    [Tooltip("Smooth eases between the ends, like breathing. Triangle moves at a steady rate and turns round sharply. Strobe snaps between them. Flicker wanders at random, like a flame.")]
    public PulseShape shape = PulseShape.Triangle;

    [Tooltip("Strobe only: the fraction of each cycle spent at the high end.")]
    [Range(0.05f, 0.95f)]
    public float strobeOnFraction = 0.5f;

    // Min and max both left at 0 means the pulse hasn't been set up.
    public bool IsSet => min != 0f || max != 0f;

    // Where it is between its ends, from 0 at the low end to 1 at the high end. The seed keeps the flickering of
    // different lights, and of different pulses, from matching.
    public float Wave(float time, float seed)
    {
        if (periodSeconds <= 0f)
        {
            return 1f;
        }

        var cycles = time / periodSeconds;

        switch (shape)
        {
            case PulseShape.Smooth:
                return 0.5f - 0.5f * Mathf.Cos(cycles * 2f * Mathf.PI);

            case PulseShape.Triangle:
                return Mathf.PingPong(cycles * 2f, 1f);

            case PulseShape.Strobe:
                return Mathf.Repeat(cycles, 1f) < strobeOnFraction ? 1f : 0f;

            default:
                // Perlin noise stays near the middle, so it's stretched to use the whole range.
                return Mathf.InverseLerp(0.2f, 0.8f, Mathf.PerlinNoise(cycles, seed));
        }
    }

    public float Evaluate(float time, float seed)
    {
        return Mathf.Lerp(min, max, Wave(time, seed));
    }
}

// Everything about how a light looks in one state, lit or unlit.
[Serializable]
public class LightLook
{
    [Tooltip("The radius of the light, in world units. Left at 0 and 0, a lit source keeps the radius of its Light2D, and an unlit one has no glow at all.")]
    public LightPulse radius = new LightPulse();

    [Tooltip("The intensity of the light. At 0 and 0 the light gives off nothing, whatever the Light2D's own intensity is.")]
    public LightPulse intensity = new LightPulse { min = 1f, max = 1f };

    [Tooltip("Whether the light changes color as its intensity pulses, from the dim color at the low end to the bright color at the high end.")]
    public bool tint;

    public Color dimColor = new Color(1f, 0.4f, 0f);
    public Color brightColor = new Color(1f, 0.85f, 0.4f);
}

// A source of light. It runs the Light2D on the same object, adding the pulsing glow and the on/off control that
// the sconces, torch and bulb use. Put your own Light2D on the object to choose its color, falloff and so on; the
// radius and color the looks leave unset are taken from it, but the intensity always comes from the looks. If there
// isn't one, a plain white point light is added.
//
// A source is either lit or unlit, and each has its own look, so a sconce can glow dimly when it's out and brightly
// when it's lit using this one component. Only one of these belongs on an object: they share its Light2D.
public class LightTileSource : MonoBehaviour
{
    // A second pulse on the same light has to wander differently from the first.
    private const float IntensitySeedOffset = 37f;

    [Header("Lit")]
    public LightLook litLook = new LightLook();

    [Header("Unlit")]
    public LightLook unlitLook = new LightLook();

    [Header("Changing")]
    [Tooltip("Seconds to ease from one look to the other when lit or unlit. 0 changes immediately.")]
    public float fadeSeconds;

    [Tooltip("Seconds it stays lit before it goes out by itself. 0 keeps it lit until something snuffs it. Lighting it again, with Light, starts the time over.")]
    public float timeUntilGoOut;

    [Tooltip("Seconds left before it goes out by itself. Only for watching: it's 0 when unlit or when it never goes out.")]
    public float timeLeftUntilSnuff;

    [Tooltip("Starts each light part of the way through its cycle, at random, so that a row of sconces doesn't pulse together.")]
    public bool randomizeStart = true;

    [Header("Shadows")]
    [Tooltip("Off: the light follows the project's Global Light Settings. On: it uses the shadow settings below instead. Either way, these override whatever is set on the Light2D's own Shadows section.")]
    public bool overrideGlobalShadows;

    [Tooltip("Whether the light is blocked by tiles and anything else with a Shadow Caster 2D. Only used when Override Global Shadows is on.")]
    public bool castShadows = true;

    [Tooltip("How dark the shadows are. Only used when Override Global Shadows is on.")]
    [Range(0f, 1f)]
    public float shadowStrength = 0.75f;

    [Tooltip("How soft the edges of the shadows are. Only used when Override Global Shadows is on.")]
    [Range(0f, 1f)]
    public float shadowSoftness = 0.3f;

    [Tooltip("How quickly the soft edge fades. Only used when Override Global Shadows is on.")]
    [Range(0f, 1f)]
    public float shadowFalloffStrength = 0.5f;

    [Space]
    [Tooltip("Whether the source starts lit. Change it while playing with SetLit, Light and Snuff.")]
    public bool lit = true;

    private Light2D sourceLight;

    // What the Light2D was given, for a look that leaves them unset.
    private float baseRadius;
    private Color baseColor = Color.white;

    // How lit it looks: 1 is the lit look, 0 the unlit look, and between is a fade from one to the other.
    private float litBlend;

    private float timeOffset;
    private float noiseSeed;

    private bool warnedNoGlow;
    private bool updated;

    // When it was last lit, and whether it was lit the last time it looked, so it notices being lit by any means.
    private float burnStart;
    private bool wasLit;

    // The version of the shared shadow settings the light was last given.
    private int appliedShadowVersion = -1;

    void Awake()
    {
        if (GetComponents<LightTileSource>().Length > 1)
        {
            Debug.LogWarning(
                $"{name}: has more than one LightTileSource. They share the object's Light2D and will fight over it. " +
                "Use one, with its lit and unlit looks, or put each on its own object.", this);
        }

        sourceLight = GetComponent<Light2D>();

        if (!sourceLight)
        {
            sourceLight = gameObject.AddComponent<Light2D>();
            sourceLight.lightType = Light2D.LightType.Point;
            sourceLight.intensity = 1f;

            Debug.Log($"{name}: added a Light2D for its LightTileSource.", this);
        }

        baseRadius = sourceLight.pointLightOuterRadius;
        baseColor = sourceLight.color;

        if (randomizeStart)
        {
            timeOffset = UnityEngine.Random.Range(0f, 100f);
        }

        noiseSeed = UnityEngine.Random.Range(0f, 100f);
        litBlend = lit ? 1f : 0f;
        wasLit = lit;
        burnStart = Time.time;

        ApplyShadows();

        Refresh();
    }

    void OnEnable()
    {
        if (sourceLight)
        {
            Refresh();
        }
    }

    void OnDisable()
    {
        if (sourceLight)
        {
            sourceLight.enabled = false;
        }
    }

    void Update()
    {
        UpdateBurning();

        var target = lit ? 1f : 0f;
        var step = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;

        // Whatever the other scripts set it to as they started is how it begins, not something to fade to.
        if (!updated)
        {
            updated = true;
            step = 1f;
        }

        litBlend = Mathf.MoveTowards(litBlend, target, step);

        ApplyLook();

        // Pick up edits made while playing: to the shared settings, or to this light's own.
        if (overrideGlobalShadows || appliedShadowVersion != GlobalLightSettings.Current.Version)
        {
            ApplyShadows();
        }
    }

    public void SetLit(bool isLit)
    {
        lit = isLit;
    }

    // Lights it, and if it was lit already, starts the time until it goes out over.
    public void Light()
    {
        SetLit(true);
        burnStart = Time.time;
    }

    public void Snuff()
    {
        SetLit(false);
    }

    // Times it from when it was lit, and puts it out when the time is up.
    private void UpdateBurning()
    {
        if (lit && !wasLit)
        {
            burnStart = Time.time;
        }

        wasLit = lit;

        if (!lit || timeUntilGoOut <= 0f)
        {
            timeLeftUntilSnuff = 0f;
            return;
        }

        timeLeftUntilSnuff = timeUntilGoOut - (Time.time - burnStart);

        if (timeLeftUntilSnuff <= 0f)
        {
            timeLeftUntilSnuff = 0f;
            lit = false;
            wasLit = false;
        }
    }

    private void ApplyShadows()
    {
        appliedShadowVersion = GlobalLightSettings.Current.Version;

        GlobalLightSettings.Apply(
            sourceLight, overrideGlobalShadows, castShadows, shadowStrength, shadowSoftness, shadowFalloffStrength);
    }

    // Sets the light up for the state it is in now, which also switches it on or off.
    private void Refresh()
    {
        litBlend = lit ? 1f : 0f;
        ApplyLook();
    }

    private float Radius(LightLook look, bool isLit, float time)
    {
        if (look.radius.IsSet)
        {
            return look.radius.Evaluate(time, noiseSeed);
        }

        return isLit ? baseRadius : 0f;
    }

    private float Intensity(LightLook look, float time)
    {
        return look.intensity.Evaluate(time, noiseSeed + IntensitySeedOffset);
    }

    private Color Tint(LightLook look, float time)
    {
        if (!look.tint)
        {
            return baseColor;
        }

        return Color.Lerp(look.dimColor, look.brightColor, look.intensity.Wave(time, noiseSeed + IntensitySeedOffset));
    }

    private void ApplyLook()
    {
        if (!sourceLight)
        {
            return;
        }

        // A light that gives off nothing isn't worth drawing.
        var glows = litBlend > 0f || unlitLook.radius.IsSet;

        sourceLight.enabled = enabled && glows;

        if (!glows && enabled && !warnedNoGlow)
        {
            warnedNoGlow = true;
            Debug.LogWarning(
                $"{name}: its Light2D is switched off because it's unlit and the unlit look has no radius set.", this);
        }

        if (!sourceLight.enabled)
        {
            return;
        }

        var time = Time.time + timeOffset;

        sourceLight.intensity = Mathf.Lerp(Intensity(unlitLook, time), Intensity(litLook, time), litBlend);

        sourceLight.color = Color.Lerp(Tint(unlitLook, time), Tint(litLook, time), litBlend);

        if (sourceLight.lightType == Light2D.LightType.Point)
        {
            var radius = Mathf.Lerp(Radius(unlitLook, false, time), Radius(litLook, true, time), litBlend);

            if (!Mathf.Approximately(sourceLight.pointLightOuterRadius, radius))
            {
                sourceLight.pointLightOuterRadius = radius;
            }
        }
    }
}
