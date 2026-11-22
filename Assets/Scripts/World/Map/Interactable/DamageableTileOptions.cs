using UnityEngine;
using UnityEngine.Serialization;

// What can destroy a tile, and how. The inspector only shows the settings of the kinds of damage that are switched on, and
// the sides when the damage depends on them (see DamageableTileOptionsEditor).
public class DamageableTileOptions : MonoBehaviour
{
    [Tooltip("Destroyed by a rolling ball that is going fast enough.")]
    public bool roll;
    // Not built, and it isn't clear what it was meant to do, so it's out of the inspector for now.
    //[Tooltip("Not built yet.")]
    //public bool wood;
    [Tooltip("Destroyed after something has stood on it for a while.")]
    public bool standOn;
    [Tooltip("Destroyed after something has touched it, from the sides that are ticked, for a while.")]
    public bool touch;
    [Tooltip("Dug through by a digger, such as the Drill, from the sides that are ticked: to be drilled upwards, tick Down, and " +
             "to be drilled downwards, tick Up. It isn't destroyed until the digger is all the way through, and it doesn't " +
             "come back while anything is in its space.")]
    public bool dig;
    [Tooltip("Caught alight by something that gives off heat (a HeatSource) touching it for a while. A tile that is alight " +
             "sets fire to the tiles next to it that are damaged by fire too, and so on, and burns away.")]
    public bool fire;
    [Tooltip("Blown apart by an explosion that reaches it, if it is strong enough.")]
    public bool dynamite;

    [Tooltip("How long, in seconds, it stays destroyed before it comes back. 0 is for the rest of the game. Below 0 is meant " +
             "to be for good, even after the game is saved, which isn't built yet.")]
    public float timeStayDestroyed;

    public enum BreakEffect
    {
        None,
        Particles,
        Debris
    }

    [Tooltip("What shows when a tile is destroyed. Particles plays an effect where it was. Debris breaks its picture into four " +
             "pieces that fly off.")]
    public BreakEffect breakEffect;
    [Tooltip("The effect to play where the tile was. It's removed when it has finished playing.")]
    public GameObject breakParticles;
    [Tooltip("A sound to play when the tile is destroyed.")]
    public AudioClip breakSound;

    [Tooltip("The slowest and fastest, in units per second, that a piece of debris is thrown out, though it's always thrown " +
             "upward a little as well.")]
    public float debrisMinSpeed = 2f;
    [Tooltip("The fastest, in units per second, that a piece of debris is thrown out. See Debris Min Speed.")]
    public float debrisMaxSpeed = 5f;
    [Range(0f, 90f)]
    [Tooltip("How far, in degrees, either way of the direction it was hit, each piece of debris can be thrown, chosen at random. " +
             "At 0 they all go the same way. When nothing hit it, it's either way of straight out from the middle of the tile.")]
    public float debrisSpread = 45f;
    [Range(0f, 1f)]
    [Tooltip("How much of the speed of whatever hit the tile goes with the pieces as well, so a harder hit throws them " +
             "further. 0 is none, and 1 is all of it.")]
    public float debrisImpactShare = 0.3f;
    [Tooltip("The fastest, in degrees per second, that a piece of debris spins, either way.")]
    public float debrisSpin = 360f;
    [Tooltip("How strongly gravity pulls a piece of debris down.")]
    public float debrisGravityScale = 1f;
    [Tooltip("How long, in seconds, a piece of debris lasts. It fades away over the last part of it.")]
    public float debrisSeconds = 3f;

    [Tooltip("How fast the ball has to be rolling to destroy it.")]
    public float rollSpeedNeeded;
    [Tooltip("How long, in seconds, something has to stand on it, without getting off, before it is destroyed. If it gets " +
             "off before then, the time starts over.")]
    public float standOnTimeNeeded;
    [Tooltip("How much has to be standing on it for the time to count, going by the mass of what is on it. 0 is any weight. " +
             "Only things with a CanTriggerFloorCollapse count.")]
    public float standOnWeightNeeded;
    [Tooltip("On, the weights of everything standing on it are added up, so any number of light things can add up to enough. " +
             "Off, it has to be a single thing that is heavy enough, and the rest don't help.")]
    public bool standOnWeightsAdd = true;

    [Min(0.01f)]
    [Tooltip("How hard it is to dig through. A digger gets through a tile in its hardness divided by the digger's power, in " +
             "seconds: with a power of 5, a hardness of 5 takes a second, and 10 takes two.")]
    public float digHardness = 5f;
    [Min(0)]
    [Tooltip("Makes it dug through as one bigger block. 0 is just this tile. 1 is a block 3 tiles wide and 3 long, 2 is 5 by " +
             "5, and so on, with this tile as the middle of the face the digger meets. The digger takes the hardness's " +
             "time to get through the whole block, and all of it breaks together. Only tiles in the block that can be dug at " +
             "all (from any side) are part of it, and it stops short at the first one in the middle that can't.")]
    public int digRadius;

    [Min(0f)]
    [Tooltip("How intense the heat has to be for it to catch fire, by the Fire Intensity of the heat source. A source that " +
             "isn't intense enough doesn't heat it at all, however long it is there. 0 is any heat.")]
    public float fireIntensityNeeded;
    [Min(0f)]
    [Tooltip("How close, in units, something giving off heat has to be to start heating it, measured from its edge to the " +
             "tile's. 0 is touching it.")]
    public float fireReach;
    [Tooltip("How long, in seconds, something giving off heat has to be that close, without moving away, before it catches " +
             "fire. If it moves away before then, the time starts over.")]
    public float fireIgniteSeconds = 1f;
    [Min(0f)]
    [Tooltip("How long, in seconds, it takes to cool from just about to catch fire back to cold once the heat has gone. " +
             "If the heat comes back first, it carries on from how warm it still is. 0 is cold straight away.")]
    public float fireCoolSeconds = 2f;
    [Tooltip("How long, in seconds, after it catches fire that it sets fire to the tiles next to it.")]
    public float fireSpreadSeconds = 0.5f;
    [Min(0f)]
    [Tooltip("How intense the fire is when it burns, for setting fire to the tiles next to it: it spreads to a tile that needs " +
             "no more intensity than this. It always burns at least as intensely as it needed to catch, so tiles that need the " +
             "same as each other set each other alight without this. Set it higher to burn a section that needs more.")]
    public float fireBurnIntensity;
    [Tooltip("How long, in seconds, it burns for before it is destroyed.")]
    public float fireBurnSeconds = 2f;
    [Tooltip("Shows that it is about to catch fire, as the heat touches it, by glowing red. Off, there is no sign until it " +
             "catches.")]
    public bool fireWarning;
    [Range(0f, 1f)]
    [Tooltip("How red it glows by the time it catches. 0 is not at all, and 1 is as red as it gets.")]
    public float fireGlowAmount = 0.7f;
    [Range(0f, 1f)]
    [Tooltip("How burnt black it is by the time it is destroyed. 0 is not at all, and 1 is black.")]
    public float fireCharAmount = 0.8f;
    [Tooltip("An effect to show while it burns, such as flames, put where the tile is. It's removed when the tile is " +
             "destroyed, so it should loop.")]
    public GameObject fireFlames;

    public enum WarningType
    {
        Darken,
        Shake,
        DarkenAndShake
    }

    [Tooltip("How long, in seconds, something has to touch it, without letting go, before it is destroyed. If it lets go " +
             "before then, the time starts over.")]
    public float touchTimeNeeded;

    [FormerlySerializedAs("standOnWarning")]
    [Tooltip("Shows that it is about to give way, as the time runs, for Stand On and Touch. Off, there is no sign until it goes.")]
    public bool crumbleWarning;
    [FormerlySerializedAs("standOnWarningType")]
    [Tooltip("How it shows it. Darken goes darker as the time runs. Shake shakes more and more.")]
    public WarningType crumbleWarningType;
    [Range(0f, 1f)]
    [Tooltip("How dark it is by the time it goes. 0 is not at all, and 1 is black.")]
    public float warningDarkness = 0.5f;
    [Tooltip("How far, in units, it shakes at the most.")]
    public float warningShake = 0.04f;

    public enum DynamiteResult
    {
        Fly,
        Break
    }

    [Tooltip("What a blast does to it. Fly knocks the whole tile loose as one solid block, which is thrown away from the blast, " +
             "lands, and fades after the debris time below. Break destroys it like any other damage, with the Break Effect " +
             "chosen below, and any debris is thrown out from the blast.")]
    public DynamiteResult dynamiteResult;
    [Min(0.01f)]
    [Tooltip("How heavy the block is when it is blown loose. A heavier one is thrown less far by the same blast.")]
    public float dynamiteMass = 1f;
    [Min(0f)]
    [Tooltip("How strong the blast has to be where the tile is, which is the explosion's force times how far in from the edge " +
             "of its reach the tile is. A tile further from the blast feels less of it. 0 is any blast that reaches it.")]
    public float dynamiteForceNeeded;

    [Tooltip("Damaged from any side. Otherwise, only from the sides that are ticked.")]
    public bool All;
    [Tooltip("Damaged from above.")]
    public bool Up;
    [Tooltip("Damaged from below.")]
    public bool Down;
    [Tooltip("Damaged from the left.")]
    public bool Left;
    [Tooltip("Damaged from the right.")]
    public bool Right;
}
