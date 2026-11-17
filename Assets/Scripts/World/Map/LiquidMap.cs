using UnityEngine;

// Marks a tilemap as full of a liquid: anything on one of its tiles is in it.
public class LiquidMap : MonoBehaviour
{
    [Tooltip("What the liquid is. Water if it's left empty.")]
    public Fluid fluid;

    public Fluid Fluid
    {
        get { return fluid ? fluid : Fluid.Water; }
    }
}
