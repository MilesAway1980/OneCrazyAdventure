using Characters;
using Mirror;
using UnityEngine;

// Puts a character on the network. The host simulates every character, so on the host nothing changes: this only tells the other
// machines what they need. The character's position is sent by the network rigidbody next to this one. What's sent here is the
// rest of what a machine needs to show the character: which way they face, whether they're in the air, whether they're using
// their action, and whether they're in the game or put away.
//
// A machine that has joined someone else's game doesn't simulate the character. It only shows them, so the scripts that move
// them and use their abilities are switched off there (see CharacterParking), and the picture is chosen from what the host sends.
//
// Added to the characters by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Character))]
public class CharacterNetwork : NetworkBehaviour
{
    [SyncVar] private bool facingLeft;
    [SyncVar] private bool airborne;
    [SyncVar] private bool performing;
    [SyncVar(hook = nameof(OnInGameChanged))] private bool inGame = true;

    private Character character;

    private Character Character
    {
        get
        {
            if (!character)
            {
                character = GetComponent<Character>();
            }

            return character;
        }
    }

    void Update()
    {
        if (isServer)
        {
            // The host, which is also where playing alone or on one machine happens. What it simulates is what the others are told.
            facingLeft = Character.FacingLeft;
            airborne = Character.Airborne;
            performing = Character.IsPerformingAction();
            inGame = Character.InGame;
        }
        else if (isClient)
        {
            Character.ShowSprite(facingLeft, airborne, performing);
        }
    }

    // A character that is only shown is moved by the network and nothing else. Anything that was moving it when the machine joined,
    // or that tries to, can't carry it off.
    void FixedUpdate()
    {
        if (isClient && !isServer)
        {
            var body = GetComponent<Rigidbody2D>();

            if (body)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }
    }

    public override void OnStartClient()
    {
        if (isServer)
        {
            return;
        }

        // Only joined to a game: shown, not simulated, and in or out of the game as the host says.
        Character.SetShownOnly(true);
        Character.SetInGame(inGame);
    }

    public override void OnStopClient()
    {
        if (isServer)
        {
            return;
        }

        // Back to being simulated here, if the machine goes back to playing by itself.
        Character.SetShownOnly(false);
    }

    private void OnInGameChanged(bool oldValue, bool newValue)
    {
        if (!isServer)
        {
            Character.SetInGame(newValue);
        }
    }
}
