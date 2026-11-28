using System;
using System.Collections.Generic;
using UnityEngine;

// The master list of the words the game shows the players: the hub menu, the messages along the top of the screen, the join and
// leave prompts. Change any of them here, in one place, without touching the scripts that show them.
//
// Make the list with Tools > Game Text > Open Master Text. It's an asset in a Resources folder, called GameText, and the game
// finds it there by itself. Until it's made, the words below are used as they are.
//
// Some texts have a word in curly brackets, such as {character}, which is replaced with the real name when it's shown. Keep
// the bracketed words in the text, wherever they suit it best. Only the ones listed for each text are filled in.
[CreateAssetMenu(fileName = "GameText", menuName = "Game/Master Text")]
public class GameText : ScriptableObject
{
    [Header("Hub menu: main page")]
    [Tooltip("The hub menu button that opens the page for swapping characters.")]
    public string swapCharacters = "Swap Characters";
    [Tooltip("The hub menu button that brings the whole party to the player who pressed it.")]
    public string bringEveryoneHere = "Bring Everyone Here";
    [Tooltip("The hub menu button that closes the menu.")]
    public string closeMenu = "Close";

    [Tooltip("The hub menu button when the tutorial's hints are being shown, and when they are not. Pressing it switches them.")]
    public string hintsOn = "Hints: On";

    [Tooltip("The hub menu button while the tutorial's hints are not being shown (see Hints On, above).")]
    public string hintsOff = "Hints: Off";

    [Tooltip("Along the bottom of the first page.")]
    public string mainHint = "Jump: pick     Interact: close";

    [Header("Hub menu: swap page")]
    [Tooltip("Over the characters in the party being picked.")]
    public string partyHeading = "Party";

    [Tooltip("Over everyone who has been found.")]
    public string foundHeading = "Found";

    [Tooltip("Across a character who is in the party, at the hub, and can be taken out.")]
    public string inParty = "IN PARTY";

    [Tooltip("Across the last character at the hub, who can't be taken out until somebody else is picked.")]
    public string staysHere = "STAYS HERE";

    [Tooltip("Across a party member who is out in the field and can't be swapped.")]
    public string awayFromHub = "AWAY";

    [Tooltip("Along the bottom of the swap page.")]
    public string swapHint = "Jump: add or remove     Action: done     Interact: back     Away from the hub: can't be swapped";

    [Header("Messages")]
    [Tooltip("When the party is brought to the hub.")]
    public string partyBroughtToHub = "The party has been brought to the hub.";

    [Tooltip("When the party has been swapped.")]
    public string partyChanged = "The party has changed.";

    [Tooltip("When a character who is away from the hub is picked. {character} is their name.")]
    public string cantSwapAway = "{character} is away from the hub, so can't be swapped here.";

    [Tooltip("When the last character at the hub is picked to be taken out.")]
    public string mustStayPickAnother = "Someone has to stay at the hub. Pick another character first.";

    [Tooltip("When a player tries to pin their character to a screen but there's no other character for them to switch to.")]
    public string cantPinNoOther = "There's no other character to switch to, so this one can't be pinned.";

    [Tooltip("When a player tries to pin a character but every screen is already used.")]
    public string noScreenToPin = "There's no spare screen to pin it to.";

    [Tooltip("When a player tries to unpin a view that another player pinned. {player} is that player's number.")]
    public string pinnedByOther = "Player {player} pinned that view, so only they can unpin it.";

    [Tooltip("Shown to a player holding the pin button while they choose a character that isn't pinned. {character} is their name.")]
    public string pinChoicePin = "Release to pin {character}";

    [Tooltip("The same, for a character that is already pinned.")]
    public string pinChoiceUnpin = "Release to unpin {character}";

    [Tooltip("When a player tries to put down what they're carrying, but where they are is inside a wall or the ground.")]
    public string noRoomToPutDown = "There's no room to put that down here.";

    [Tooltip("When the swap is finished with nobody at the hub.")]
    public string mustStay = "Someone has to stay at the hub.";

    [Tooltip("The first time a hub is reached. {hub} is its name.")]
    public string hubReachedFirst = "{hub} reached! It's your checkpoint now.";

    [Tooltip("When a hub that was reached before becomes the checkpoint again. {hub} is its name.")]
    public string hubCheckpoint = "{hub} is your checkpoint now.";

    [Tooltip("When a character is found. {character} is their name.")]
    public string characterFound = "Found {character}! They're waiting for you at the hub.";

    [Tooltip("When a character is sent back by a Kill Zone. {character} is their name and {hub} is the hub they come back at.")]
    public string sentBackToHub = "{character} is back at {hub}.";

    [Header("Players")]
    [Tooltip("In the middle of the screen while nobody has joined, and along the bottom while there's room for more.")]
    public string pressAnyButtonToJoin = "Press any button to join...";

    [Tooltip("Along the top while a player holds the leave button. {player} is their name.")]
    public string playerLeaving = "{player} leaving...";

    [Header("Hints")]
    [Tooltip("The hints that a Hint component in the world can show, each with the topic that names it and its words. A Hint is given a topic (any text), and shows the words of the entry with the same topic, ignoring capitals. Add as many as you like here. The controls in curly brackets are replaced with the buttons the player is using: {move}, {jump}, {action}, {interact}, {hub}, {next} and {previous}.")]
    public List<HintText> hints = new List<HintText>
    {
        new HintText("Move", "Move with {move}."),
        new HintText("Jump", "Press {jump} to jump."),
        new HintText("SwitchCharacter", "Press {next} or {previous} to switch to another character."),
        new HintText("Interact", "Press {interact} next to a lever to pull it."),
        new HintText("Carry", "Press {interact} next to a crate to pick it up, and again to put it down."),
        new HintText("Button", "Stand on a button to press it. The door only stays open while it's pressed."),
        new HintText("SwitchOnButton", "Leave one character on the button, press {next} to switch to another, and walk through."),
        new HintText("Teamwork", "Some doors need everyone. Put a character on each button, and send another through."),
        new HintText("Hub", "A hub! Stand here and press {hub} to swap characters.")
    };

    [Header("Meters")]
    [Tooltip("Across the Flashlight's battery bar when it has run flat.")]
    public string batteryFlat = "FLAT";

    // The words of the hint with this topic, if there is one. The topic is matched ignoring capitals and spaces at its ends.
    public bool TryGetHint(string topic, out string text)
    {
        text = null;
        topic = topic == null ? "" : topic.Trim();

        if (topic.Length == 0 || hints == null)
        {
            return false;
        }

        foreach (var entry in hints)
        {
            if (entry != null && string.Equals((entry.topic ?? "").Trim(), topic, StringComparison.OrdinalIgnoreCase))
            {
                text = entry.text;
                return true;
            }
        }

        return false;
    }

    private static GameText current;
    private static bool warnedMissing;

    // The master list, from the asset called GameText in a Resources folder. If there isn't one, the words as written above.
    public static GameText Current
    {
        get
        {
            if (!current)
            {
                current = Resources.Load<GameText>("GameText");

                if (!current)
                {
                    current = CreateInstance<GameText>();
                    current.hideFlags = HideFlags.HideAndDontSave;

                    if (!warnedMissing)
                    {
                        warnedMissing = true;
                        Debug.Log("There is no master text asset, so the game's built-in words are used. Make one with Tools > Game Text > Open Master Text to change them.");
                    }
                }
            }

            return current;
        }
    }

    // A text with its bracketed words filled in. Any that aren't given are left as they are.
    public static string Fill(string text, string character = null, string hub = null, string player = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (character != null)
        {
            text = text.Replace("{character}", character);
        }

        if (hub != null)
        {
            text = text.Replace("{hub}", hub);
        }

        if (player != null)
        {
            text = text.Replace("{player}", player);
        }

        return text;
    }
}

// One hint: the topic that names it, and what it says.
[Serializable]
public class HintText
{
    [Tooltip("What a Hint component's Topic is set to, to show this. Any text, without needing to be unique, though the first with a topic is the one found.")]
    public string topic;

    [TextArea]
    [Tooltip("The words the hint shows. Controls can be put in curly brackets, and are replaced by the player's own buttons: {move}, {jump}, {action}, {interact}, {hub}, {next} and {previous}.")]
    public string text;

    public HintText()
    {
    }

    public HintText(string topic, string text)
    {
        this.topic = topic;
        this.text = text;
    }
}
