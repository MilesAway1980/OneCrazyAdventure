using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Characters;

public class CharacterMove : MonoBehaviour
{
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction abilityAction;
    private InputAction interactAction;
    private InputAction sprintAction;
    private InputAction attackAction;
    private InputAction prevAction;
    private InputAction nextAction;
    private InputAction zoomInAction;
    private InputAction zoomOutAction;
    private InputAction lookAction;
    private InputAction lookLock;
    private InputAction leaveAction;

    private Player player;

    // Who this player is controlling. Not kept here: CharacterControl has the record of it.
    private Character currentCharacter => player ? player.character : null;
    
    PlayerInput playerInput;

    // How far through holding the leave button the player is, from 0 (not holding it) up to 1 (they've left).
    public float LeaveProgress { get; private set; }

    private int inputDeviceNumber;
    //private bool firstPress;
    
    private Dictionary<InputAction, bool> inputActionPresses;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        player = GetComponent<Player>();
        inputActionPresses = new Dictionary<InputAction, bool>();

        inputDeviceNumber = playerInput.playerIndex;
        if (playerInput.currentControlScheme == null)
        {
            print ($"{player.name} does not have an available control scheme.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (playerInput.currentControlScheme == null)
        {
            return;
        }

        if (moveAction == null)
        {
            moveAction = playerInput.actions.FindAction("Move");
            jumpAction = playerInput.actions.FindAction("Jump");
            abilityAction = playerInput.actions.FindAction("Ability");
            interactAction = playerInput.actions.FindAction("Interact");
            sprintAction = playerInput.actions.FindAction("Sprint");
            attackAction = playerInput.actions.FindAction("Attack");
            prevAction = playerInput.actions.FindAction("PreviousCharacter");
            nextAction = playerInput.actions.FindAction("NextCharacter");
            zoomInAction = playerInput.actions.FindAction("ZoomIn");
            zoomOutAction = playerInput.actions.FindAction("ZoomOut");
            lookAction = playerInput.actions.FindAction("Look");
            lookLock = playerInput.actions.FindAction("LookLock");
            leaveAction = playerInput.actions.FindAction("Leave");
        }

        LeaveProgress = leaveAction != null && leaveAction.IsInProgress()
            ? leaveAction.GetTimeoutCompletionPercentage()
            : 0f;

        // The action is a hold, so this only happens once the button has been held for long enough. That keeps a
        // stray press from dropping someone out of the game.
        if (leaveAction != null && leaveAction.WasPerformedThisFrame())
        {
            PlayerManager.RemovePlayer(player);
            return;
        }

        if (!currentCharacter)
        {
            return;
        }

        // Read every frame, even when there's nothing to apply it to, so the record of which buttons are down stays level
        // with the real ones. A button still held when a menu closes, or a character is taken over, isn't a new press.
        var input = ReadInput();

        if (!currentCharacter)
        {
            return;
        }

        // A hub menu this player has open takes all of their buttons, and their character stands still while it's open.
        if (HubMenu.IsOpenFor(player))
        {
            currentCharacter.DirectionPress(Vector2.zero);
            currentCharacter.Jump(false);
            currentCharacter.Action(false);
            currentCharacter.InteractHold(false);

            HubMenu.Feed(player, input.move, input.jumpPressed, input.abilityPressed, input.interactPressed);
            return;
        }

        ApplyInput(currentCharacter, input);

        CameraManager.Look(player, lookAction.ReadValue<Vector2>());

        if (prevPressed)
        {
            CharacterControl.Switch(player.id, false);
        }

        if (nextPressed)
        {
            CharacterControl.Switch(player.id, true);
        }

        if (zoomInAction.IsPressed())
        {
            CameraManager.Zoom(player, 1);
        }

        if (zoomOutAction.IsPressed())
        {
            CameraManager.Zoom(player, -1);
        }

        if (lockPressed)
        {
            CameraManager.Lock(player);
        }
    }

    // Whether the buttons that only concern this player's own machine went down this frame. Set by ReadInput.
    private bool prevPressed;
    private bool nextPressed;
    private bool lockPressed;

    // Reads this player's controls. What the character needs goes in the frame; the buttons about this player's own screen
    // and about switching are left in the fields above.
    private PlayerInputFrame ReadInput()
    {
        var input = new PlayerInputFrame
        {
            move = moveAction.ReadValue<Vector2>(),
            jumpHeld = jumpAction.IsPressed(),
            abilityHeld = abilityAction.IsPressed(),
            interactHeld = interactAction.IsPressed()
        };

        input.jumpPressed = input.jumpHeld && !GetPressed(jumpAction);
        input.abilityPressed = input.abilityHeld && !GetPressed(abilityAction);
        input.interactPressed = input.interactHeld && !GetPressed(interactAction);

        var prevHeld = prevAction.IsPressed();
        var nextHeld = nextAction.IsPressed();
        var lockHeld = lookLock.IsPressed();

        prevPressed = prevHeld && !GetPressed(prevAction);
        nextPressed = nextHeld && !GetPressed(nextAction);
        lockPressed = lockHeld && !GetPressed(lookLock);

        SetPressed(jumpAction, input.jumpHeld);
        SetPressed(abilityAction, input.abilityHeld);
        SetPressed(interactAction, input.interactHeld);
        SetPressed(prevAction, prevHeld);
        SetPressed(nextAction, nextHeld);
        SetPressed(lookLock, lockHeld);

        return input;
    }

    // Does what a frame of input says to a character. This is the part that will run on the host, whichever machine the
    // player is really on.
    private static void ApplyInput(Character character, PlayerInputFrame input)
    {
        character.DirectionPress(input.move);

        // Before the other buttons, so a character that wants one button held while another goes down sees it held when
        // both are pressed on the same frame.
        character.InteractHold(input.interactHeld);

        character.Jump(input.jumpHeld);

        if (input.abilityPressed)
        {
            character.FirstActionPress();
        }

        character.Action(input.abilityHeld);

        if (input.interactPressed)
        {
            character.Interact();
        }
    }

    // The Player Input sends these messages when this player's controller is unplugged or runs out of battery, and
    // when it comes back.  The same device is reconnected to the player automatically.
    void OnDeviceLost()
    {
        print($"{name} lost their device.");
        ReleaseEverything();
    }

    void OnDeviceRegained()
    {
        print($"{name} got their device back.");
        ReleaseEverything();
    }

    // Lets go of everything the player was holding. The release of a button on a controller that has vanished is
    // never seen, so the character would otherwise carry on walking, jumping or using their action by themselves.
    private void ReleaseEverything()
    {
        LeaveProgress = 0f;

        if (inputActionPresses != null)
        {
            foreach (var action in inputActionPresses.Keys.ToList())
            {
                inputActionPresses[action] = false;
            }
        }

        if (currentCharacter)
        {
            currentCharacter.DirectionPress(Vector2.zero);
            currentCharacter.Jump(false);
            currentCharacter.Action(false);
            currentCharacter.InteractHold(false);
        }
    }

    // The player is leaving the game. Their character is let go of, mid-press or not, so it doesn't carry on walking or
    // using its action with nobody controlling it, and anyone else can take it over.
    public void ReleaseControl()
    {
        ReleaseEverything();

        if (player)
        {
            CharacterControl.Release(player.id);
        }
    }

    private bool GetPressed(InputAction action)
    {
        bool pressed;
        if (!inputActionPresses.TryGetValue(action, out pressed))
        {
            inputActionPresses.Add(action, false);
        }

        return pressed;
    }

    private void SetPressed(InputAction action, bool pressed)
    {
        inputActionPresses[action] = pressed;
    }
}
