using UnityEngine;

// What can destroy a tile, and how. The inspector only shows the settings of the kinds of damage that are switched on, and
// the sides when the damage depends on them (see DamageableTileOptionsEditor).
public class DamageableTileOptions : MonoBehaviour
{
    [Tooltip("Destroyed by a rolling ball that is going fast enough.")]
    public bool roll;
    [Tooltip("Not built yet.")]
    public bool wood;
    [Tooltip("Destroyed after something has stood on it for a while.")]
    public bool standOn;
    [Tooltip("Not built yet.")]
    public bool touch;
    [Tooltip("Not built yet.")]
    public bool dig;
    [Tooltip("Not built yet.")]
    public bool fire;
    [Tooltip("Blown loose by Dynamite.")]
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
    [Tooltip("How long, in seconds, something has to stand on it before it is destroyed.")]
    public float standOnTimeNeeded;

    [Tooltip("How heavy the tile is when it is blown loose and falls.")]
    public float dynamiteMass;
    [Tooltip("Not used yet.")]
    public float dynamiteForceNeeded;

    [Tooltip("Damaged from any side. Otherwise, only from the sides that are ticked.")]
    public bool All;
    public bool Up;
    public bool Down;
    public bool Left;
    public bool Right;
}
