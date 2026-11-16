using System.Collections.Generic;
using Characters;
using UnityEngine;
using UnityEngine.InputSystem;

// The names of the buttons a player is using, as the Input System shows them for the controller they are playing with: "Space"
// on a keyboard, "Button South" on a gamepad. Tutorial texts use them, so a hint always names the player's own buttons.
public static class ControlNames
{
    // Each word that can be put in curly brackets, and the Input System action it stands for.
    private static readonly (string word, string action)[] Controls =
    {
        ("{move}", "Move"),
        ("{jump}", "Jump"),
        ("{action}", "Ability"),
        ("{interact}", "Interact"),
        ("{next}", "NextCharacter"),
        ("{previous}", "PreviousCharacter")
    };

    // The text with the control words in it replaced by the buttons the player uses. A word whose button can't be found is left
    // as it is.
    public static string Fill(string text, Player player)
    {
        if (string.IsNullOrEmpty(text) || !player)
        {
            return text;
        }

        var input = player.GetComponent<PlayerInput>();

        if (!input || input.actions == null)
        {
            return text;
        }

        foreach (var (word, actionName) in Controls)
        {
            if (!text.Contains(word))
            {
                continue;
            }

            var action = input.actions.FindAction(actionName);

            if (action != null)
            {
                text = text.Replace(word, action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions, input.currentControlScheme));
            }
        }

        return text;
    }
}

// Whether the tutorial's hints are shown. They're off to begin with, so players who like working things out aren't told, and
// a player who is stuck can turn them on from the hub menu. The choice is remembered between sessions.
public static class TutorialHints
{
    private const string Key = "TutorialHintsOn";

    public static event System.Action Changed;

    public static bool Enabled
    {
        get { return PlayerPrefs.GetInt(Key, 0) == 1; }

        set
        {
            if (Enabled == value)
            {
                return;
            }

            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}

// A hint for the players that shows while anyone in the party is in its area, and goes when they all leave. For teaching the
// controls and the puzzles in the Tutorial Yard, as each one comes up.
//
// Give it a trigger collider on a layer that collides with the characters. The words come from the master text (Tools > Game
// Text > Open Master Text), unless it is set to use its own.
public class TutorialPrompt : MonoBehaviour
{
    public enum Hint
    {
        Move,
        Jump,
        SwitchCharacter,
        Interact,
        Carry,
        Button,
        SwitchOnButton,
        Teamwork,
        Hub,
        Custom
    }

    [Tooltip("Which hint it shows. The words of each are in the master text. Custom shows the text below.")]
    public Hint hint = Hint.Move;

    [TextArea]
    [Tooltip("Only for Custom. Controls can be put in curly brackets: {move}, {jump}, {action}, {interact}, {next} and {previous}.")]
    public string customText;

    [Tooltip("Show it only the first time someone goes in, and not again after they have left.")]
    public bool onlyOnce;

    private readonly HashSet<Character> inside = new HashSet<Character>();
    private bool shown;
    private bool used;

    void Reset()
    {
        // Something to walk into, if there isn't anything yet.
        if (!GetComponent<Collider2D>())
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(4f, 3f);
        }
    }

    void Start()
    {
        var area = GetComponent<Collider2D>();

        if (area)
        {
            area.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"{name}: the Tutorial Prompt has no collider, so it can never be seen.", this);
        }
    }

    void OnEnable()
    {
        TutorialHints.Changed += HintsChanged;
    }

    void OnDisable()
    {
        TutorialHints.Changed -= HintsChanged;

        inside.Clear();
        GameMessages.ClearHint(this);
        shown = false;
    }

    // The hints were turned on or off while someone is in the area.
    private void HintsChanged()
    {
        if (!TutorialHints.Enabled)
        {
            shown = false;
            GameMessages.ClearHint(this);
        }
        else if (inside.Count > 0 && !shown && !(onlyOnce && used))
        {
            foreach (var character in inside)
            {
                Show(character);
                break;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var character = other.GetComponentInParent<Character>();

        if (character && IsInParty(character) && inside.Add(character) && !shown && TutorialHints.Enabled && !(onlyOnce && used))
        {
            Show(character);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var character = other.GetComponentInParent<Character>();

        if (character)
        {
            inside.Remove(character);
            HideIfEmpty();
        }
    }

    void Update()
    {
        // On a machine that has joined someone else's game nothing touches anything, so who is in the area comes from where the
        // characters are.
        if (GameSession.IsClientOnly)
        {
            UpdateInsideByPlace();
        }

        // Someone swapped out, or taken out of the game, in the area never leaves it.
        if (inside.Count > 0 && inside.RemoveWhere(c => !c || !c.InGame) > 0)
        {
            HideIfEmpty();
        }
    }

    private void UpdateInsideByPlace()
    {
        var area = GetComponent<Collider2D>();

        if (!area)
        {
            return;
        }

        var bounds = area.bounds;

        foreach (var character in Character.GetCharacters(true))
        {
            var place = character.transform.position;
            var here = IsInParty(character) && bounds.Contains(new Vector3(place.x, place.y, bounds.center.z));

            if (here)
            {
                if (inside.Add(character) && !shown && TutorialHints.Enabled && !(onlyOnce && used))
                {
                    Show(character);
                }
            }
            else if (inside.Remove(character))
            {
                HideIfEmpty();
            }
        }
    }

    private static bool IsInParty(Character character)
    {
        return CharacterSelecter.Instance && CharacterSelecter.Instance.IsInParty(character);
    }

    private void HideIfEmpty()
    {
        if (inside.Count == 0 && shown)
        {
            shown = false;
            GameMessages.ClearHint(this);
        }
    }

    // Shows the hint in the words of the player who is playing the character that came in, with their own buttons.
    private void Show(Character character)
    {
        var text = Words();

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Player player = null;

        if (PlayerManager.players != null)
        {
            foreach (var candidate in PlayerManager.players)
            {
                if (candidate && candidate.character == character)
                {
                    player = candidate;
                    break;
                }
            }

            if (!player && PlayerManager.players.Count > 0)
            {
                player = PlayerManager.players[0];
            }
        }

        shown = true;
        used = true;

        GameMessages.SetHint(this, ControlNames.Fill(text, player));
    }

    private string Words()
    {
        var words = GameText.Current;

        switch (hint)
        {
            case Hint.Move: return words.hintMove;
            case Hint.Jump: return words.hintJump;
            case Hint.SwitchCharacter: return words.hintSwitch;
            case Hint.Interact: return words.hintInteract;
            case Hint.Carry: return words.hintCarry;
            case Hint.Button: return words.hintButton;
            case Hint.SwitchOnButton: return words.hintSwitchOnButton;
            case Hint.Teamwork: return words.hintTeamwork;
            case Hint.Hub: return words.hintHub;
            default: return customText;
        }
    }
}
