using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [Range(1, 4)]
    public int numPlayers;
    public GameObject playerPrefab;
    
    private static PlayerManager instance;
    public static List<Player> players;
    
    void Start()
    {
        instance = this;
        players = new List<Player>();
    }

    // Update is called once per frame
    void Update()
    {
        bool refresh = false;
        if (players.Count < numPlayers)
        {
            var newPlayer = Instantiate(playerPrefab, transform, true);
            var player = newPlayer.GetComponent<Player>();
            players.Add(player);
            refresh = true;
        }
        else if (players.Count > numPlayers)
        {
            var last =  players.Last();
            players.Remove(last);
            Destroy(last.gameObject);

            refresh = true;
        }

        if (refresh)
        {
            var count = 1;
            foreach (var p in players)
            {
                p.playerNum = count++;
                p.name = $"Player {p.playerNum}";
            }
        }
    }

    public static int GetPlayerCount()
    {
        if (instance)
        {
            return instance.numPlayers;
        }
        else
        {
            return -1;
        }
    }
}
