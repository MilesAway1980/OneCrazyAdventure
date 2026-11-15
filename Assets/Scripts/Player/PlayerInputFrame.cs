using UnityEngine;

// What a player's controls say about their character for one frame. This is everything the character needs from the player,
// and nothing else, so it can be read on one machine and applied on another: a player on the host applies it straight away,
// and a player on another machine will send it to the host.
//
// "Held" is whether a button is down right now. "Pressed" is that it went down this frame, which is what lets a button be
// acted on once however long it's held.
//
// Looking around, zooming and leaving the game are not in here. They are about the player's own screen and controller, so
// they stay on the machine the player is on.
public struct PlayerInputFrame
{
    public Vector2 move;

    public bool jumpHeld;
    public bool abilityHeld;
    public bool interactHeld;

    public bool jumpPressed;
    public bool abilityPressed;
    public bool interactPressed;
}
