using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Characters;

// A person playing, and the controller they're using. It exists only on the machine the person is sitting at. Which
// character they are controlling is not kept here: CharacterControl, on the host, has that, and this asks for it.
public class Player : MonoBehaviour
{
    public CharacterMove characterMove;
    public int playerNum;

    // Never changes while the player is in the game, unlike playerNum, which is renumbered when someone leaves. This is
    // how CharacterControl knows the player, on the host's own machine, and how the cameras tell players apart.
    public int id { get; private set; }

    // The player's seat in a game they have joined, which is what the host knows them by. It's given by the host once it has made room,
    // and is -1 until then, and whenever the player isn't joined to someone else's game.
    public int seatId = -1;

    private static int nextId = 1;
    private static List<Player> players;

    // A number nobody else has, for a seat on the host for a player on another machine. They share the numbers the host's own players
    // are given, so none are the same.
    public static int AllocateSeatId()
    {
        return nextId++;
    }

    public static Player FindById(int playerId)
    {
        return players?.FirstOrDefault(p => p && p.id == playerId);
    }

    // The player on this machine who has this seat in someone else's game.
    public static Player FindBySeat(int seat)
    {
        return seat < 0 ? null : players?.FirstOrDefault(p => p && p.seatId == seat);
    }

    // The character this player is controlling, or null if they're not controlling one. On a machine that has joined someone else's game,
    // it's the character the host says is theirs.
    public Character character
    {
        get
        {
            return GameSession.IsClientOnly
                ? ConnectionPlayer.CharacterOfSeat(seatId)
                : CharacterControl.GetCharacter(id);
        }
    }

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

        if (GameSession.IsClientOnly)
        {
            if (ConnectionPlayer.Local)
            {
                ConnectionPlayer.Local.ReleaseSeat(this);
            }
        }
        else
        {
            CharacterControl.Release(id);
        }
    }

    void Update()
    {
        if (GameSession.IsClientOnly)
        {
            // The host looks after who has which character, so all there is to do is ask for a seat.
            if (ConnectionPlayer.Local)
            {
                ConnectionPlayer.Local.EnsureSeat(this);
            }

            return;
        }

        // Not joined to anyone's game, so whatever seat they had is gone.
        seatId = -1;

        // Takes the first character nobody is controlling: when the player first arrives, and again if the one they were
        // controlling is put away.
        var current = character;
        if (!current || !current.InGame)
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
