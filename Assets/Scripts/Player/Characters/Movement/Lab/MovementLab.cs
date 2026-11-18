using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Characters;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The Movement Lab: it measures how each character really moves, running the game's own physics, and writes down what the
// movement formula said it should do next to what it did, so that what doesn't match, or doesn't feel right, can be found without
// playing every character by hand.
//
// It runs in whichever scene is open, with that scene's own characters. Each is taken to a lane of its own, high above the
// level, which is made for the test and taken away again: a flat run, a drop, and a tank for each liquid. They're all tested at
// once, each doing the same things with the same scripted buttons:
//
//   - running from standing still, then letting go: how fast, how long to get going, how far to stop;
//   - jumping with the button held, with a tap, and running at full speed;
//   - falling from a height: how fast it ends up;
//   - in each liquid: dropped in from above, how deep it settles, how many times it bobs, how long until it's still (or how
//     fast it sinks); how far a kick lifts it; and whether, kicking and steering toward a bank, it gets out onto it.
//
// Characters whose action button changes how they move (a balloon, an umbrella, a bowling ball) are tested again holding it.
//
// Start it from Tools > Movement > Movement Lab, with the game not yet playing, and look at the results there. Players mustn't
// join while it runs. Everything is put back as it was when it finishes.
public class MovementLab : MonoBehaviour
{
    [Header("The test")]
    [Tooltip("How fast it runs. The physics steps are the same size whatever this is, so the results shouldn't change, but a quick tap of the jump button is timed to the frame, so keep it at 1 to be sure.")]
    [Range(1f, 4f)]
    public float timeScale = 1f;

    [Tooltip("How far above the water the bank is that a character has to get out onto, in tiles.")]
    [Min(0f)]
    public float bankHeight = 1f;

    [Tooltip("Where the lanes are made: far from the level, and from the kill zones below it.")]
    public Vector2 labOrigin = new Vector2(2000f, 2000f);

    [Tooltip("Characters that are also tested holding their action button, by class name.")]
    public string[] actionPassTypes = { "Balloon", "Umbrella", "BowlingBall" };

    [Tooltip("Start by itself when the scene starts, instead of waiting to be started.")]
    public bool runOnStart;

    // How the lanes are laid out, in tiles, from the lane's own origin, which is the top of its floor.
    private const float LanePitch = 70f;
    private const float RunFloorLeft = -10f;
    private const float RunFloorRight = 90f;
    private const float TankStart = 100f;
    private const float TankPitch = 40f;
    private const float TankWidth = 16f;
    private const float TankDepth = 10f;
    private const float BankWidth = 10f;
    private const float FallHeight = 40f;

    private readonly List<Lane> lanes = new List<Lane>();
    private readonly List<GameObject> made = new List<GameObject>();
    private readonly List<Tilemap> madeTilemaps = new List<Tilemap>();
    private readonly List<Fluid> fluids = new List<Fluid>();
    private static Sprite whiteSprite;

    private bool started;
    private bool finished;
    private bool startedFromEditor;
    private float startedAt;
    private float previousTimeScale = 1f;
    private PlayerManager playerManager;
    private bool joiningWas;
    private string status = "";
    private LabReport report;

    // The longest it is let run, in seconds of game time, before what's been measured is kept and it stops.
    private const float MostSeconds = 400f;

#if UNITY_EDITOR
    // Started from the Movement Lab window: it asks for the game to play and for this to start with it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartFromEditor()
    {
        if (!EditorPrefs.GetBool("MovementLab.AutoRun", false))
        {
            return;
        }

        EditorPrefs.SetBool("MovementLab.AutoRun", false);

        var lab = new GameObject("Movement Lab").AddComponent<MovementLab>();
        lab.timeScale = EditorPrefs.GetFloat("MovementLab.TimeScale", 1f);
        lab.bankHeight = EditorPrefs.GetFloat("MovementLab.BankHeight", 1f);
        lab.startedFromEditor = true;
        lab.Begin();
    }
#endif

    void Start()
    {
        if (runOnStart && !started)
        {
            Begin();
        }
    }

    public void Begin()
    {
        if (started)
        {
            return;
        }

        started = true;
        StartCoroutine(Setup());
    }

    private IEnumerator Setup()
    {
        status = "Waiting for the scene to start...";

        // The characters and the map start themselves; they have to be going before they can be moved about.
        for (var frames = 0; frames < 600 && !(Map.IsInitialized() && Character.GetCharacters(false).Count > 0); frames++)
        {
            yield return null;
        }

        yield return null;
        yield return null;

        if (!Map.IsInitialized())
        {
            Fail("There's no Map in this scene, so there are no liquids to test. Open a scene with one.");
            yield break;
        }

        playerManager = FindAnyObjectByType<PlayerManager>();

        if (PlayerManager.GetJoinedPlayerCount() > 0)
        {
            Fail("Players have joined, so what they do would get in the way. Start the Lab again without anyone joining.");
            yield break;
        }

        if (playerManager)
        {
            joiningWas = playerManager.allowJoining;
            playerManager.allowJoining = false;
        }

        var characters = Character.GetCharacters(false).Where(c => c && c.movementProfile).Distinct().ToList();

        if (characters.Count == 0)
        {
            Fail("There are no characters with a movement profile in this scene.");
            yield break;
        }

        FindFluids();
        MakeSprite();
        BuildLiquids();

        for (var i = 0; i < characters.Count; i++)
        {
            var lane = new Lane(this, characters[i], labOrigin + Vector2.up * (i * LanePitch));
            lanes.Add(lane);
            BuildLane(lane);
        }

        // Everything is brought into the game, to be tested; each goes back to what it was when it's done.
        foreach (var lane in lanes)
        {
            lane.character.SetInGame(true);
        }

        yield return null;
        yield return null;

        report = new LabReport { bankHeight = bankHeight };
        previousTimeScale = Time.timeScale;
        Time.timeScale = timeScale;
        startedAt = Time.time;

        foreach (var lane in lanes)
        {
            lane.Start();
        }

        status = "Running...";
    }

    private void Fail(string message)
    {
        status = message;
        Debug.LogWarning("Movement Lab: " + message);
        finished = true;

#if UNITY_EDITOR
        if (startedFromEditor)
        {
            EditorApplication.isPlaying = false;
        }
#endif
    }

    // Every liquid to try: the presets, and any different one a liquid tilemap in the scene is.
    private void FindFluids()
    {
        fluids.Clear();
        fluids.AddRange(Fluid.Presets);

        foreach (var map in FindObjectsByType<LiquidMap>(FindObjectsInactive.Include))
        {
            var fluid = map.Fluid;

            if (!fluids.Exists(f => Mathf.Approximately(f.density, fluid.density) && Mathf.Approximately(f.viscosity, fluid.viscosity)
                && Mathf.Approximately(f.minKickHeight, fluid.minKickHeight)))
            {
                fluids.Add(new Fluid($"Liquid {fluid.density:0.##}/{fluid.viscosity:0.##}", fluid.density, fluid.viscosity, fluid.minKickHeight));
            }
        }

        fluids.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
    }

    private static void MakeSprite()
    {
        if (whiteSprite)
        {
            return;
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    // One tilemap for each liquid, for every lane's tank of it. The map is told about them so they're found like any other.
    private Dictionary<Fluid, Tilemap> liquidMaps = new Dictionary<Fluid, Tilemap>();

    private void BuildLiquids()
    {
        liquidMaps = new Dictionary<Fluid, Tilemap>();

        var grid = new GameObject("Movement Lab Liquids");
        grid.AddComponent<Grid>();
        made.Add(grid);

        foreach (var fluid in fluids)
        {
            var maker = new GameObject(fluid.name);
            maker.transform.SetParent(grid.transform, false);

            var tilemap = maker.AddComponent<Tilemap>();
            var drawing = maker.AddComponent<TilemapRenderer>();
            drawing.sortingOrder = -4;

            var liquid = maker.AddComponent<LiquidMap>();
            liquid.density = fluid.density;
            liquid.viscosity = fluid.viscosity;
            liquid.minKickHeight = fluid.minKickHeight;

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = whiteSprite;
            tile.color = Color.HSVToRGB(Mathf.Abs(fluid.name.GetHashCode() % 100) / 100f, 0.6f, 0.85f) * new Color(1f, 1f, 1f, 0.45f);
            tileFor[tilemap] = tile;

            liquidMaps[fluid] = tilemap;
            madeTilemaps.Add(tilemap);
            Map.AddTilemap(tilemap);
        }
    }

    private readonly Dictionary<Tilemap, Tile> tileFor = new Dictionary<Tilemap, Tile>();

    private GameObject Box(Transform parent, string objectName, Vector2 centre, Vector2 size, Color colour, bool solid)
    {
        var box = new GameObject(objectName);
        box.transform.SetParent(parent, false);
        box.transform.localPosition = centre;
        box.transform.localScale = new Vector3(size.x, size.y, 1f);

        var picture = box.AddComponent<SpriteRenderer>();
        picture.sprite = whiteSprite;
        picture.color = colour;
        picture.sortingOrder = -6;

        if (solid)
        {
            box.layer = LayerMask.NameToLayer("Ground");
            box.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        return box;
    }

    private void BuildLane(Lane lane)
    {
        var root = new GameObject($"Movement Lab Lane: {lane.character.name}");
        root.transform.position = lane.origin;
        made.Add(root);

        var ground = new Color(0.35f, 0.35f, 0.4f);
        var bank = new Color(0.45f, 0.4f, 0.3f);

        Box(root.transform, "Run Floor", new Vector2((RunFloorLeft + RunFloorRight) / 2f, -1f), new Vector2(RunFloorRight - RunFloorLeft, 2f), ground, true);

        for (var i = 0; i < fluids.Count; i++)
        {
            var left = TankStart + i * TankPitch;

            Box(root.transform, "Left Bank", new Vector2(left - BankWidth / 2f, -(TankDepth + 2f) / 2f), new Vector2(BankWidth, TankDepth + 2f), bank, true);
            Box(root.transform, "Tank Floor", new Vector2(left + TankWidth / 2f, -TankDepth - 1f), new Vector2(TankWidth, 2f), ground, true);
            Box(root.transform, "Right Bank", new Vector2(left + TankWidth + BankWidth / 2f, (bankHeight - TankDepth - 2f) / 2f),
                new Vector2(BankWidth, bankHeight + TankDepth + 2f), bank, true);

            var tilemap = liquidMaps[fluids[i]];
            var tile = tileFor[tilemap];
            var originCell = new Vector2Int(Mathf.RoundToInt(lane.origin.x), Mathf.RoundToInt(lane.origin.y));

            for (var x = 0; x < (int)TankWidth; x++)
            {
                for (var y = -(int)TankDepth; y < 0; y++)
                {
                    tilemap.SetTile(new Vector3Int(originCell.x + (int)left + x, originCell.y + y, 0), tile);
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (lanes.Count == 0 || finished || report == null)
        {
            return;
        }

        foreach (var lane in lanes)
        {
            lane.Tick();
            lane.Apply();
        }

        if (lanes.All(l => l.done))
        {
            Finish("Finished.");
        }
        else if (Time.time - startedAt > MostSeconds)
        {
            Finish("Stopped, as it was taking too long. What was measured is kept.");
        }
    }

    // The buttons are held every frame as well as every step, as the game's own controls are, so that a character always sees
    // them, whatever the frame rate.
    void Update()
    {
        if (lanes.Count == 0 || finished || report == null)
        {
            return;
        }

        foreach (var lane in lanes)
        {
            lane.Apply();
        }
    }

    private void Finish(string message)
    {
        finished = true;
        report.when = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        foreach (var lane in lanes)
        {
            report.entries.AddRange(lane.entries);
        }

        report.Save();
        Restore();

        status = $"{message} {report.entries.Count} results saved to {LabReport.FilePath}. Open Tools > Movement > Movement Lab to look at them.";
        Debug.Log("Movement Lab: " + status);

#if UNITY_EDITOR
        if (startedFromEditor)
        {
            EditorApplication.isPlaying = false;
        }
#endif
    }

    // Everything as it was: the characters where they were and in or out of the game, the time, the joining, and the lanes gone.
    private bool restored;

    private void Restore()
    {
        if (restored)
        {
            return;
        }

        restored = true;
        Time.timeScale = previousTimeScale;

        foreach (var lane in lanes)
        {
            lane.PutBack();
        }

        foreach (var tilemap in madeTilemaps)
        {
            Map.RemoveTilemap(tilemap);
        }

        foreach (var thing in made)
        {
            if (thing)
            {
                Destroy(thing);
            }
        }

        if (playerManager)
        {
            playerManager.allowJoining = joiningWas;
        }
    }

    void OnDestroy()
    {
        // Stopped partway: the scene is left as it was found.
        if (started && !restored && report != null)
        {
            Restore();
        }
    }

    void OnGUI()
    {
        var box = new Rect(10f, 10f, 520f, 40f + lanes.Count * 18f);
        GUI.Box(box, "Movement Lab");

        var line = new Rect(20f, 32f, 500f, 18f);
        GUI.Label(line, status);

        foreach (var lane in lanes)
        {
            line.y += 18f;
            GUI.Label(line, $"{lane.character.name}: {(lane.done ? "done" : lane.phase)}");
        }
    }

    // ----- One character's test -----

    private class Lane
    {
        public readonly Character character;
        public readonly Vector2 origin;
        public readonly List<LabEntry> entries = new List<LabEntry>();
        public bool done;
        public string phase = "waiting";

        private readonly MovementLab lab;
        private readonly Rigidbody2D body;
        private readonly MovementProfile profile;
        private readonly Vector2 originalPosition;
        private readonly bool originalInGame;
        private readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();

        // The buttons the test is holding, which are what the character is told.
        private Vector2 steer;
        private bool jump;
        private bool action;
        private bool actionWasHeld;

        private static readonly Collider2D[] colliders = new Collider2D[8];

        private const float RunSeconds = 5f;
        private const float StopSeconds = 4f;
        private const float RunUpSeconds = 2.5f;
        private const float WaterSeconds = 14f;
        private const float EscapeSeconds = 20f;

        private float Step { get { return Time.fixedDeltaTime; } }

        public Lane(MovementLab lab, Character character, Vector2 origin)
        {
            this.lab = lab;
            this.character = character;
            this.origin = origin;

            body = character.GetComponent<Rigidbody2D>();
            profile = character.movementProfile;
            originalPosition = character.transform.position;
            originalInGame = character.InGame;
        }

        public void Start()
        {
            routines.Push(Everything());
        }

        // Moves the test on by a physics step.
        public void Tick()
        {
            if (done)
            {
                return;
            }

            while (routines.Count > 0)
            {
                var top = routines.Peek();

                if (!top.MoveNext())
                {
                    routines.Pop();
                    continue;
                }

                if (top.Current is IEnumerator inner)
                {
                    routines.Push(inner);
                    continue;
                }

                return;
            }

            done = true;
            steer = Vector2.zero;
            jump = false;
            action = false;
            Apply();
        }

        // Tells the character what the buttons are, as the controls do.
        public void Apply()
        {
            if (!character)
            {
                return;
            }

            character.DirectionPress(steer);
            character.Jump(jump);
            character.Action(action);

            if (action && !actionWasHeld)
            {
                character.FirstActionPress();
            }

            actionWasHeld = action;
        }

        public void PutBack()
        {
            if (!character)
            {
                return;
            }

            steer = Vector2.zero;
            jump = false;
            action = false;
            Apply();

            character.Teleport(originalPosition);
            character.SetInGame(originalInGame);
        }

        private IEnumerator Everything()
        {
            yield return Pass("base", false, true);

            if (lab.actionPassTypes.Contains(character.GetType().Name))
            {
                yield return Pass("action", true, false);
            }
        }

        private IEnumerator Pass(string pass, bool holdAction, bool inLiquids)
        {
            var entry = new LabEntry
            {
                character = character.name,
                pass = pass,
                mode = "base",
                material = profile.material ? profile.material.name : "none",
                mass = profile.mass,
                density = profile.Density
            };

            entries.Add(entry);
            action = holdAction;

            phase = $"{pass}: running";
            yield return RunAndStop(entry);

            phase = $"{pass}: jumping";
            yield return Jumps(entry);

            phase = $"{pass}: falling";
            yield return Fall(entry);

            // What the formula says for the mode he was in.
            var expected = new ResolvedMovement();
            profile.Resolve(entry.mode, null, MovementTuning.Current, expected);
            entry.expectedTopSpeed = expected.maxMoveSpeed;
            entry.expectedTimeToTop = expected.timeToTopSpeed;
            entry.expectedStopDistance = expected.stopDistance;
            entry.expectedJumpHeight = expected.jumpHeight;
            entry.expectedJumpDistance = expected.jumpDistance;
            entry.expectedTerminalSpeed = expected.maxFallSpeed;

            if (inLiquids)
            {
                for (var i = 0; i < lab.fluids.Count; i++)
                {
                    var result = new LabFluidResult { fluid = lab.fluids[i].name, density = lab.fluids[i].density, viscosity = lab.fluids[i].viscosity };
                    entry.fluids.Add(result);

                    phase = $"{lab.fluids[i].name}: settling";
                    yield return Settling(result, lab.fluids[i], i);

                    phase = $"{lab.fluids[i].name}: kicking";
                    yield return Kick(result, i);

                    phase = $"{lab.fluids[i].name}: getting out";
                    yield return Escape(result, i);
                }
            }

            action = false;
            steer = Vector2.zero;
            jump = false;
        }

        // ----- Helpers -----

        private IEnumerator Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += Step)
            {
                yield return null;
            }
        }

        private Bounds BodyBounds()
        {
            var count = body.GetAttachedColliders(colliders);
            var bounds = new Bounds(body.position, Vector3.zero);
            var found = false;

            for (var i = 0; i < count; i++)
            {
                if (colliders[i].isTrigger || !colliders[i].enabled)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
                else
                {
                    bounds = colliders[i].bounds;
                    found = true;
                }
            }

            return bounds;
        }

        // How high the bottom of him is above the lane's floor.
        private float Feet()
        {
            return BodyBounds().min.y - origin.y;
        }

        private bool Grounded()
        {
            return character.canJump && Mathf.Abs(body.linearVelocity.y) < 0.1f;
        }

        // Puts him somewhere, at rest, with nothing held.
        private IEnumerator Place(Vector2 local)
        {
            steer = Vector2.zero;
            jump = false;

            character.Teleport(origin + local);
            yield return Wait(0.3f);
        }

        private IEnumerator Settle()
        {
            for (var t = 0f; t < 2.5f && !Grounded(); t += Step)
            {
                yield return null;
            }
        }

        // ----- The tests -----

        private IEnumerator RunAndStop(LabEntry entry)
        {
            yield return Place(new Vector2(0f, 0.7f));
            yield return Settle();

            var speeds = new List<Vector2>();
            steer = Vector2.right;

            for (var t = 0f; t < RunSeconds; t += Step)
            {
                yield return null;
                speeds.Add(new Vector2(t + Step, Mathf.Abs(body.linearVelocity.x)));
            }

            entry.mode = string.IsNullOrEmpty(character.MovementModeName) ? "base" : character.MovementModeName;

            // How fast he settled at: the last second.
            var settled = speeds.Where(s => s.x >= RunSeconds - 1f).Select(s => s.y).DefaultIfEmpty(0f).Average();
            entry.topSpeed = settled;
            entry.moved = settled > 0.05f;

            if (entry.moved)
            {
                // Linear speeding up takes 95% of the time to get to 95% of the speed.
                var reached = speeds.FirstOrDefault(s => s.y >= settled * 0.95f);
                entry.timeToTop = reached.x > 0f ? reached.x / 0.95f : -1f;
            }

            var releasedAt = body.position.x;
            steer = Vector2.zero;
            entry.stopSeconds = -1f;

            for (var t = 0f; t < StopSeconds; t += Step)
            {
                yield return null;

                if (Mathf.Abs(body.linearVelocity.x) < 0.1f)
                {
                    entry.stopSeconds = t + Step;
                    break;
                }
            }

            entry.stopDistance = Mathf.Abs(body.position.x - releasedAt);
        }

        private IEnumerator Jumps(LabEntry entry)
        {
            yield return Jump(entry, false, false);
            yield return Jump(entry, true, false);
            yield return Jump(entry, false, true);
        }

        private IEnumerator Jump(LabEntry entry, bool tap, bool running)
        {
            yield return Place(new Vector2(0f, 0.7f));
            yield return Settle();

            if (running)
            {
                steer = Vector2.right;
                yield return Wait(RunUpSeconds);
            }

            var footStart = Feet();
            var startX = body.position.x;
            var apex = footStart;
            var apexAt = 0f;
            var released = false;
            var landedX = startX;
            var airTime = 0f;

            jump = true;

            for (var t = 0f; t < 6f; t += Step)
            {
                yield return null;
                airTime = t + Step;

                var feet = Feet();

                if (feet > apex)
                {
                    apex = feet;
                    apexAt = t + Step;
                }

                // A tap is a single step; otherwise the button is held until the top.
                if (tap)
                {
                    jump = false;
                }
                else if (!released && t > 0.1f && body.linearVelocity.y <= 0f)
                {
                    jump = false;
                    released = true;
                }

                if (t > 0.25f && Grounded())
                {
                    landedX = body.position.x;
                    break;
                }

                landedX = body.position.x;
            }

            jump = false;
            steer = Vector2.zero;

            var height = apex - footStart;

            if (tap)
            {
                entry.tapHeight = height;
            }
            else if (running)
            {
                entry.jumpDistance = Mathf.Abs(landedX - startX);
            }
            else
            {
                entry.jumpHeight = height;
                entry.apexSeconds = apexAt;
                entry.airSeconds = airTime;
            }
        }

        private IEnumerator Fall(LabEntry entry)
        {
            yield return Place(new Vector2(5f, FallHeight));

            var fastest = 0f;
            var fell = 0f;

            for (var t = 0f; t < 12f; t += Step)
            {
                yield return null;
                fell = t + Step;
                fastest = Mathf.Max(fastest, -body.linearVelocity.y);

                if (t > 0.5f && Grounded())
                {
                    break;
                }
            }

            entry.terminalSpeed = fastest;
            entry.fallSeconds = fell;
        }

        // Dropped into the tank from above, and left to settle.
        private IEnumerator Settling(LabFluidResult result, Fluid fluid, int index)
        {
            var left = TankStart + index * TankPitch;
            var tuning = MovementTuning.Current;

            profile.DescribeIn(fluid, tuning, "base", out result.expectedFloats, out result.expectedUnder, out result.expectedSinkSpeed);

            var inFluid = new ResolvedMovement();
            profile.Resolve("base", fluid, tuning, inFluid);
            result.expectedKick = inFluid.jumpHeight;

            yield return Place(new Vector2(left + TankWidth / 2f, 4f));

            var times = new List<float>();
            var feet = new List<float>();

            for (var t = 0f; t < WaterSeconds; t += Step)
            {
                yield return null;
                times.Add(t + Step);
                feet.Add(Feet());
            }

            var height = Mathf.Max(0.01f, BodyBounds().size.y);

            var tail = feet.Where((f, i) => times[i] >= WaterSeconds - 2f).ToList();
            var rest = tail.Count > 0 ? tail.Average() : feet.Last();
            var onBottom = rest < -TankDepth + 0.3f;

            result.floats = !onBottom;

            if (onBottom)
            {
                result.under = 1f;

                var atBottom = feet.FindIndex(f => f <= -TankDepth + 0.2f);
                result.settleSeconds = atBottom >= 0 ? times[atBottom] : -1f;

                var top = feet.FindIndex(f => f <= -1f);
                var low = feet.FindIndex(f => f <= -8f);

                if (top >= 0 && low > top && times[low] > times[top])
                {
                    result.sinkSpeed = 7f / (times[low] - times[top]);
                }
            }
            else
            {
                result.under = Mathf.Clamp01(-rest / height);

                // The last time he was more than a hair from where he stopped, and how many times he came back up after going in.
                var lastMoving = -1f;
                var entered = false;
                var bobs = 0;

                for (var i = 0; i < feet.Count; i++)
                {
                    if (Mathf.Abs(feet[i] - rest) > 0.05f)
                    {
                        lastMoving = times[i];
                    }

                    if (feet[i] < 0f)
                    {
                        entered = true;
                    }

                    if (entered && i > 0 && i < feet.Count - 1 && feet[i] > feet[i - 1] && feet[i] >= feet[i + 1] && feet[i] - rest > 0.04f)
                    {
                        bobs++;
                    }
                }

                result.bobs = bobs;
                result.settleSeconds = lastMoving > WaterSeconds - 0.5f ? -1f : Mathf.Max(0f, lastMoving);
            }
        }

        // One kick from where he is, which is rest in the tank: how far it lifts him.
        private IEnumerator Kick(LabFluidResult result, int index)
        {
            var feetStart = Feet();
            var best = feetStart;
            jump = true;

            for (var t = 0f; t < 1.5f; t += Step)
            {
                yield return null;
                jump = false;
                best = Mathf.Max(best, Feet());
            }

            result.kickRise = best - feetStart;
        }

        // From the bottom of the tank, steering toward the bank and kicking, does he get out onto it?
        private IEnumerator Escape(LabFluidResult result, int index)
        {
            var left = TankStart + index * TankPitch;
            var bankTop = lab.bankHeight;

            yield return Place(new Vector2(left + 3f, -TankDepth + 1f));

            steer = Vector2.right;
            var nextKick = 0.3f;

            for (var t = 0f; t < EscapeSeconds; t += Step)
            {
                yield return null;

                if (t >= nextKick)
                {
                    jump = true;
                    nextKick = t + 0.4f;
                }
                else
                {
                    jump = false;
                }

                if (Feet() >= bankTop - 0.25f && body.position.x > origin.x + left + TankWidth + 0.4f)
                {
                    result.escaped = true;
                    result.escapeSeconds = t + Step;
                    break;
                }
            }

            steer = Vector2.zero;
            jump = false;
        }
    }
}
