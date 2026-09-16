using UnityEngine;

public class MovementSettings : MonoBehaviour
{
    public string identifier;

    public float jumpForce;
    public float maxMoveSpeed;
    public float moveSpeedIncrease;
    public float moveSpeedDecay;
    [Tooltip("How quickly the player's fall speed will increase when going down.")]
    public float fallAcceleration;
    [Tooltip("How quickly the player's upward speed will decrease when holding down the jump button.")]
    public float heldJumpDecay;
    [Tooltip("How quickly the player's upward speed will decrease when the jump button is released (should be higher than \"Held Jump Decay\".")]
    public float releasedJumpDecay;
    public float maxFallSpeed;
    public float gravity;
    public float linearDampening;
    public float angularDampening;
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
