using System;
using System.Collections.Generic;
using UnityEngine;

// How a character moves, worked out from what it is: what it's made of and how much it weighs. The MovementTuning says how
// weight becomes speed, jumping and falling; the material says whether it floats. A few traits and overrides are there to push
// a character away from what the formula says, for the ones that are meant to be special.
//
// A character that changes how it moves while it does something (a balloon floating, a ball inflated or rolling) has modes,
// which are changes made to the numbers while that is going on. In a liquid, whatever the mode, it moves as it should there.
[CreateAssetMenu(menuName = "Movement/Profile", fileName = "Profile")]
public class MovementProfile : ScriptableObject
{
    [Header("What it is")]
    [Tooltip("What it's made of. Its density against a liquid's is whether it floats or sinks. Nothing means the density of water.")]
    public CharacterMaterial material;

    [Tooltip("How much it weighs. This is also its weight on the Rigidbody2D, so it's what a button feels and what pushing it takes.")]
    [Min(0.01f)]
    public float mass = 1f;

    [Tooltip("Whether the Rigidbody2D's mass is set to the mass above when the game starts.")]
    public bool applyMass = true;

    [Header("How it differs from others of its weight")]
    public MovementTraits traits = new MovementTraits();

    [Header("When it does something")]
    [Tooltip("Changes made while it's doing something. A character looks for the one named by what it's doing, such as \"float\".")]
    public List<MovementMode> modes = new List<MovementMode>();

    [Header("Fixed values")]
    [Tooltip("Values to use instead of what the formula gives. Anything left at -1 is worked out.")]
    public MovementOverrides overrides = new MovementOverrides();

    // How dense it is, compared with water.
    public float Density
    {
        get { return material ? Mathf.Max(0.0001f, material.density) : 1f; }
    }

    // The mode of that name, or none (which changes nothing) if it doesn't have one.
    public MovementMode FindMode(string modeName)
    {
        if (!string.IsNullOrEmpty(modeName) && modeName != "base")
        {
            foreach (var mode in modes)
            {
                if (string.Equals(mode.name, modeName, StringComparison.OrdinalIgnoreCase))
                {
                    return mode;
                }
            }
        }

        return MovementMode.Plain;
    }

    private static float Auto(float given, float worked)
    {
        return given >= 0f ? given : worked;
    }

    // Works out how this character moves in a mode, in a liquid (or in the air, if there isn't one), into the result given. It
    // is filled in rather than made, as this happens every frame.
    public void Resolve(string modeName, Fluid fluid, MovementTuning t, ResolvedMovement r)
    {
        var mode = FindMode(modeName);
        var heavy = Mathf.Max(0.0001f, mass / Mathf.Max(0.0001f, t.referenceMass));

        var topSpeed = Auto(overrides.topSpeed, t.topSpeed * Mathf.Pow(heavy, -t.speedMassExponent) * traits.runSpeed) * mode.speed;
        var timeToTop = Auto(overrides.timeToTopSpeed, t.timeToTopSpeed * Mathf.Pow(heavy, t.accelerationMassExponent) / Mathf.Max(0.01f, traits.acceleration)) / Mathf.Max(0.01f, mode.acceleration);
        var timeToStop = Auto(overrides.timeToStop, t.timeToStop * Mathf.Pow(heavy, t.brakingMassExponent) / Mathf.Max(0.01f, traits.acceleration)) / Mathf.Max(0.01f, mode.braking);
        var jumpHeight = Auto(overrides.jumpHeight, t.jumpHeight * Mathf.Pow(heavy, -t.jumpMassExponent) * traits.jump) * mode.jumpHeight;
        var riseGravity = t.riseGravity * mode.riseGravity;
        var fallGravity = Auto(overrides.fallGravity, t.fallGravity * Mathf.Pow(heavy, t.fallMassExponent) / Mathf.Max(0.01f, traits.floatiness)) * mode.fallGravity;
        var terminalSpeed = Auto(overrides.terminalSpeed, t.terminalSpeed * Mathf.Pow(heavy, t.terminalMassExponent)) * mode.terminalSpeed;
        var jumpCut = t.jumpCut;

        if (fluid)
        {
            // A liquid slows him, and his jump is a kick through it. The extra pull that makes a jump snappy or a fall heavy is
            // for the air: the liquid's own drag does that job here.
            var drag = 1f / (1f + t.swimDrag * fluid.viscosity);

            topSpeed *= drag;
            jumpHeight *= t.swimKick * drag;
            riseGravity = 1f;
            fallGravity = 1f;
            jumpCut = 1f;
        }

        var gravity = Mathf.Abs(Physics2D.gravity.y) * t.characterGravity;
        var rise = Mathf.Max(0.0001f, gravity * riseGravity);
        var fall = Mathf.Max(0.0001f, gravity * fallGravity);
        var jumpSpeed = Mathf.Sqrt(2f * rise * Mathf.Max(0f, jumpHeight));

        // How much speed is gained or lost in one 0.02 second step to take that long.
        const float step = 0.02f;

        r.maxMoveSpeed = topSpeed;
        r.moveSpeedIncrease = topSpeed * step / Mathf.Max(timeToTop, step);
        r.moveSpeedDecay = topSpeed * step / Mathf.Max(timeToStop, step);
        r.jumpForce = jumpSpeed;
        r.heldJumpDecay = riseGravity;
        r.releasedJumpDecay = riseGravity * jumpCut;
        r.fallAcceleration = fallGravity;
        r.maxFallSpeed = terminalSpeed;
        r.climbSpeed = Auto(overrides.climbSpeed, t.climbSpeed * traits.runSpeed);
        r.gravity = t.characterGravity;
        r.linearDampening = t.airDamping;
        r.angularDampening = t.angularDamping;
        r.density = Density * Mathf.Max(0.0001f, mode.density);

        // What a designer wants to read off.
        r.timeToTopSpeed = Mathf.Max(timeToTop, step);
        r.timeToStop = Mathf.Max(timeToStop, step);
        r.stopDistance = topSpeed * r.timeToStop * 0.5f;
        r.jumpHeight = jumpHeight;
        r.apexTime = jumpSpeed / rise;
        r.fallTime = Mathf.Sqrt(2f * Mathf.Max(0f, jumpHeight) / fall);
        r.jumpDistance = topSpeed * (r.apexTime + r.fallTime);
    }

    // What a liquid does to this character at rest in it, for reading off: how much of it is under (1 if it sinks), and how
    // fast it sinks or the most it is held up by.
    public void DescribeIn(Fluid fluid, MovementTuning t, string modeName, out bool floats, out float submergedAtRest, out float speed)
    {
        var density = Density * Mathf.Max(0.0001f, FindMode(modeName).density);
        var ratio = Mathf.Min(fluid.density / density, t.maxBuoyancyRatio);
        var damping = t.airDamping + fluid.viscosity * t.fluidDamping;
        var pull = Mathf.Abs(Physics2D.gravity.y) * t.characterGravity;

        floats = ratio > 1f;
        submergedAtRest = floats ? 1f / ratio : 1f;
        speed = Mathf.Abs(pull * (1f - ratio)) / Mathf.Max(0.0001f, damping);
    }
}

// How a character's movement is changed while it's doing something. Everything is a multiple, so 1 changes nothing.
[Serializable]
public class MovementMode
{
    public static readonly MovementMode Plain = new MovementMode();

    [Tooltip("What this is for, as a character names it: \"float\", \"bouncy\", \"rolling\".")]
    public string name = "float";

    [Min(0f)] public float speed = 1f;
    [Tooltip("Over 1 gets up to speed faster.")]
    [Min(0f)] public float acceleration = 1f;
    [Tooltip("Over 1 stops faster.")]
    [Min(0f)] public float braking = 1f;
    [Min(0f)] public float jumpHeight = 1f;
    [Tooltip("The pull of gravity going up. A big number stops a jump going up almost at once.")]
    [Min(0f)] public float riseGravity = 1f;
    [Tooltip("The pull of gravity falling. A small number is a slow fall.")]
    [Min(0f)] public float fallGravity = 1f;
    [Min(0f)] public float terminalSpeed = 1f;
    [Tooltip("How dense it is while this is going on, as a multiple of what it's made of. A small number is something filled with air, which floats in water and can rise in the air.")]
    [Min(0f)] public float density = 1f;
}

// How a character differs from others of its weight.
[Serializable]
public class MovementTraits
{
    [Tooltip("How fast it runs, compared with others of its weight.")]
    [Range(0.2f, 3f)] public float runSpeed = 1f;
    [Tooltip("How quickly it gets going and stops, compared with others of its weight.")]
    [Range(0.2f, 3f)] public float acceleration = 1f;
    [Tooltip("How high it jumps, compared with others of its weight.")]
    [Range(0.2f, 3f)] public float jump = 1f;
    [Tooltip("How slowly it falls, compared with others of its weight. Over 1 is lighter on its feet.")]
    [Range(0.2f, 3f)] public float floatiness = 1f;
}

// Values that are used as they are instead of being worked out. -1 means to work it out.
[Serializable]
public class MovementOverrides
{
    [Tooltip("Tiles per second.")] public float topSpeed = -1f;
    [Tooltip("Seconds.")] public float timeToTopSpeed = -1f;
    [Tooltip("Seconds.")] public float timeToStop = -1f;
    [Tooltip("Tiles.")] public float jumpHeight = -1f;
    [Tooltip("Times normal gravity.")] public float fallGravity = -1f;
    [Tooltip("Tiles per second.")] public float terminalSpeed = -1f;
    [Tooltip("Tiles per second.")] public float climbSpeed = -1f;
}

// What a profile works out for how a character moves right now, in the form the movement code uses. The first block is that;
// the rest is for reading.
public class ResolvedMovement
{
    public float jumpForce;
    public float maxMoveSpeed;
    public float moveSpeedIncrease;
    public float moveSpeedDecay;
    public float fallAcceleration;
    public float heldJumpDecay;
    public float releasedJumpDecay;
    public float maxFallSpeed;
    public float gravity;
    public float linearDampening;
    public float angularDampening;
    public float climbSpeed;

    // How dense the character is now, compared with water.
    public float density;

    public float timeToTopSpeed;
    public float timeToStop;
    public float stopDistance;
    public float jumpHeight;
    public float apexTime;
    public float fallTime;
    public float jumpDistance;
}
