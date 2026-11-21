using UnityEngine;
using UnityEngine.Rendering.Universal;

// The shadow settings shared by the game's lights (LightSource, the Flashlight), so that dozens of sconces can
// be set up in one place. A light follows these unless it has been told to use settings of its own.
//
// To use it: Assets > Create > Lighting > Global Light Settings, name it GlobalLightSettings and put it in a folder
// called Resources. Without one the lights use Unity's own defaults.
[CreateAssetMenu(fileName = "GlobalLightSettings", menuName = "Lighting/Global Light Settings")]
public class GlobalLightSettings : ScriptableObject
{
    public const string ResourceName = "GlobalLightSettings";

    [Tooltip("Whether lights are blocked by tiles and anything else with a Shadow Caster 2D. Each shadow-casting light costs more to draw.")]
    public bool castShadows = true;

    [Tooltip("How dark the shadows are: the Light2D's shadow Strength.")]
    [Range(0f, 1f)]
    public float shadowStrength = 0.75f;

    [Tooltip("How soft the edges of the shadows are: the Light2D's shadow Softness.")]
    [Range(0f, 1f)]
    public float shadowSoftness = 0.3f;

    [Tooltip("How quickly the soft edge fades: the Light2D's shadow Falloff Strength.")]
    [Range(0f, 1f)]
    public float shadowFalloffStrength = 0.5f;

    // Goes up whenever a value is edited, so lights know to pick up the change while playing.
    public int Version { get; private set; }

    private static GlobalLightSettings current;

    public static GlobalLightSettings Current
    {
        get
        {
            if (!current)
            {
                current = Resources.Load<GlobalLightSettings>(ResourceName);

                if (!current)
                {
                    current = CreateInstance<GlobalLightSettings>();
                    current.hideFlags = HideFlags.DontSave;

                    Debug.LogWarning(
                        $"No {ResourceName} asset found in a Resources folder, so lights use the default shadow settings. " +
                        "Create one with Assets > Create > Lighting > Global Light Settings.");
                }
            }

            return current;
        }
    }

    // Lets Unity enter play mode without reloading scripts, which would otherwise keep an old copy.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCurrent()
    {
        current = null;
    }

    private void OnValidate()
    {
        Version++;
    }

    // Puts the shadow settings on a light: these shared ones, or the light's own when it's overriding them.
    public static void Apply(
        Light2D light, bool overrideGlobal, bool ownCastShadows, float ownStrength, float ownSoftness, float ownFalloffStrength)
    {
        if (overrideGlobal)
        {
            light.shadowsEnabled = ownCastShadows;
            light.shadowIntensity = ownStrength;
            light.shadowSoftness = ownSoftness;
            light.shadowSoftnessFalloffIntensity = ownFalloffStrength;
            return;
        }

        var shared = Current;

        light.shadowsEnabled = shared.castShadows;
        light.shadowIntensity = shared.shadowStrength;
        light.shadowSoftness = shared.shadowSoftness;
        light.shadowSoftnessFalloffIntensity = shared.shadowFalloffStrength;
    }
}
