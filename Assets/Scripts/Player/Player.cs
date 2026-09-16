using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Characters;

public class Player : MonoBehaviour
{
    public Character character;
    public CharacterMove characterMove;
    public int playerNum;

    private static Dictionary<Player, Character> playerCharacters;
    private static Dictionary<Character, Player> characterPlayers;
    private static List<Player> players;

    void Start()
    {
        characterMove = GetComponent<CharacterMove>();
        if (playerCharacters == null)
        {
            playerCharacters = new Dictionary<Player, Character>();
            characterPlayers = new Dictionary<Character, Player>();
            players = new List<Player>();
        }

        players.Add(this);
    }
    
    void OnDestroy()
    {
        players.Remove(this);

        Character character = null;
        if (playerCharacters.ContainsKey(this))
        {
            character = playerCharacters[this];
            playerCharacters.Remove(this);
        }

        if (character)
        {
            if (characterPlayers.ContainsKey(character))
            {
                characterPlayers.Remove(character);
            }
        }
        
        CleanupCharacterPlayerDictionaries();
    }

    void Update()
    {
        Character characterToAssign = null;
        if (!playerCharacters.ContainsKey(this))
        {
            //This player doesn't have a character yet.
            var allCharacters = Character.GetCharacters(false);
            foreach (var c in allCharacters)
            {
                if (characterPlayers.ContainsKey(c))
                {
                    //Someone else already has this character
                    continue;
                }
                
                characterToAssign = c;

                break;
            }
        }

        if (!character || !character.gameObject.activeSelf)
        {
            characterToAssign = GetAvailableCharacter(this, true);
        }

        if (characterToAssign)
        {
            character = characterToAssign;
            playerCharacters[this] = character;
            characterPlayers[characterToAssign] = this;

            characterMove.AssignCharacter(characterToAssign);
        }
    }

    public void AssignCharacter(Character newCharacter)
    {
        if (character == newCharacter)
        {
            //Nothing has changed
            return;
        }

        playerCharacters[this] = newCharacter;
        characterPlayers.Remove(character);
        
        character = newCharacter;
    }

    public static Character GetAvailableCharacter(Player player, bool next)
    {
        Character available = null;
        if (player)
        {
            int index;
            var currentCharacter = player.character;
            var allCharacters = Character.GetCharacters(true);
            
            if (!currentCharacter || !currentCharacter.gameObject.activeSelf)
            {
                CleanupCharacterPlayerDictionaries();
                currentCharacter = null;
                index = 0;
            }
            else
            {
                index = allCharacters.IndexOf(currentCharacter);    
            }

            while (!available)
            {
                if (next)
                {
                    index++;
                    if (index >= allCharacters.Count)
                    {
                        index = 0;
                    }
                }
                else
                {
                    index--;
                    if (index < 0)
                    {
                        index = allCharacters.Count - 1;
                    }
                }

                var tryChar = allCharacters[index];

                if (!characterPlayers.ContainsKey(tryChar))
                {
                    available = tryChar;
                }

                if (tryChar == currentCharacter)
                {
                    //It's looped all the way around and there are no available characters
                    return currentCharacter;
                }
            }
        }
        else
        {
            print ("ERROR: No available player found in GetAvailableCharacter()");
        }

        return available;
    }

    private static void CleanupCharacterPlayerDictionaries()
    {
        var newCPDict = new Dictionary<Character, Player>();
        var newPCDict  = new Dictionary<Player, Character>();
        foreach (var cp in characterPlayers)
        {
            var c = cp.Key;
            var p = cp.Value;
            
            if (
                !c ||
                !c.gameObject ||
                !c.gameObject.activeSelf ||
                
                !p ||
                !p.gameObject ||
                !p.gameObject.activeSelf
            )
            {
                continue;
            }

            //Both the player and character are still active
            newCPDict[cp.Key] = cp.Value;
        }
        
        foreach (var pc in playerCharacters)
        {
            var p = pc.Key;
            var c = pc.Value;
            
            if (
                !c ||
                !c.gameObject ||
                !c.gameObject.activeSelf ||
                
                !p ||
                !p.gameObject ||
                !p.gameObject.activeSelf
            )
            {
                continue;
            }
            
            //Both the player and character are still active
            newPCDict[pc.Key] = pc.Value;
        }
        
        characterPlayers = newCPDict;
        playerCharacters = newPCDict;
    }

    public static int GetCount()
    {
        return players.Count;
    }

    public static Player GetPlayer(int playerNum)
    {
        return players?.FirstOrDefault(x => x.playerNum == playerNum);
    }

    public Character GetCharacter()
    {
        return character;
    }
}
