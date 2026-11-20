using System.Collections.Generic;
using System.Linq;
using Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Tools > Tutorial Yard > Open Builder: builds the first area, the Tutorial Yard, into the scene that is open, as set out in
// Docs/TutorialYard.md. It makes the ground, the two hubs, the gates, buttons, lever and crate, the prompts and the kill zones,
// all under one object called "Tutorial Yard", and the whole of it can be undone, or removed with the button, and made again.
//
// It only makes the layout, with plain coloured blocks for gates and a plain look for everything else, so there is something
// to play. Nothing it makes is special: every piece is an ordinary object that can be moved, resized, swapped for better
// looking art, or deleted afterward.
public class TutorialYardBuilder : EditorWindow
{
    private const string RootName = "Tutorial Yard";

    // The layout, left to right, in tiles from the start of the Yard. See the doc for what each part is for.
    private const int StartHubX = 1;
    private const int JumpsStartX = 12;
    private const int PitStartX = 24;
    private const int GateAX = 38;
    private const int ButtonAX = 33;
    private const int ButtonA2X = 43;
    private const int LedgeStartX = 52;
    private const int LedgeEndX = 57;
    private const int CrateX = 54;
    private const int ButtonBX = 64;
    private const int GateBX = 74;
    private const int LeverX = 84;
    private const int GateCX = 90;
    private const int ButtonD1X = 97;
    private const int GateD1X = 104;
    private const int ButtonD2X = 109;
    private const int GateD2X = 116;
    private const int ExitHubX = 121;
    private const int YardEndX = 136;

    [SerializeField] private TileBase groundTile;
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private GameObject leverPrefab;
    [SerializeField] private GameObject cratePrefab;

    [SerializeField] private Vector2Int origin = new Vector2Int(-400, 0);
    [SerializeField, Min(2)] private int wallHeight = 8;
    [SerializeField, Min(1)] private int floorThickness = 4;
    [SerializeField, Min(1)] private int firstStepHeight = 1;
    [SerializeField, Min(1)] private int secondStepHeight = 2;
    [SerializeField, Range(2, 4)] private int pitWidth = 3;
    [SerializeField, Min(1)] private int ledgeHeight = 2;
    [SerializeField, Min(0.1f)] private float spawnHeight = 1f;
    [SerializeField, Min(0f)] private float buttonMinimumWeight = 5f;

    [SerializeField] private bool moveStarters = true;
    [SerializeField] private bool setStartersOnly;

    private static readonly string[] StarterNames = { "Brad", "Die", "Paperclip", "Pencil" };

    private static Sprite blockSprite;
    private Vector2 scroll;

    [MenuItem("Tools/Tutorial Yard/Open Builder")]
    public static void Open()
    {
        var window = GetWindow<TutorialYardBuilder>("Tutorial Yard");
        window.minSize = new Vector2(380f, 560f);
        window.FindDefaults();
    }

    // The button and lever prefabs that are already in the project.
    private void FindDefaults()
    {
        if (!buttonPrefab)
        {
            buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Button.prefab");
        }

        if (!leverPrefab)
        {
            leverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lever.prefab");
        }
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "Builds the Tutorial Yard into the scene that is open, under one object called \"" + RootName + "\". " +
            "It can be undone, or removed and made again with the buttons below.", MessageType.Info);

        EditorGUILayout.LabelField("What it uses", EditorStyles.boldLabel);
        groundTile = (TileBase)EditorGUILayout.ObjectField(
            new GUIContent("Ground Tile", "The tile the floors, steps and ledge are made from. Required."), groundTile, typeof(TileBase), false);
        buttonPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Button Prefab", "Assets/Prefabs/Button.prefab. Required."), buttonPrefab, typeof(GameObject), false);
        leverPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Lever Prefab", "Assets/Prefabs/Lever.prefab. Required."), leverPrefab, typeof(GameObject), false);
        cratePrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Crate Prefab", "A crate that can be carried. Optional: if it's empty, a plain one is made, which may need adjusting."),
            cratePrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Where and how big", EditorStyles.boldLabel);
        origin = EditorGUILayout.Vector2IntField(
            new GUIContent("Start Position", "Where the left end of the Yard goes, on the floor, in whole tiles. Choose somewhere with nothing else in it. The Yard is about 136 tiles wide."), origin);
        wallHeight = EditorGUILayout.IntSlider(
            new GUIContent("Gate Height", "How tall the gates are, in tiles. Taller than anyone can jump."), wallHeight, 4, 16);
        floorThickness = EditorGUILayout.IntSlider(
            new GUIContent("Floor Thickness", "How many tiles deep the ground is."), floorThickness, 1, 8);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Jumps (test with every starter)", EditorStyles.boldLabel);
        firstStepHeight = EditorGUILayout.IntSlider(
            new GUIContent("First Step", "Height of the first step, in tiles."), firstStepHeight, 1, 4);
        secondStepHeight = EditorGUILayout.IntSlider(
            new GUIContent("Second Step", "Height of the second step, in tiles."), secondStepHeight, 1, 5);
        pitWidth = EditorGUILayout.IntSlider(
            new GUIContent("Pit Width", "How wide the gap is, in tiles."), pitWidth, 2, 4);
        ledgeHeight = EditorGUILayout.IntSlider(
            new GUIContent("Crate Ledge", "Height of the ledge the crate sits on, in tiles."), ledgeHeight, 1, 5);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Buttons", EditorStyles.boldLabel);
        buttonMinimumWeight = EditorGUILayout.Slider(
            new GUIContent("Minimum Weight", "The weight, as the mass of what's standing on it, that takes each button all the way down. Less than that and it goes down by the same fraction, and a button only switches once it's down as far as its Percent Pressed To Activate. The starters weigh 20 (Paperclip) to 80 (Die), and the crate 3. The crate has to be able to hold the button for Gate B, so with a button that switches at 55 percent, this can be at most about 5. Raise it to make buttons need heavier things; lower it to make them easier."),
            buttonMinimumWeight, 0f, 100f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
        spawnHeight = EditorGUILayout.FloatField(
            new GUIContent("Spawn Height", "How high above the floor a character is put at a hub, in units."), spawnHeight);
        moveStarters = EditorGUILayout.ToggleLeft(
            new GUIContent("Move the four starters to the start hub", "Moves Brad, Die, Paperclip and Pencil, if they are in the scene, to the spawn points of the start hub."),
            moveStarters);
        setStartersOnly = EditorGUILayout.ToggleLeft(
            new GUIContent("Set the CharacterScript to the four starters only", "Ticks Brad, Die, Paperclip and Pencil on the CharacterScript asset and unticks everyone else. It changes the asset, so leave it off to keep your own test setup."),
            setStartersOnly);

        EditorGUILayout.Space();

        var ready = groundTile && buttonPrefab && leverPrefab;

        if (!ready)
        {
            EditorGUILayout.HelpBox("The Ground Tile, Button Prefab and Lever Prefab are needed.", MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("Build the Tutorial Yard", GUILayout.Height(32f)))
            {
                Build();
            }
        }

        if (GUILayout.Button("Remove the Tutorial Yard"))
        {
            Remove();
        }

        EditorGUILayout.EndScrollView();
    }

    private void Remove()
    {
        var existing = GameObject.Find(RootName);

        if (!existing)
        {
            ShowNotification(new GUIContent("There is no Tutorial Yard in this scene."));
            return;
        }

        if (EditorUtility.DisplayDialog("Remove the Tutorial Yard?", "Everything under the \"" + RootName + "\" object will be deleted. This can be undone.", "Remove", "Cancel"))
        {
            Undo.DestroyObjectImmediate(existing);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }

    // Everything the Yard is made of, in order.
    private void Build()
    {
        var existing = GameObject.Find(RootName);

        if (existing)
        {
            if (!EditorUtility.DisplayDialog("Replace the Tutorial Yard?", "There is a Tutorial Yard in this scene already. It will be removed and built again.", "Replace", "Cancel"))
            {
                return;
            }

            Undo.DestroyObjectImmediate(existing);
        }

        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build the Tutorial Yard");

        var root = new GameObject(RootName);
        root.transform.position = new Vector3(origin.x, origin.y, 0f);

        BuildGround(root.transform);

        var hubSpawns = new List<Vector2>();
        BuildHub(root.transform, "Start Hub", "Start", StartHubX, true, hubSpawns);
        BuildHub(root.transform, "Yard Gate Hub", "Yard Gate", ExitHubX, false, null);

        // Gate A: a button in front, and another past it, to hold it from the far side.
        var gateA = MakeDoor(root.transform, "Gate A", GateAX);
        MakeButton(root.transform, "Button A", ButtonAX, gateA);
        MakeButton(root.transform, "Button A2", ButtonA2X, gateA);

        // Gate B: held open by a crate left on its button.
        var gateB = MakeDoor(root.transform, "Gate B", GateBX);
        MakeButton(root.transform, "Button B", ButtonBX, gateB);
        MakeCrate(root.transform, LedgeStartX + (CrateX - LedgeStartX), ledgeHeight);

        // Gate C: opened by the lever.
        var gateC = MakeDoor(root.transform, "Gate C", GateCX);
        MakeLever(root.transform, "Lever", LeverX, gateC);

        // Gates D1 and D2: one character on each button, and a third through both.
        var gateD1 = MakeDoor(root.transform, "Gate D1", GateD1X);
        var gateD2 = MakeDoor(root.transform, "Gate D2", GateD2X);
        MakeButton(root.transform, "Button D1", ButtonD1X, gateD1);
        MakeButton(root.transform, "Button D2", ButtonD2X, gateD2);

        BuildPrompts(root.transform);
        BuildKillZones(root.transform);

        if (moveStarters)
        {
            MoveStarters(root.transform, hubSpawns);
        }

        if (setStartersOnly)
        {
            SetStartersOnly();
        }

        Undo.RegisterCreatedObjectUndo(root, "Build the Tutorial Yard");
        Undo.CollapseUndoOperations(group);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log($"Built the Tutorial Yard at {origin}, about {YardEndX} tiles wide. Playtest each jump with every starter, and " +
                  "see Docs/TutorialYard.md for what is where.", root);
    }

    // The floors, steps, ledge and pit, as a tilemap on the Ground layer.
    private void BuildGround(Transform parent)
    {
        // The grid is under the Yard, so it moves with it. The game's tile code works out where a tile is from where its tilemap
        // is, so the Yard can be put anywhere.
        var gridObject = new GameObject("Ground Grid");
        gridObject.transform.SetParent(parent, false);
        gridObject.AddComponent<Grid>();

        var tilemapObject = new GameObject("Ground");
        tilemapObject.transform.SetParent(gridObject.transform, false);
        tilemapObject.layer = LayerMask.NameToLayer("Ground");

        var tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        var tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();

        // The tiles' colliders are merged into one shape, with no seams between them for a character to catch on while
        // walking. The composite collider needs a body to hang on, which adds one, and which is made static: the ground
        // doesn't move.
        var composite = tilemapObject.AddComponent<CompositeCollider2D>();
        var groundBody = tilemapObject.GetComponent<Rigidbody2D>();

        if (groundBody)
        {
            groundBody.bodyType = RigidbodyType2D.Static;
        }

        tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
        composite.geometryType = CompositeCollider2D.GeometryType.Outlines;

        // Each stretch of the Yard is a start, an end (not including it) and how high the ground comes up to.
        var pitEnd = PitStartX + pitWidth;
        var stretches = new List<(int start, int end, int top)>
        {
            (0, JumpsStartX, 0),
            (JumpsStartX, JumpsStartX + 3, firstStepHeight),
            (JumpsStartX + 3, PitStartX - 5, secondStepHeight),
            (PitStartX - 5, PitStartX, 0),
            (pitEnd, LedgeStartX, 0),
            (LedgeStartX, LedgeEndX, ledgeHeight),
            (LedgeEndX, YardEndX, 0)
        };

        foreach (var (start, end, top) in stretches)
        {
            for (int x = start; x < end; x++)
            {
                for (int y = top - floorThickness; y < top; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
                }
            }
        }
    }

    // A hub with its area, a pole, and a flag that shows once it has been reached. The spawn spots are for four characters.
    private void BuildHub(Transform parent, string objectName, string hubName, int x, bool start, List<Vector2> spawnsOut)
    {
        var hubObject = new GameObject(objectName);
        hubObject.transform.SetParent(parent, false);
        hubObject.transform.localPosition = new Vector3(x, 0f, 0f);

        // The area that counts as being at the hub: wide enough for the whole party to stand in.
        var area = hubObject.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = new Vector2(9f, 5f);
        area.offset = new Vector2(4.5f, 2.5f);

        var hub = hubObject.AddComponent<CharacterHub>();
        hub.hubName = hubName;
        hub.startHub = start;

        var points = new Transform[4];

        for (int i = 0; i < points.Length; i++)
        {
            var point = new GameObject("Spawn " + (i + 1));
            point.transform.SetParent(hubObject.transform, false);
            point.transform.localPosition = new Vector3(1.5f + i * 2f, spawnHeight, 0f);
            points[i] = point.transform;

            if (spawnsOut != null)
            {
                spawnsOut.Add(point.transform.position);
            }
        }

        hub.spawnPoints = points;

        // A plain pad on the floor, a pole, and a green flag that is hidden until the hub is reached.
        MakeBlock("Pad", hubObject.transform, new Vector2(4.5f, 0.1f), new Vector2(9f, 0.2f), new Color(0.35f, 0.35f, 0.4f), -1);
        MakeBlock("Pole", hubObject.transform, new Vector2(0.4f, 1.8f), new Vector2(0.15f, 3.4f), new Color(0.6f, 0.6f, 0.6f), -1);

        var flag = MakeBlock("Reached Flag", hubObject.transform, new Vector2(1.0f, 3.0f), new Vector2(1.2f, 0.8f), new Color(0.3f, 0.85f, 0.4f), 0);
        hub.reachedVisual = flag;
    }

    // A plain coloured rectangle, for gates, pads and the like. No collider: callers add what they need.
    private GameObject MakeBlock(string objectName, Transform parent, Vector2 localCentre, Vector2 size, Color colour, int sortingOrder)
    {
        var block = new GameObject(objectName);
        block.transform.SetParent(parent, false);
        block.transform.localPosition = new Vector3(localCentre.x, localCentre.y, 0f);

        var sprite = BlockSprite();
        var renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = colour;
        renderer.sortingOrder = sortingOrder;

        if (sprite)
        {
            var native = sprite.bounds.size;
            block.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        }

        return block;
    }

    private static Sprite BlockSprite()
    {
        if (!blockSprite)
        {
            blockSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        return blockSprite;
    }

    // A gate: a tall block on the floor, on the Ground layer so characters stand against it, that slides up out of the way.
    private SwitchDoor MakeDoor(Transform parent, string objectName, int x)
    {
        var door = MakeBlock(objectName, parent, new Vector2(x + 0.5f, wallHeight / 2f), new Vector2(1f, wallHeight), new Color(0.55f, 0.4f, 0.25f), 1);
        door.layer = LayerMask.NameToLayer("Ground");

        // The collider is the whole of the block, whatever size its picture is scaled to.
        var collider = door.AddComponent<BoxCollider2D>();
        var doorSprite = door.GetComponent<SpriteRenderer>().sprite;
        collider.size = doorSprite ? (Vector2)doorSprite.bounds.size : Vector2.one;

        var body = door.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        var switchDoor = door.AddComponent<SwitchDoor>();
        switchDoor.openOffset = new Vector2(0f, wallHeight + 0.5f);
        switchDoor.speed = 6f;

        return switchDoor;
    }

    // A button from the project's prefab, standing on the floor, that opens the door while it's pressed.
    private void MakeButton(Transform parent, string objectName, int centreX, SwitchDoor door)
    {
        var button = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, parent);
        button.name = objectName;

        // The prefab's own origin is the middle of its base, which is a tile thick.
        button.transform.localPosition = new Vector3(centreX, 0.5f, 0f);

        var switchComponent = button.GetComponent<ButtonSwitch>();

        if (!switchComponent)
        {
            Debug.LogWarning($"{objectName}: the button prefab has no ButtonSwitch, so it opens nothing.", button);
            return;
        }

        var serialized = new SerializedObject(switchComponent);
        LinkSwitchables(serialized.FindProperty("switchableObject"), door);

        // So it keeps telling the door while it's pressed, and the door shuts again when it's let go.
        serialized.FindProperty("staysActiveWhenOn").boolValue = true;

        // How much it takes to press it all the way down.
        serialized.FindProperty("minimumWeight").floatValue = buttonMinimumWeight;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void MakeLever(Transform parent, string objectName, int x, SwitchDoor door)
    {
        var lever = (GameObject)PrefabUtility.InstantiatePrefab(leverPrefab, parent);
        lever.name = objectName;
        lever.transform.localPosition = new Vector3(x, 0.125f, 0f);

        // LeverSwitch uses a trigger collider on its own object as its reach. The prefab doesn't have one (the levers in the
        // scenes each have one added by hand), so add it here, the same as those: a trigger, one unit square.
        if (!lever.GetComponent<BoxCollider2D>())
        {
            var reach = lever.AddComponent<BoxCollider2D>();
            reach.isTrigger = true;
            reach.size = Vector2.one;
        }

        var switchComponent = lever.GetComponent<LeverSwitch>();

        if (!switchComponent)
        {
            Debug.LogWarning($"{objectName}: the lever prefab has no LeverSwitch, so it opens nothing.", lever);
            return;
        }

        var serialized = new SerializedObject(switchComponent);
        LinkSwitchables(serialized.FindProperty("switchableObjects"), door);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Adds a door to the end of the list of what a button or lever switches.
    private static void LinkSwitchables(SerializedProperty list, SwitchDoor door)
    {
        // Anything already in it, such as from the prefab, is kept.
        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = door;
    }

    // A crate that can be carried, on top of the ledge. Without a prefab, a plain one is made, with a trigger around it for the
    // carry range and a joint to hold it to whoever has it.
    private void MakeCrate(Transform parent, int x, int ledge)
    {
        GameObject crate;

        if (cratePrefab)
        {
            crate = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, parent);
            crate.name = "Crate";
            crate.transform.localPosition = new Vector3(x + 0.5f, ledge + 0.6f, 0f);
            return;
        }

        crate = MakeBlock("Crate", parent, new Vector2(x + 0.5f, ledge + 0.6f), new Vector2(1f, 1f), new Color(0.85f, 0.6f, 0.2f), 2);
        var crateSprite = crate.GetComponent<SpriteRenderer>().sprite;
        var size = crateSprite ? (Vector2)crateSprite.bounds.size : Vector2.one;

        var solid = crate.AddComponent<BoxCollider2D>();
        solid.size = size;

        // How near someone has to be to pick it up, which is what the carry script looks for.
        var reach = crate.AddComponent<BoxCollider2D>();
        reach.isTrigger = true;
        reach.size = size * 1.6f;

        var body = crate.AddComponent<Rigidbody2D>();
        body.mass = 3f;
        body.gravityScale = 2f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // The carry script switches this on and joins it to whoever picks it up.
        crate.AddComponent<FixedJoint2D>();
        crate.AddComponent<CarryableObject>();
    }

    private void BuildPrompts(Transform parent)
    {
        MakePrompt(parent, "Prompt: Move", "Move", 6f, new Vector2(10f, 6f));
        MakePrompt(parent, "Prompt: Jump", "Jump", JumpsStartX - 2f, new Vector2(5f, 6f));
        MakePrompt(parent, "Prompt: Button", "Button", ButtonAX - 4f, new Vector2(5f, 6f));
        MakePrompt(parent, "Prompt: Switch On Button", "SwitchOnButton", ButtonAX, new Vector2(5f, 6f));
        MakePrompt(parent, "Prompt: Switch Character", "SwitchCharacter", GateAX - 3f, new Vector2(5f, 8f));
        MakePrompt(parent, "Prompt: Carry", "Carry", LedgeStartX + 1f, new Vector2(8f, 8f));
        MakePrompt(parent, "Prompt: Lever", "Interact", LeverX, new Vector2(7f, 6f));
        MakePrompt(parent, "Prompt: Teamwork", "Teamwork", (ButtonD1X + GateD2X) / 2f, new Vector2(10f, 8f));
        MakePrompt(parent, "Prompt: Hub", "Hub", ExitHubX + 4.5f, new Vector2(9f, 6f));
    }

    private void MakePrompt(Transform parent, string objectName, string topic, float x, Vector2 size)
    {
        var prompt = new GameObject(objectName);
        prompt.transform.SetParent(parent, false);
        prompt.transform.localPosition = new Vector3(x, size.y / 2f, 0f);

        var area = prompt.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = size;

        prompt.AddComponent<Hint>().topic = topic;
    }

    // One in the pit, close under the floor so nobody falls far, and one far below the whole Yard for anything else.
    private void BuildKillZones(Transform parent)
    {
        MakeKillZone(parent, "Kill Zone: Pit", new Vector2(PitStartX + pitWidth / 2f, -2.5f), new Vector2(pitWidth, 3f));

        var below = -floorThickness - 12f;
        MakeKillZone(parent, "Kill Zone: Below", new Vector2(YardEndX / 2f, below), new Vector2(YardEndX + 40f, 4f));
    }

    private void MakeKillZone(Transform parent, string objectName, Vector2 centre, Vector2 size)
    {
        var zone = new GameObject(objectName);
        zone.transform.SetParent(parent, false);
        zone.transform.localPosition = new Vector3(centre.x, centre.y, 0f);

        var area = zone.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = size;

        zone.AddComponent<KillZone>();
    }

    // The four starters, if they're in the scene, go to the spawn spots of the start hub.
    private void MoveStarters(Transform root, List<Vector2> spawns)
    {
        var found = 0;

        foreach (var character in Resources.FindObjectsOfTypeAll<Character>())
        {
            // Only characters that are in a scene, not ones in project assets.
            if (!character.gameObject.scene.IsValid() || character.gameObject.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            var index = System.Array.IndexOf(StarterNames, character.name);

            if (index < 0 || index >= spawns.Count)
            {
                continue;
            }

            Undo.RecordObject(character.transform, "Move starter");
            character.transform.position = new Vector3(spawns[index].x, spawns[index].y, character.transform.position.z);
            found++;
        }

        if (found < StarterNames.Length)
        {
            Debug.LogWarning($"Moved {found} of the four starters to the start hub. The rest, named exactly " +
                             $"{string.Join(", ", StarterNames)}, weren't found in this scene.");
        }
    }

    // Ticks the four starters on the CharacterScript asset, and unticks everyone else.
    private void SetStartersOnly()
    {
        var guids = AssetDatabase.FindAssets("t:CharacterScriptables");

        if (guids.Length == 0)
        {
            Debug.LogWarning("No CharacterScript asset was found, so the characters found at the start weren't set.");
            return;
        }

        var asset = AssetDatabase.LoadAssetAtPath<CharacterScriptables>(AssetDatabase.GUIDToAssetPath(guids[0]));
        var serialized = new SerializedObject(asset);

        foreach (var field in typeof(CharacterScriptables).GetFields().Where(f => f.FieldType == typeof(bool)))
        {
            var property = serialized.FindProperty(field.Name);

            if (property != null)
            {
                property.boolValue = StarterNames.Any(s => s.ToLower() == field.Name.ToLower());
            }
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
    }
}
