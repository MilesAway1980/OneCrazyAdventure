using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Characters;

// A person playing, and the controller they're using. It exists only on the machine the person is sitting at. Which
// character they are controlling is not kept here: CharacterControl, on the host, has that, and this asks for it.
public class Player : MonoBehaviour
{
    [Tooltip("The script that moves this player's character.")]
    public CharacterMove characterMove;
    [Tooltip("Which player this is, counting from 1.")]
    public int playerNum;

    // Never changes while the player is in the game, unlike playerNum, which is renumbered when someone leaves. This is
    // how CharacterControl knows the player, on the host's own machine, and how the cameras tell players apart.
    public int id { get; private set; }

    // The player's seat in a game they have joined, which is what the host knows them by. It's given by the host once it has made room,
    // and is -1 until then, and whenever the player isn't joined to someone else's game.
    [Tooltip("The player's seat in a game they have joined to, given by the host. -1 until then. Not for setting.")]
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

    // The character this player was controlling when last looked, to tell when it changes, and the hint that is showing for it.
    private Character lastCharacter;
    private SwitchHint hint;

    // When the character this player controls changes (switched to, or given one when they arrive or when theirs is put away), shows
    // a hint of which it is, in a game with one player. This is looked at here, on the machine the player is on, so it works the same for a player on the host
    // and one who has joined someone else's game.
    private void WatchCharacter()
    {
        var now = character;

        if (now == lastCharacter)
        {
            return;
        }

        lastCharacter = now;

        if (hint)
        {
            hint.Cancel();
        }

        // With one player in the game, a pointer shows which character they have. With several, each character has the number of the
        // player controlling it over it all the time (PlayerTags), so this isn't needed.
        hint = now && now.InGame && !PlayerTags.IsMultiplayer ? SwitchHint.Show(now) : null;
    }

    void Update()
    {
        WatchCharacter();

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
