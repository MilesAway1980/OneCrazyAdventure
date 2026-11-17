using System.Collections.Generic;
using Characters;
using UnityEngine;

// The record of who is controlling which character right now. Nobody is tied to a character: a player simply controls one
// until they switch to another, and then someone else is free to take the one they left.
//
// This is the one place that fact is kept. Everything else (the cameras, the hubs, the prompts) asks it, and nothing
// keeps its own copy. That matters for playing over a network, because there will be only one record, on the host, and
// the machines the other players are on will be told what it says. A player is known here by a number (Player.id) rather
// than the Player object, since that object only exists on the machine its player is sitting at.
//
// Everything that changes the record is in this file, and is what will become a request to the host.
public static class CharacterControl
{
    private static readonly Dictionary<int, Character> characterOfPlayer = new Dictionary<int, Character>();
    private static readonly Dictionary<Character, int> playerOfCharacter = new Dictionary<Character, int>();
    private static readonly List<int> stalePlayers = new List<int>();

    // Starts empty every time the game starts, in case Unity is set to skip reloading scripts when entering Play mode.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        characterOfPlayer.Clear();
        playerOfCharacter.Clear();
    }

    // The character this player is controlling, or null if they aren't controlling one.
    public static Character GetCharacter(int playerId)
    {
        return characterOfPlayer.TryGetValue(playerId, out var character) && character
            ? character
            : null;
    }

    // The player (seat) controlling this character, if anyone is.
    public static bool TryGetPlayerId(Character character, out int playerId)
    {
        playerId = 0;
        return character && playerOfCharacter.TryGetValue(character, out playerId);
    }

    // Whether any player is controlling this character.
    public static bool IsControlled(Character character)
    {
        return character && playerOfCharacter.ContainsKey(character);
    }

    // Gives the player the first character nobody is controlling, if they haven't got one. Returns the character they are
    // controlling afterwards, which is null if there was none free.
    public static Character Claim(int playerId)
    {
        Clean();

        var current = GetCharacter(playerId);
        if (current)
        {
            return current;
        }

        foreach (var candidate in Character.GetCharacters(false))
        {
            if (IsAvailable(candidate))
            {
                Assign(playerId, candidate);
                return candidate;
            }
        }

        return null;
    }

    // Whether there's a character, other than the one they have, that this player could switch to.
    public static bool HasAnotherAvailable(int playerId)
    {
        Clean();

        var current = GetCharacter(playerId);

        foreach (var candidate in Character.GetCharacters(false))
        {
            if (candidate != current && IsAvailable(candidate))
            {
                return true;
            }
        }

        return false;
    }

    // Moves the player on to the next (or previous) character nobody else is controlling. If there isn't one, they keep the
    // character they have. Returns the character they are controlling afterwards.
    public static Character Switch(int playerId, bool next)
    {
        Clean();

        var current = GetCharacter(playerId);
        var all = Character.GetCharacters(false);
        var count = all.Count;

        if (count == 0)
        {
            return current;
        }

        var step = next ? 1 : -1;
        var index = current
            ? all.IndexOf(current)
            : (next ? -1 : 0);

        for (var tried = 0; tried < count; tried++)
        {
            index = ((index + step) % count + count) % count;
            var candidate = all[index];

            if (candidate == current)
            {
                // All the way round to themselves, so nobody else is free.
                break;
            }

            if (IsAvailable(candidate))
            {
                Assign(playerId, candidate);
                return candidate;
            }
        }

        return current;
    }

    // The player stops controlling anyone, because they've left the game.
    public static void Release(int playerId)
    {
        if (!characterOfPlayer.TryGetValue(playerId, out var character))
        {
            return;
        }

        characterOfPlayer.Remove(playerId);
        playerOfCharacter.Remove(character);

        if (character)
        {
            character.ControlLost();
        }
    }

    private static bool IsAvailable(Character character)
    {
        return character && character.InGame && !playerOfCharacter.ContainsKey(character);
    }

    // Makes the player the controller of this character, telling the one they're leaving and the one they're taking over.
    private static void Assign(int playerId, Character character)
    {
        characterOfPlayer.TryGetValue(playerId, out var old);

        if (old == character)
        {
            return;
        }

        if (old)
        {
            playerOfCharacter.Remove(old);
        }

        characterOfPlayer[playerId] = character;
        playerOfCharacter[character] = playerId;

        if (old)
        {
            old.ControlLost();
        }

        character.ControlGained();
    }

    // Drops anyone controlling a character that has gone: put away, such as swapped out at a hub, or destroyed. They'll be
    // given another the next time they ask.
    private static void Clean()
    {
        stalePlayers.Clear();

        foreach (var pair in characterOfPlayer)
        {
            var character = pair.Value;

            if (!character || !character.InGame)
            {
                stalePlayers.Add(pair.Key);
            }
        }

        foreach (var playerId in stalePlayers)
        {
            Release(playerId);
        }
    }
}
