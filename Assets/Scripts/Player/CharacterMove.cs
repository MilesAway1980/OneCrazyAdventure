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

    private Character currentCharacter;
    private Player player;
    
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

        Vector2 moveValue = moveAction.ReadValue<Vector2>();
        Vector2 lookValue = lookAction.ReadValue<Vector2>();

        // A hub menu this player has open takes all of their buttons, and their character stands still while it's open.
        if (HubMenu.IsOpenFor(player))
        {
            currentCharacter.DirectionPress(Vector2.zero);
            currentCharacter.Jump(false);
            currentCharacter.Action(false);
            currentCharacter.InteractHold(false);

            HubMenu.Feed(
                player,
                moveValue,
                jumpAction.WasPressedThisFrame(),
                abilityAction.WasPressedThisFrame(),
                interactAction.WasPressedThisFrame());

            // Kept level with the buttons while the menu has them, so that one still held when it closes isn't taken for a new
            // press, and one that was let go isn't taken for still down.
            SetPressed(jumpAction, jumpAction.IsPressed());
            SetPressed(abilityAction, abilityAction.IsPressed());
            SetPressed(interactAction, interactAction.IsPressed());
            SetPressed(prevAction, prevAction.IsPressed());
            SetPressed(nextAction, nextAction.IsPressed());

            return;
        }

        currentCharacter.DirectionPress(moveValue);

        // Before the other buttons, so a character that wants one button held while another goes down sees it held when
        // both are pressed on the same frame.
        currentCharacter.InteractHold(interactAction.IsPressed());

        CameraManager.Look(player, lookValue);

        if (jumpAction.IsPressed())
        {
            currentCharacter.Jump(true);
            SetPressed(jumpAction, true);
        }
        else
        {
            currentCharacter.Jump(false);
            SetPressed(jumpAction, false);
        }

        if (abilityAction.IsPressed())
        {
            if (!GetPressed(abilityAction))
            {
                SetPressed(abilityAction, true);
                currentCharacter.FirstActionPress();
            }

            //WasPressedThisFrame is for the button press (instant)
            //WasPerformedThisFrame has the DefaultHoldTime delay (default is 0.4 seconds).  Same as useAction.triggered
            //WasCompletedThisFrame has a release timer (Need to confirm which value)
            //WasReleasedThisFrame is for the button release (instant)
            
            currentCharacter.Action(true);
        }
        else
        {
            SetPressed(abilityAction, false);
            currentCharacter.Action(false);
        }

        if (prevAction.IsPressed())
        {
            if (!GetPressed(prevAction))
            {
                SetCurrentCharacter(Player.GetAvailableCharacter(player, true));
                player.AssignCharacter(currentCharacter);
                SetPressed(prevAction, true);
            }
        }
        else
        {
            SetPressed(prevAction, false);
        }

        if (nextAction.IsPressed())
        {
            if (!GetPressed(nextAction))
            {
                SetCurrentCharacter(Player.GetAvailableCharacter(player, false));
                player.AssignCharacter(currentCharacter);
                SetPressed(nextAction, true);
            }
        }
        else
        {
            SetPressed(nextAction, false);
        }

        if (zoomInAction.IsPressed())
        {
            CameraManager.Zoom(player, 1);
            SetPressed(zoomInAction, true);
        }
        else
        {
            SetPressed(zoomInAction, false);
        }

        if (zoomOutAction.IsPressed())
        {
            CameraManager.Zoom(player, -1);
            SetPressed(zoomOutAction, true);
        }
        else
        {
            SetPressed(zoomOutAction, false);
        }
        
        if (lookLock.IsPressed())
        {
            if (!GetPressed(lookLock))
            {
                CameraManager.Lock(player);
                SetPressed(lookLock, true);
            }
        }
        else
        {
            SetPressed(lookLock, false);
        }

        if (interactAction.IsPressed())
        {
            if (!GetPressed(interactAction))
            {
                SetPressed(interactAction, true);
                currentCharacter.Interact();
            }
        }
        else
        {
            SetPressed(interactAction, false);
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
        SetCurrentCharacter(null);
    }

    public void AssignCharacter(Character c)
    {
        SetCurrentCharacter(c);
    }

    // Changes who the player controls, telling the character they're leaving and the one they're taking over.
    private void SetCurrentCharacter(Character c)
    {
        if (c == currentCharacter)
        {
            return;
        }

        if (currentCharacter)
        {
            currentCharacter.ControlLost();
        }

        currentCharacter = c;

        if (currentCharacter)
        {
            currentCharacter.ControlGained();
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
