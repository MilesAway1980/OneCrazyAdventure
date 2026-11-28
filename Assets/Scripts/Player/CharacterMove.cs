using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Characters;

[RequireComponent(typeof(PlayerInput), typeof(Player))]
public class CharacterMove : MonoBehaviour
{
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction abilityAction;
    private InputAction interactAction;
    private InputAction hubAction;
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
            hubAction = playerInput.actions.FindAction("Hub");
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

        // Read every frame, even when there's nothing to apply it to, so the record of which buttons are down stays level
        // with the real ones. A button still held when a menu closes, or a character is taken over, isn't a new press.
        var input = ReadInput();

        var joined = GameSession.IsClientOnly;

        if (joined)
        {
            // In someone else's game the host decides everything, so what the controls say goes to it, and nothing is done to a
            // character here. This player's own screen, and asking to switch character, are still theirs to do.
            if (HubMenu.IsOpenFor(player))
            {
                // A hub menu this player has open, which is shown here, takes all of their buttons, and the host is told their
                // character is standing still.
                if (ConnectionPlayer.Local)
                {
                    ConnectionPlayer.Local.SendInput(player, new PlayerInputFrame());
                }

                HubMenu.Feed(player, input.move, input.jumpPressed, input.abilityPressed, input.interactPressed);
                return;
            }

            if (ConnectionPlayer.Local)
            {
                ConnectionPlayer.Local.SendInput(player, input);
            }
        }
        else
        {
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
        }

        CameraManager.Look(player, lookAction.ReadValue<Vector2>());

        // With the group camera, holding the pin button chooses a character to pin with Previous and Next, which then aren't for
        // switching.
        var wasChoosing = pinChoosing;

        if (CameraManager.IsGroupCameraActive)
        {
            UpdatePinChoice();
        }

        if (!pinChoosing && !wasChoosing)
        {
            if (prevPressed)
            {
                SwitchCharacter(false);
            }

            if (nextPressed)
            {
                SwitchCharacter(true);
            }
        }

        if (zoomInAction.IsPressed())
        {
            CameraManager.Zoom(player, 1);
        }

        if (zoomOutAction.IsPressed())
        {
            CameraManager.Zoom(player, -1);
        }

        // The older cameras have a lock of their own.
        if (lockPressed && !CameraManager.IsGroupCameraActive)
        {
            CameraManager.Lock(player);
        }
    }

    // ----- Pinning -----

    // Holding the pin button chooses a character to pin: Previous and Next move the choice through every character in the game,
    // starting with the one this player has. Letting go pins the one chosen, or unpins it if it's already pinned. Pinning the
    // character the player has also moves them on to the next one nobody has, since they've left it for a view of its own. Pinning
    // any other leaves them where they are.
    private bool pinChoosing;
    private Character pinChoice;

    private void UpdatePinChoice()
    {
        if (!pinChoosing)
        {
            if (!lockPressed)
            {
                return;
            }

            pinChoosing = true;
            pinChoice = currentCharacter;

            if (!pinChoice)
            {
                StepPinChoice(true);
            }
        }

        if (!lockHeld)
        {
            FinishPinChoice();
            return;
        }

        if (prevPressed)
        {
            StepPinChoice(false);
        }

        if (nextPressed)
        {
            StepPinChoice(true);
        }

        CameraManager.SetPinChoice(player, pinChoice);
    }

    // Moves the choice to the next or previous character that's in the game, all the way round.
    private void StepPinChoice(bool next)
    {
        var inGame = new List<Character>();

        foreach (var candidate in Character.GetCharacters(false))
        {
            if (candidate && candidate.InGame)
            {
                inGame.Add(candidate);
            }
        }

        if (inGame.Count == 0)
        {
            pinChoice = null;
            return;
        }

        var index = inGame.IndexOf(pinChoice);
        var count = inGame.Count;
        index = index < 0 ? 0 : ((index + (next ? 1 : -1)) % count + count) % count;
        pinChoice = inGame[index];
    }

    private void FinishPinChoice()
    {
        var chosen = pinChoice;

        pinChoosing = false;
        pinChoice = null;
        CameraManager.SetPinChoice(player, null);

        if (!chosen || !chosen.InGame)
        {
            return;
        }

        var text = GameText.Current;

        if (CameraManager.IsPinned(chosen))
        {
            if (CameraManager.Unpin(player, chosen) == UnpinResult.NotYours)
            {
                var owner = CameraManager.PinOwner(chosen);
                GameMessages.Show(GameText.Fill(text.pinnedByOther, player: owner ? owner.playerNum.ToString() : "?"), 2.5f);
            }

            return;
        }

        var own = chosen == currentCharacter;

        // Pinning the character they have leaves it where it stands, so there has to be another for them to take over. (Someone
        // else's game decides that for itself, so it's tried anyway.)
        if (own && !GameSession.IsClientOnly && !CharacterControl.HasAnotherAvailable(player.id))
        {
            GameMessages.Show(text.cantPinNoOther, 2.5f);
            return;
        }

        var result = CameraManager.Pin(player, chosen);

        if (result == PinResult.NoScreenToSpare)
        {
            GameMessages.Show(text.noScreenToPin, 2.5f);
        }
        else if (result == PinResult.Pinned && own)
        {
            SwitchCharacter(true);
        }
    }

    // If this player is taken out of the game while choosing, the arrow goes with them.
    void OnDisable()
    {
        if (pinChoosing)
        {
            pinChoosing = false;
            pinChoice = null;
            CameraManager.SetPinChoice(player, null);
        }
    }

    // Whether the buttons that only concern this player's own machine went down this frame. Set by ReadInput.
    private bool prevPressed;
    private bool nextPressed;
    private bool lockPressed;
    private bool lockHeld;

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

        var hubHeld = hubAction != null && hubAction.IsPressed();

        input.hubPressed = hubHeld && !GetPressed(hubAction);

        var prevHeld = prevAction.IsPressed();
        var nextHeld = nextAction.IsPressed();
        lockHeld = lookLock.IsPressed();

        prevPressed = prevHeld && !GetPressed(prevAction);
        nextPressed = nextHeld && !GetPressed(nextAction);
        lockPressed = lockHeld && !GetPressed(lookLock);

        SetPressed(jumpAction, input.jumpHeld);
        SetPressed(abilityAction, input.abilityHeld);
        SetPressed(interactAction, input.interactHeld);

        if (hubAction != null)
        {
            SetPressed(hubAction, hubHeld);
        }

        SetPressed(prevAction, prevHeld);
        SetPressed(nextAction, nextHeld);
        SetPressed(lookLock, lockHeld);

        return input;
    }

    // Asks for the next or previous character nobody else has: of the host if this player has joined someone else's game, which
    // decides, and of the record here if not.
    private void SwitchCharacter(bool next)
    {
        if (GameSession.IsClientOnly)
        {
            if (ConnectionPlayer.Local)
            {
                ConnectionPlayer.Local.RequestSwitch(player, next);
            }
        }
        else
        {
            CharacterControl.Switch(player.id, next);
        }
    }

    // Does what a frame of input says to a character. This is the part that runs on the host, whichever machine the player is
    // really on.
    public static void ApplyInput(Character character, PlayerInputFrame input)
    {
        // On its way through a teleporter, it can't be steered or made to do anything.
        if (character.InTransit)
        {
            return;
        }

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

        if (input.hubPressed)
        {
            CharacterHub.OpenMenuFor(character);
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

        if (GameSession.IsClientOnly)
        {
            // The character is the host's to stop, so it's told nothing is held.
            if (player && ConnectionPlayer.Local)
            {
                ConnectionPlayer.Local.SendInput(player, new PlayerInputFrame());
            }
        }
        else if (currentCharacter)
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

        if (!player)
        {
            return;
        }

        if (GameSession.IsClientOnly)
        {
            if (ConnectionPlayer.Local)
            {
                ConnectionPlayer.Local.ReleaseSeat(player);
            }
        }
        else
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
