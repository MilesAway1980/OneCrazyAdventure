using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Characters;

// A person playing, and the controller they're using. It exists only on the machine the person is sitting at. Which
// character they are controlling is not kept here: CharacterControl has that, and this asks it.
public class Player : MonoBehaviour
{
    public CharacterMove characterMove;
    public int playerNum;

    // Never changes while the player is in the game, unlike playerNum, which is renumbered when someone leaves. This is
    // how CharacterControl knows the player.
    public int id { get; private set; }

    private static int nextId = 1;
    private static List<Player> players;

    // The character this player is controlling, or null if they're not controlling one.
    public Character character => CharacterControl.GetCharacter(id);

    void Awake()
    {
        id = nextId++;
    }

    void Start()
    {
        characterMove = GetComponent<CharacterMove>();
        if (players == null)
        {
            players = new List<Player>();
        }

        players.Add(this);
    }

    void OnDestroy()
    {
        players?.Remove(this);
        CharacterControl.Release(id);
    }

    void Update()
    {
        // Takes the first character nobody is controlling: when the player first arrives, and again if the one they were
        // controlling is put away.
        var current = character;
        if (!current || !current.gameObject.activeSelf)
        {
            CharacterControl.Claim(id);
        }
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
