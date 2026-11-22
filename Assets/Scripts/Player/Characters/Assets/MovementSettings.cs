using UnityEngine;

// The old way of setting up how a character moves, with a copy on every character in every scene. Nothing reads these any
// more: a character's MovementProfile does it instead. The component is kept only so scenes that still have them don't show
// missing scripts; Tools > Movement > Remove Old Movement Settings takes them off.
public class MovementSettings : MonoBehaviour
{
    [Tooltip("A name for this set of movement settings.")]
    public string identifier;

    [Tooltip("How hard the player pushes off the ground when they jump.")]
    public float jumpForce;
    [Tooltip("The fastest the player can walk.")]
    [Min(0f)]
    public float maxMoveSpeed;
    [Tooltip("How much walking speed is gained in a physics step (0.02 seconds) while the player holds a direction. Turning round also uses Move Speed Decay, to stop first. Multiply by 50 for speed gained per second.")]
    [Min(0f)]
    public float moveSpeedIncrease;
    [Tooltip("How much walking speed is lost in a physics step (0.02 seconds) while the player isn't steering. Multiply by 50 for speed lost per second.")]
    [Min(0f)]
    public float moveSpeedDecay;
    [Tooltip("How quickly the player's fall speed will increase when going down.")]
    public float fallAcceleration;
    [Tooltip("How quickly the player's upward speed will decrease when holding down the jump button.")]
    public float heldJumpDecay;
    [Tooltip("How quickly the player's upward speed will decrease when the jump button is released (should be higher than \"Held Jump Decay\".")]
    public float releasedJumpDecay;
    [Tooltip("The fastest the player can fall.")]
    public float maxFallSpeed;
    [Tooltip("How strongly gravity pulls the player down.")]
    public float gravity;
    [Tooltip("How much the player's movement is slowed by drag.")]
    public float linearDampening;
    [Tooltip("How much the player's turning is slowed by drag.")]
    public float angularDampening;
    [Tooltip("How fast the player climbs.")]
    public float climbSpeed;
    
    public void CloneSettings(MovementSettings newSettings)
    {
        jumpForce = newSettings.jumpForce;
        maxMoveSpeed = newSettings.maxMoveSpeed;
        moveSpeedIncrease = newSettings.moveSpeedIncrease;
        moveSpeedDecay = newSettings.moveSpeedDecay;
        fallAcceleration = newSettings.fallAcceleration;
        heldJumpDecay = newSettings.heldJumpDecay;
        releasedJumpDecay = newSettings.releasedJumpDecay;
        maxFallSpeed = newSettings.maxFallSpeed;
        gravity = newSettings.gravity;
        linearDampening = newSettings.linearDampening;
        angularDampening = newSettings.angularDampening;
        climbSpeed = newSettings.climbSpeed;
    }
}
