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
    public string swapCharacters = "Swap Characters";
    public string bringEveryoneHere = "Bring Everyone Here";
    public string closeMenu = "Close";

    [Tooltip("The hub menu button when the tutorial's hints are being shown, and when they are not. Pressing it switches them.")]
    public string hintsOn = "Hints: On";

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

    [Header("Tutorial hints")]
    [Tooltip("Hints shown while someone is in a Tutorial Prompt's area. The controls in curly brackets are replaced with the buttons the player is using: {move}, {jump}, {action}, {interact}, {next} and {previous}.")]
    public string hintMove = "Move with {move}.";

    public string hintJump = "Press {jump} to jump.";
    public string hintSwitch = "Press {next} or {previous} to switch to another character.";
    public string hintInteract = "Press {interact} next to a lever to pull it.";
    public string hintCarry = "Press {interact} next to a crate to pick it up, and again to put it down.";
    public string hintButton = "Stand on a button to press it. The door only stays open while it's pressed.";
    public string hintSwitchOnButton = "Leave one character on the button, press {next} to switch to another, and walk through.";
    public string hintTeamwork = "Some doors need everyone. Put a character on each button, and send another through.";
    public string hintHub = "A hub! Stand here and press {interact} to swap characters.";

    [Header("Meters")]
    [Tooltip("Across the Flashlight's battery bar when it has run flat.")]
    public string batteryFlat = "FLAT";

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
