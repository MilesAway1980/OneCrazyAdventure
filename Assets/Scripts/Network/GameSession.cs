using System.Collections;
using Mirror;
using UnityEngine;

// How this copy of the game is connected, and the one place that starts and stops Mirror.
//
// Every game, solo or not, runs as a host. Playing alone or on one machine is a host that isn't listening, so no network port
// is opened and nobody can join, and it plays exactly as a hosted game does. That means there is only one way the game
// runs, and the host is always the one that decides what happens. Hosting a game for others is the same host listening for
// connections. Joining one is a client that does what the host tells it.
//
// A session starts by itself when a scene with a NetworkManager in it loads, so no scene needs anything added.
public class GameSession : MonoBehaviour
{
    public enum SessionMode
    {
        None,
        // One machine, any number of players on it. The host isn't listening.
        Local,
        // The host for a game other machines can join.
        Hosting,
        // Joined to someone else's game.
        Joined
    }

    [Tooltip("What to join when 'Join' is pressed.")]
    public string joinAddress = "localhost";

    [Tooltip("Shows a small panel for hosting or joining a game. For testing, until the game has menus for it.")]
    public bool showPanel = true;

    public static GameSession Instance { get; private set; }

    public SessionMode Mode { get; private set; }

    // This machine runs the game: playing alone, with others on the same machine, or hosting for others. Everything the game
    // decides is done here.
    public static bool IsHost => NetworkServer.active;

    // This machine is joined to someone else's game, which decides everything.
    public static bool IsClientOnly => NetworkClient.active && !NetworkServer.active;

    // Other machines are, or could be, part of the game.
    public static bool IsOnline => (NetworkServer.active && NetworkServer.listen) || IsClientOnly;

    private NetworkManager manager;
    private bool switching;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<GameSession>())
        {
            return;
        }

        new GameObject("Game Session").AddComponent<GameSession>();
    }

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Only for testing: a finished game has its own menus for it.
        showPanel = Application.isEditor || Debug.isDebugBuild;
    }

    void Start()
    {
        if (Instance != this)
        {
            return;
        }

        manager = NetworkManager.singleton ? NetworkManager.singleton : FindFirstObjectByType<NetworkManager>();

        if (!manager)
        {
            Debug.LogWarning("There is no NetworkManager in this scene, so the game can't be started as a host. Add one to the scene.");
            enabled = false;
            return;
        }

        StartLocal();
    }

    [Tooltip("Whether a machine that has joined someone else's game runs physics. On, which is how it's best left: the broken pieces of tiles then land and bounce as they do on the host. Everything that is moved by the host is only moved there (kinematic) and nothing decides anything, so there is nothing for it to duplicate. Off, nothing is simulated there at all, and pieces of tiles fall through the ground.")]
    public bool simulatePhysicsWhenJoined = true;

    // How physics ran before it was turned off, to put it back.
    private SimulationMode2D? normalPhysicsMode;

    private void SetPhysics(bool simulate)
    {
        if (simulate)
        {
            if (normalPhysicsMode.HasValue)
            {
                Physics2D.simulationMode = normalPhysicsMode.Value;
                normalPhysicsMode = null;
            }
        }
        else if (!normalPhysicsMode.HasValue)
        {
            normalPhysicsMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
        }
    }

    // The setting belongs to the project, so it must not be left changed after the game stops.
    void OnDestroy()
    {
        if (Instance == this)
        {
            SetPhysics(true);
        }
    }

    private string lastLoggedState = "";

    void Update()
    {
        // Temporary: says in the Console each time the state of a join changes, to find out where one gets stuck.
        if (Mode == SessionMode.Joined)
        {
            var state = $"{(NetworkClient.isConnected ? "connected" : NetworkClient.isConnecting ? "connecting" : "disconnected")}, ready {NetworkClient.ready}, {NetworkClient.spawned.Count} objects";

            if (state != lastLoggedState)
            {
                lastLoggedState = state;
                Debug.Log($"Game session: join state is now {state}.");
            }
        }

        // The tickbox can be changed while playing, to compare.
        if (Mode == SessionMode.Joined && !switching)
        {
            SetPhysics(simulatePhysicsWhenJoined);
        }

        // A game that was joined has gone: it couldn't be reached, or the host left. Back to playing locally.
        if (Mode == SessionMode.Joined && !switching && !NetworkClient.active)
        {
            Debug.LogWarning("The connection to the game has been lost. Back to playing locally.");
            StartCoroutine(Restart(SessionMode.Local));
        }
    }

    // Settings the game needs whatever the mode, done before every start because the NetworkManager is set up for Mirror's
    // own players.
    private void Configure()
    {
        // Each connection gets one object, made by the Network Manager, which is how its machine talks to the host. Without it the
        // host sends the machine nothing at all. It's not a person playing: those come from the controllers plugged into the
        // machine, and the characters are the game's own.
        var connectionPlayer = Resources.Load<GameObject>("ConnectionPlayer");

        if (connectionPlayer)
        {
            manager.playerPrefab = connectionPlayer;
            manager.autoCreatePlayer = true;
        }
        else
        {
            Debug.LogError("There is no ConnectionPlayer prefab, so the host can't send anything to other machines. " +
                           "Make it with Tools > Network > Create Connection Player Prefab.");
            manager.playerPrefab = null;
            manager.autoCreatePlayer = false;
        }

        // The buttons it puts on the screen are replaced by the panel below, or by the game's own menus later.
        var hud = manager.GetComponent<NetworkManagerHUD>();
        if (hud)
        {
            hud.enabled = false;
        }
    }

    private void StartLocal()
    {
        Configure();

        // Mirror switches this back on whenever it shuts down, so it has to be set again for each start.
        NetworkServer.listen = false;
        manager.StartHost();

        Mode = SessionMode.Local;
        Debug.Log("Game session: playing locally. This is a host that isn't listening, so no network port is open.");
    }

    // Changes how the game is connected. Mirror is shut down fully first, which takes a frame.
    private IEnumerator Restart(SessionMode target)
    {
        switching = true;

        switch (manager.mode)
        {
            case NetworkManagerMode.Host:
                manager.StopHost();
                break;
            case NetworkManagerMode.ClientOnly:
                manager.StopClient();
                break;
            case NetworkManagerMode.ServerOnly:
                manager.StopServer();
                break;
        }

        yield return null;

        Configure();

        // On a machine that has joined, what the host moves is only moved there, and the host decides everything. Physics still runs, for
        // what is made there only to be seen, such as the pieces of a broken tile, but it can be turned off (see the tickbox).
        SetPhysics(target != SessionMode.Joined || simulatePhysicsWhenJoined);

        switch (target)
        {
            case SessionMode.Local:
                StartLocal();
                break;

            case SessionMode.Hosting:
                NetworkServer.listen = true;
                manager.StartHost();
                Debug.Log("Game session: hosting. Other players can join.");
                break;

            case SessionMode.Joined:
                manager.networkAddress = joinAddress;
                manager.StartClient();
                Debug.Log($"Game session: joining {joinAddress}.");
                break;
        }

        Mode = target;
        switching = false;
    }

    public void Host()
    {
        if (!switching)
        {
            StartCoroutine(Restart(SessionMode.Hosting));
        }
    }

    public void Join()
    {
        if (!switching)
        {
            StartCoroutine(Restart(SessionMode.Joined));
        }
    }

    public void GoLocal()
    {
        if (!switching)
        {
            StartCoroutine(Restart(SessionMode.Local));
        }
    }

    // ----- A panel for trying it out -----

    private string StatusText()
    {
        switch (Mode)
        {
            case SessionMode.Local:
                return "Playing locally (no network port open)";

            case SessionMode.Hosting:
                var port = Transport.active is PortTransport portTransport ? $" on port {portTransport.Port}" : "";
                return $"Hosting{port}: {NetworkServer.connections.Count} connected (counting this machine)";

            case SessionMode.Joined:
                if (!NetworkClient.isConnected)
                {
                    return $"Connecting to {joinAddress}... ({(NetworkClient.isConnecting ? "connecting" : "not connecting")})";
                }

                return $"Joined to {joinAddress}: {(NetworkClient.ready ? "ready" : "not ready")}, " +
                       $"{NetworkClient.spawned.Count} objects from the host, " +
                       $"physics {(Physics2D.simulationMode == SimulationMode2D.Script ? "off" : "on")}";

            default:
                return "Starting...";
        }
    }

    void OnGUI()
    {
        if (!showPanel || !manager)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(Screen.width - 270, 10, 260, 200));
        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label(StatusText());

        if (!switching)
        {
            if (Mode == SessionMode.Local)
            {
                if (GUILayout.Button("Host a game"))
                {
                    Host();
                }

                joinAddress = GUILayout.TextField(joinAddress);

                if (GUILayout.Button("Join"))
                {
                    Join();
                }
            }
            else if (Mode != SessionMode.None)
            {
                // The extra editors can't change anything in the Inspector, so this is where it's switched, for comparing.
                if (Mode == SessionMode.Joined)
                {
                    var label = simulatePhysicsWhenJoined ? "Physics here: on (click to turn off)" : "Physics here: off (click to turn on)";

                    if (GUILayout.Button(label))
                    {
                        simulatePhysicsWhenJoined = !simulatePhysicsWhenJoined;
                    }
                }

                if (GUILayout.Button("Back to playing locally"))
                {
                    GoLocal();
                }
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
