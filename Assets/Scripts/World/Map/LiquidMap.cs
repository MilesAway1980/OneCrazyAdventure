using UnityEngine;

// Marks a tilemap as full of a liquid: anything on one of its tiles is in it.
public class LiquidMap : MonoBehaviour
{
    [Min(0.01f)]
    [Tooltip("How dense the liquid is, compared with water, which is 1. A character denser than this sinks in it, and a lighter one floats.")]
    public float density = 1f;

    [Min(0f)]
    [Tooltip("How thick the liquid is, compared with water, which is 1. Oil is a few times that and honey far more. The thicker it is, the slower things move through it.")]
    public float viscosity = 1f;

    [Min(0f)]
    [Tooltip("The least a kick or hop in this liquid lifts a character, in tiles, whatever its jump and the liquid's drag come to, so that every character can at least jump off the bottom and not be stuck. A deeper or thicker liquid can ask for more.")]
    public float minKickHeight = Fluid.DefaultMinKickHeight;

    private Fluid fluid;

    public Fluid Fluid
    {
        get
        {
            if (fluid == null || fluid.density != Mathf.Max(0.01f, density) || fluid.viscosity != Mathf.Max(0f, viscosity)
                || fluid.minKickHeight != Mathf.Max(0f, minKickHeight))
            {
                fluid = new Fluid("Liquid", density, viscosity, minKickHeight);
            }

            return fluid;
        }
    }
}
