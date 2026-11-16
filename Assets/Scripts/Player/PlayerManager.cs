using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Players join by pressing a button on the device they want to use: a key on the keyboard, or any button on a
// gamepad. Whatever they pressed is theirs, and the Player Input picks the control scheme that suits it. There is no
// need to put a PlayerInputManager in the scene; this finds one or adds it.
public class PlayerManager : MonoBehaviour
{
    [Tooltip("The most players that can be playing at once. Once that many have joined, nobody else can until one leaves.")]
    [Range(1, 4)]
    [FormerlySerializedAs("numPlayers")]
    public int maxPlayers = 4;

    [Tooltip("Needs a Player Input component.")]
    public GameObject playerPrefab;

    [Tooltip("Whether pressing a button on a free device adds a player. Turn it off to stop anyone else joining.")]
    public bool allowJoining = true;

    [Tooltip("For testing without picking controllers: this many players are added straight away, each given whichever device is free, the way players used to be. They aren't added again if they leave. 0 for none.")]
    [Range(0, 4)]
    public int testPlayers;

    private static PlayerManager instance;
    public static List<Player> players;

    private PlayerInputManager inputManager;
    private int testPlayersAdded;

    void Start()
    {
        instance = this;
        players = new List<Player>();

        // Tells players when someone's leaving and that they can join.
        if (!GetComponent<PlayerPrompts>())
        {
            gameObject.AddComponent<PlayerPrompts>();
        }

        inputManager = FindAnyObjectByType<PlayerInputManager>();

        if (!inputManager)
        {
            inputManager = gameObject.AddComponent<PlayerInputManager>();
        }

        inputManager.playerPrefab = playerPrefab;
        inputManager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
        inputManager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

        // The game has its own cameras for up to four players.
        inputManager.splitScreen = false;

        inputManager.onPlayerJoined += OnPlayerJoined;
        inputManager.onPlayerLeft += OnPlayerLeft;

        UpdateJoining();
    }

    void OnDestroy()
    {
        if (inputManager)
        {
            inputManager.onPlayerJoined -= OnPlayerJoined;
            inputManager.onPlayerLeft -= OnPlayerLeft;
        }
    }

    void Update()
    {
        // A test player takes whichever device is free, rather than waiting for a button to be pressed. They're only
        // added once, so a test player who leaves stays gone.
        if (testPlayersAdded < testPlayers && players.Count + ConnectionPlayer.RemoteSeatCount() < maxPlayers && inputManager.joiningEnabled)
        {
            if (inputManager.JoinPlayer())
            {
                testPlayersAdded++;
            }
        }

        UpdateJoining();
    }

    // The most players can be joined at once, which the input manager can't be told directly, so joining is just
    // switched off while the game is full.
    private void UpdateJoining()
    {
        var canJoin = allowJoining && players.Count + ConnectionPlayer.RemoteSeatCount() < maxPlayers;

        if (canJoin && !inputManager.joiningEnabled)
        {
            inputManager.EnableJoining();
        }
        else if (!canJoin && inputManager.joiningEnabled)
        {
            inputManager.DisableJoining();
        }
    }

    private void OnPlayerJoined(PlayerInput playerInput)
    {
        var player = playerInput.GetComponent<Player>();

        if (!player)
        {
            Debug.LogError($"{playerInput.name} joined but has no Player component, so it was ignored.", playerInput);
            return;
        }

        // Kept together under this object, as players always have been.
        playerInput.transform.SetParent(transform, true);

        players.Add(player);
        Renumber();

        var devices = string.Join(", ", playerInput.devices.Select(d => d.displayName));
        print($"{player.name} joined, using {devices} ({playerInput.currentControlScheme}).");
    }

    private void OnPlayerLeft(PlayerInput playerInput)
    {
        var player = playerInput.GetComponent<Player>();

        if (player)
        {
            players.Remove(player);
        }

        Renumber();
        print($"{playerInput.name} left.");
    }

    // Players are always numbered 1 up to however many there are, in the order they joined, because the cameras
    // look them up by number.
    private void Renumber()
    {
        players.RemoveAll(p => !p);

        var count = 1;
        foreach (var p in players)
        {
            p.playerNum = count++;
            p.name = $"Player {p.playerNum}";
        }
    }

    // Removes a player from the game; their device is free again for anyone to join with.
    public static void RemovePlayer(Player player)
    {
        if (player)
        {
            var characterMove = player.GetComponent<CharacterMove>();
            if (characterMove)
            {
                characterMove.ReleaseControl();
            }

            Destroy(player.gameObject);
        }
    }

    // How many cameras the game needs: never fewer than one, so there's a view while waiting for the first player.
    public static int GetPlayerCount()
    {
        if (instance)
        {
            return Mathf.Max(players.Count, 1);
        }
        else
        {
            return -1;
        }
    }

    // Whether pressing a button on a free device would add a player right now.
    public static bool CanJoin()
    {
        return instance && instance.inputManager && instance.inputManager.joiningEnabled;
    }

    // How many players have actually joined.
    public static int GetJoinedPlayerCount()
    {
        return instance ? players.Count : 0;
    }

    // The most players there can be at once, on every machine in the game together.
    public static int MaxPlayers
    {
        get { return instance ? instance.maxPlayers : 4; }
    }
}
