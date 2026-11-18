using UnityEngine;

// A liquid that characters can be in, such as water or oil: how dense it is and how thick. It holds a character up according to how
// dense it is compared with the character, and slows it down according to how thick it is.
//
// It isn't an asset. A Liquid Area or a LiquidMap holds its own Density and Viscosity, and makes one of these from them for whatever
// asks what's there.
public sealed class Fluid
{
    // The least kick a liquid gives, unless it says otherwise.
    public const float DefaultMinKickHeight = 1.5f;

    // Water is what everything is measured against, so it's 1 for both.
    public static readonly Fluid Water = new Fluid("Water", 1f, 1f, DefaultMinKickHeight);

    // A little lighter than water, and a few times as thick.
    public static readonly Fluid Oil = new Fluid("Oil", 0.9f, 4f, DefaultMinKickHeight);

    // The liquids the Movement Overview and the Movement Lab show things in.
    public static readonly Fluid[] Presets = { Water, Oil };

    public readonly string name;

    // How dense it is, compared with water, which is 1. A character denser than this sinks in it, and a lighter one floats.
    public readonly float density;

    // How thick it is, compared with water, which is 1. Oil is a few times that and honey far more. The thicker it is, the slower
    // things move through it and the more slowly a floating thing settles.
    public readonly float viscosity;

    // The least height, in tiles, that a kick or hop in this liquid is worked out for, whatever a character's jump and the liquid's
    // drag come to, so that every character can at least jump off the bottom of a pool and not be stuck there. A thicker or deeper
    // liquid can ask for more.
    public readonly float minKickHeight;

    public Fluid(string name, float density, float viscosity, float minKickHeight = DefaultMinKickHeight)
    {
        this.name = name;
        this.density = Mathf.Max(0.01f, density);
        this.viscosity = Mathf.Max(0f, viscosity);
        this.minKickHeight = Mathf.Max(0f, minKickHeight);
    }
}
