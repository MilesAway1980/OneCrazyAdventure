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

    private Character currentCharacter;
    private Player player;
    
    PlayerInput playerInput;

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
        }

        if (!currentCharacter)
        {
            return;
        }

        Vector2 moveValue = moveAction.ReadValue<Vector2>();
        Vector2 lookValue = lookAction.ReadValue<Vector2>();

        currentCharacter.DirectionPress(moveValue);
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
                currentCharacter = Player.GetAvailableCharacter(player, true);
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
                currentCharacter = Player.GetAvailableCharacter(player, false);
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

    public void AssignCharacter(Character c)
    {
        currentCharacter = c;
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
