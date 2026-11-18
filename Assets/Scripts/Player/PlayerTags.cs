using System.Collections.Generic;
using Characters;
using Mirror;
using UnityEngine;

// Works out whether there is more than one player in the game, and if so puts a PlayerTag (P1, P2...) over every character a player is
// controlling, and takes it away when they move on. With only one player there are no tags, and the Player shows a SwitchHint
// pointer when they switch character instead.
//
// There is one of these in the game, made when it starts. The numbers are the host's: its own players are numbered as they are on
// its screen (Player.playerNum), and the players on other machines follow them, in the order they joined. The host tells the other
// machines (through ConnectionPlayer), so every machine in a game shows the same number over the same character.
public class PlayerTags : MonoBehaviour
{
    // Whether there's more than one player in the game, on all machines together. Looked at again every frame.
    public static bool IsMultiplayer { get; private set; }

    private readonly Dictionary<Character, int> wanted = new Dictionary<Character, int>();
    private readonly Dictionary<Character, PlayerTag> tags = new Dictionary<Character, PlayerTag>();
    private readonly List<KeyValuePair<Character, int>> numbers = new List<KeyValuePair<Character, int>>();
    private readonly List<Character> finished = new List<Character>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        IsMultiplayer = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<PlayerTags>())
        {
            return;
        }

        var made = new GameObject("Player Tags");
        DontDestroyOnLoad(made);
        made.AddComponent<PlayerTags>();
    }

    // On the host, or in a game with nobody else: how many players there are, and which number is controlling which character.
    // Characters are added to the list given.
    public static int Compute(List<KeyValuePair<Character, int>> into)
    {
        var local = PlayerManager.GetJoinedPlayerCount();
        var remoteSeats = ConnectionPlayer.RemoteSeatIds();

        into.Clear();

        foreach (var pair in CharacterControl.Controlled)
        {
            if (!pair.Key || !pair.Key.InGame)
            {
                continue;
            }

            var player = Player.FindById(pair.Value);
            var number = player ? player.playerNum : local + 1 + remoteSeats.IndexOf(pair.Value);

            if (number >= 1)
            {
                into.Add(new KeyValuePair<Character, int>(pair.Key, number));
            }
        }

        return local + remoteSeats.Count;
    }

    void Update()
    {
        wanted.Clear();

        int total;

        if (GameSession.IsClientOnly)
        {
            // The host works the numbers out, and sends them.
            var link = ConnectionPlayer.Local;
            total = link ? link.PlayerTotal : 0;

            if (link)
            {
                foreach (var pair in link.controllerNumbers)
                {
                    if (NetworkClient.spawned.TryGetValue(pair.Key, out var identity) && identity)
                    {
                        var character = identity.GetComponent<Character>();

                        if (character && character.InGame)
                        {
                            wanted[character] = pair.Value;
                        }
                    }
                }
            }
        }
        else
        {
            total = Compute(numbers);

            foreach (var pair in numbers)
            {
                wanted[pair.Key] = pair.Value;
            }
        }

        IsMultiplayer = total > 1;

        if (!IsMultiplayer)
        {
            wanted.Clear();
        }

        foreach (var pair in wanted)
        {
            if (tags.TryGetValue(pair.Key, out var tag) && tag)
            {
                tag.SetNumber(pair.Value);
            }
            else
            {
                tags[pair.Key] = PlayerTag.Create(pair.Key, pair.Value);
            }
        }

        // The characters nobody controls now, or that have gone.
        finished.Clear();

        foreach (var pair in tags)
        {
            if (!pair.Key || !pair.Value || !wanted.ContainsKey(pair.Key))
            {
                finished.Add(pair.Key);
            }
        }

        foreach (var character in finished)
        {
            if (tags.TryGetValue(character, out var tag) && tag)
            {
                Destroy(tag.gameObject);
            }

            tags.Remove(character);
        }
    }
}
