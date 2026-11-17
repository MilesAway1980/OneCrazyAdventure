using UnityEngine;

// The numbers behind how every character moves. A character's weight is turned into how fast it runs, how quickly it gets going
// and stops, how high it jumps and how fast it falls by the formula these set, so changing one here changes every character that
// goes by it. The liquids and the air hold characters up and slow them according to these as well.
//
// There is only one of these. It lives in a Resources folder so the game can find it. Make it with Tools > Movement > Create
// Default Assets, and change it while playing: the changes are kept.
[CreateAssetMenu(menuName = "Movement/Tuning", fileName = "MovementTuning")]
public class MovementTuning : ScriptableObject
{
    private static MovementTuning current;

    public static MovementTuning Current
    {
        get
        {
            if (!current)
            {
                current = Resources.Load<MovementTuning>("MovementTuning");

                if (!current)
                {
                    Debug.LogWarning("There's no MovementTuning in a Resources folder, so the default numbers are being used. " +
                                     "Make one with Tools > Movement > Create Default Assets.");
                    current = CreateInstance<MovementTuning>();
                }
            }

            return current;
        }
    }

    [Header("A character of reference weight")]
    [Tooltip("The weight that the numbers below are for. A character twice as heavy gets the heavier side of every formula below.")]
    [Min(0.01f)]
    public float referenceMass = 1f;

    [Tooltip("How fast it runs, in tiles per second.")]
    [Min(0f)]
    public float topSpeed = 5f;

    [Tooltip("How long it takes to get up to top speed from standing, in seconds.")]
    [Min(0f)]
    public float timeToTopSpeed = 0.15f;

    [Tooltip("How long it takes to stop from top speed once the player lets go, in seconds.")]
    [Min(0f)]
    public float timeToStop = 0.15f;

    [Tooltip("How high it jumps with the button held, in tiles.")]
    [Min(0f)]
    public float jumpHeight = 4f;

    [Tooltip("How hard gravity pulls while it is going up with the jump button held, as a multiple of the character's normal gravity. This and the jump height decide how fast the jump goes up.")]
    [Min(0.01f)]
    public float riseGravity = 1f;

    [Tooltip("How many times harder gravity pulls going up once the jump button is let go: the shorter the press, the shorter the jump.")]
    [Min(1f)]
    public float jumpCut = 5f;

    [Tooltip("How hard gravity pulls while it is falling, as a multiple of normal. Over 1 is a snappy, heavy fall.")]
    [Min(0.01f)]
    public float fallGravity = 2.5f;

    [Tooltip("The fastest it can fall, in tiles per second.")]
    [Min(0f)]
    public float terminalSpeed = 25f;

    [Tooltip("How fast it climbs a ladder, in tiles per second. (Only for characters that can climb.)")]
    [Min(0f)]
    public float climbSpeed = 2f;

    [Header("How weight changes it")]
    [Tooltip("How much slower a heavy character runs, and a light one faster. 0 is no difference. A character twice as heavy runs 2 to the power of this less fast.")]
    [Range(0f, 1f)]
    public float speedMassExponent = 0.15f;

    [Tooltip("How much longer a heavy character takes to get up to speed. 0 is no difference.")]
    [Range(0f, 1.5f)]
    public float accelerationMassExponent = 0.4f;

    [Tooltip("How much longer a heavy character takes to stop. 0 is no difference.")]
    [Range(0f, 1.5f)]
    public float brakingMassExponent = 0.4f;

    [Tooltip("How much lower a heavy character jumps. 0 is no difference.")]
    [Range(0f, 1f)]
    public float jumpMassExponent = 0.25f;

    [Tooltip("How much harder a heavy character falls. 0 is no difference.")]
    [Range(0f, 1f)]
    public float fallMassExponent = 0.15f;

    [Tooltip("How much faster a heavy character can fall at the most. 0 is no difference.")]
    [Range(0f, 1f)]
    public float terminalMassExponent = 0.2f;

    [Header("The world")]
    [Tooltip("The pull of gravity on characters, as a multiple of the world's gravity.")]
    [Min(0.01f)]
    public float characterGravity = 2f;

    [Tooltip("How much the air slows a moving character down on its own. 0 means jumps go as high as the numbers above say.")]
    [Min(0f)]
    public float airDamping = 0f;

    [Tooltip("How much the air slows a spinning character down.")]
    [Min(0f)]
    public float angularDamping = 0.05f;

    [Tooltip("How dense the air is, compared with water, which is 1. Something lighter than this rises in air.")]
    [Min(0f)]
    public float airDensity = 0.0012f;

    [Header("In liquids")]
    [Tooltip("How much a liquid slows walking and swimming. A liquid's thickness is multiplied by this: 0.5 makes water, at 1, slow a character to two thirds.")]
    [Min(0f)]
    public float swimDrag = 0.5f;

    [Tooltip("How much of a jump's height a character gets as a kick in a liquid, before the liquid's drag.")]
    [Min(0f)]
    public float swimKick = 1f;

    [Tooltip("How much a liquid's thickness slows a character that is moving through it. This is also what settles a floating character down after it bobs: the higher it is, the fewer bobs.")]
    [Min(0f)]
    public float fluidDamping = 4f;

    [Tooltip("The most a liquid can lift a character, as a multiple of its weight. A lighter thing than this would rise too hard and leap out, so it floats a little deeper instead.")]
    [Min(1f)]
    public float maxBuoyancyRatio = 3f;
}
