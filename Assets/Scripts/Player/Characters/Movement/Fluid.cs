using UnityEngine;

// A liquid that characters can be in, such as water or oil. It holds a character up according to how dense it is compared
// with the character, and slows it down according to how thick it is. A LiquidMap says which one it is.
[CreateAssetMenu(menuName = "Movement/Fluid", fileName = "Fluid")]
public class Fluid : ScriptableObject
{
    private static Fluid water;

    // What a liquid is if nothing says otherwise.
    public static Fluid Water
    {
        get
        {
            if (!water)
            {
                water = Resources.Load<Fluid>("Fluids/Water");

                if (!water)
                {
                    water = CreateInstance<Fluid>();
                    water.name = "Water (default)";
                }
            }

            return water;
        }
    }

    [Tooltip("How dense it is, compared with water, which is 1. A character denser than this sinks in it, and a lighter one floats.")]
    [Min(0.01f)]
    public float density = 1f;

    [Tooltip("How thick it is, compared with water, which is 1. Oil is a few times that and honey far more. The thicker it is, the slower things move through it and the more slowly a floating thing settles.")]
    [Min(0f)]
    public float viscosity = 1f;
}
