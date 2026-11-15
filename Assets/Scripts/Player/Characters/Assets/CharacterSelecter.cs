using UnityEngine;
using System.Collections.Generic;
using Characters;
using System.Linq;

public class CharacterSelecter: MonoBehaviour
{
    private List<Character> characters;
    private List<Character> selections;

    [SerializeField]
    private CharacterScriptables characterScriptables;

    // The characters that have been found, whether or not they are in the party. The ones ticked in the CharacterScript
    // asset are found from the start. The party is the ones that are in the game now, up to the most at once.
    private readonly HashSet<Character> owned = new HashSet<Character>();
    private readonly Dictionary<Character, CharacterSelection> selectionOf = new Dictionary<Character, CharacterSelection>();

    public static CharacterSelecter Instance { get; private set; }

    // Raised when a character is found.
    public event System.Action<Character> Unlocked;

    public IReadOnlyCollection<Character> Owned
    {
        get { return owned; }
    }

    public IReadOnlyList<Character> Party
    {
        get { return selections; }
    }

    public int MaxParty
    {
        get { return characterScriptables.GetMaxCharactersAtOnce(); }
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        selections = new List<Character>();
        characters = FindObjectsByType<Character>(FindObjectsInactive.Include).OrderBy(c => c.GetEntityId()).ToList();
        foreach (var character in characters)
        {
            var selection = gameObject.AddComponent<CharacterSelection>();
            selection.character = character;
            selectionOf[character] = selection;
            bool add = false;

            switch (character.name.ToLower())
            {
                case "balloon":
                    add = characterScriptables.balloon;
                    break;
                case "bouncy ball":
                    add = characterScriptables.bouncyBall;
                    break;
                case "bowling ball":
                    add = characterScriptables.bowlingBall;
                    break;
                case "brad":
                    add = characterScriptables.brad;
                    break;
                case "brick":
                    add = characterScriptables.brick;
                    break;
                case "bulb":
                    add = characterScriptables.bulb;
                    break;
                case "die":
                    add = characterScriptables.die;
                    break;
                case "drill":
                    add = characterScriptables.drill;
                    break;
                case "dynamite":
                    add = characterScriptables.dynamite;
                    break;
                case "flashlight":
                    add = characterScriptables.flashlight;
                    break;
                case "ladder":
                    add = characterScriptables.ladder;
                    break;
                case "magnet":
                    add = characterScriptables.magnet;
                    break;
                case "paperclip":
                    add = characterScriptables.paperclip;
                    break;
                case "pebble":
                    add = characterScriptables.pebble;
                    break;
                case "pencil":
                    add = characterScriptables.pencil;
                    break;
                case "piton":
                    add = characterScriptables.piton;
                    break;
                case "suction cup":
                    add = characterScriptables.suctionCup;
                    break;
                case "torch":
                    add = characterScriptables.torch;
                    break;
                case "umbrella":
                    add = characterScriptables.umbrella;
                    break;
            }

            selection.selected = add;
            if (add)
            {
                owned.Add(character);
                selections.Add(character);
            }
        }

        // More are found from the start than the party holds: the rest wait with the others.
        while (selections.Count > MaxParty)
        {
            var extra = selections[selections.Count - 1];

            selections.RemoveAt(selections.Count - 1);
            selectionOf[extra].selected = false;
        }
    }

    public bool IsOwned(Character character)
    {
        return character && owned.Contains(character);
    }

    public bool IsInParty(Character character)
    {
        return character && selections != null && selections.Contains(character);
    }

    // Finds a character, so they can be chosen for the party. They aren't in the party until they're chosen. False if they
    // had been found already.
    public bool Unlock(Character character)
    {
        if (!character || !owned.Add(character))
        {
            return false;
        }

        Unlocked?.Invoke(character);
        return true;
    }

    // Makes these characters the party: the ones that are in it are put in the game, and the rest are taken out of it. It
    // has to be at least one, and no more than the most at once, of characters that have been found, each only once.
    public bool SetParty(IList<Character> newParty)
    {
        if (newParty == null || newParty.Count < 1 || newParty.Count > MaxParty || newParty.Distinct().Count() != newParty.Count)
        {
            return false;
        }

        foreach (var character in newParty)
        {
            if (!IsOwned(character) || !selectionOf.ContainsKey(character))
            {
                return false;
            }
        }

        foreach (var character in selections.ToList())
        {
            if (!newParty.Contains(character))
            {
                selectionOf[character].SetSelected(false);
            }
        }

        selections.Clear();

        foreach (var character in newParty)
        {
            selections.Add(character);
            selectionOf[character].SetSelected(true);
        }

        return true;
    }

    void Update()
    {
        
    }

    public void UpdateMaxCharacters(int newMax)
    {
        if (newMax > 1)
        {
            characterScriptables.SetMaxCharactersAtOnce(newMax);
        }
    }

    public bool AddCharacter(Character character)
    {
        if (selections.Count < characterScriptables.GetMaxCharactersAtOnce())
        {
            if (selections.Contains(character))
            {
                print("Error, trying to add a selected character twice.");
            }
            else
            {
                selections.Add(character);
                return true;
            }
        }

        return false;
    }

    public bool RemoveCharacter(Character character)
    {
        print(selections.Count);
        if (selections.Count > 1)
        {
            if (selections.Contains(character))
            {
                selections.Remove(character);
                return true;
            }
            else
            {
                print("Error, trying to remove a selected character but it isn't in the list.");
            }
        }

        return false;
    }
}
