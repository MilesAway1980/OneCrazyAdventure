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
    
    void Start()
    {
        selections = new List<Character>();
        characters = FindObjectsByType<Character>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID).ToList();
        foreach (var character in characters)
        {
            var selection = gameObject.AddComponent<CharacterSelection>();
            selection.character = character;
            bool add = false;

            switch (character.name.ToLower())
            {
                case "balloon":
                    add = characterScriptables.balloon;
                    break;
                case "brick":
                    add = characterScriptables.brick;
                    break;
                case "bouncy ball":
                    add = characterScriptables.bouncyBall;
                    break;
                case "bowling ball":
                    add = characterScriptables.bowlingBall;
                    break;
                case "bulb":
                    add = characterScriptables.bulb;
                    break;
                case "dynamite":
                    add = characterScriptables.dynamite;
                    break;
                case "ladder":
                    add = characterScriptables.ladder;
                    break;
                case "magnet":
                    add = characterScriptables.magnet;
                    break;
                case "pebble":
                    add = characterScriptables.pebble;
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
                case "pencil":
                    add = characterScriptables.pencil;
                    break;
                case "brad":
                    add = characterScriptables.brad;
                    break;
                case "paperclip":
                    add = characterScriptables.paperclip;
                    break;
                case "die":
                    add = characterScriptables.die;
                    break;
            }

            selection.selected = add;
            if (add)
            {
                selections.Add(character);
            }
        }
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
