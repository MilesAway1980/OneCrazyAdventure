using System;
using System.Collections.Generic;
using Characters;
using UnityEngine;

// What came of pinning a character to a screen.
public enum PinResult
{
    Pinned,
    AlreadyPinned,
    NoScreenToSpare,
    Invalid
}

// What came of unpinning one.
public enum UnpinResult
{
    Removed,
    NotPinned,
    NotYours
}

// The camera for every number of players. It shows the game on up to four screens, and decides what goes on each one:
//
//  * Players whose characters are close enough to fit in one view share a screen, which zooms out to keep all of them in it.
//    Players who are far from the others have a screen to themselves. So four players might have one screen, or two, or
//    four, depending on where their characters are, and it changes as they move about.
//  * A player can pin their character to a screen of its own, and it stays there however they move. There are four screens
//    in all, so what's left after one for each player is what there is to pin with: a single player can pin three, two
//    players can pin two, three players one, and four players none.
//
// It only knows about the players on this machine. Players on other machines have no Player object here, so they are never
// given a screen, and their characters are only seen when they're in view.
//
// The four Camera objects belong to the CameraManager. A screen keeps the same camera for as long as it exists, which is
// what lets a view slide apart into two, or two slide together into one, rather than jump.
public class GroupCamera : MonoBehaviour
{
    [Serializable]
    public struct ZoomRange
    {
        public float min;
        public float start;
        public float max;

        public ZoomRange(float min, float start, float max)
        {
            this.min = min;
            this.start = start;
            this.max = max;
        }
    }

    [Header("Following")]
    [Tooltip("How fast, in units a second, the camera moves to a new character or to a new group of characters.")]
    public float transitionSpeed = 60f;
    [Tooltip("How fast the camera keeps up once it's there.")]
    public float followSpeed = 1000f;
    [Tooltip("How far the look stick moves the camera, at the widest zoom.")]
    public float lookDist = 15f;

    [Header("Zoom")]
    [Tooltip("How much the zoom buttons change the zoom each frame at 60 frames a second.")]
    public float zoomSpeed = 0.1f;
    [Tooltip("How fast, in units a second, the view zooms in or out to its new size.")]
    public float zoomChangeSpeed = 20f;
    [Tooltip("The closest, the starting and the widest zoom when there's one screen.")]
    public ZoomRange fullScreenZoom = new ZoomRange(6f, 8f, 14f);
    [Tooltip("The same, when there are two screens, one above the other.")]
    public ZoomRange stackedZoom = new ZoomRange(6f, 8f, 18f);
    [Tooltip("The same, when there are two screens side by side.")]
    public ZoomRange sideBySideZoom = new ZoomRange(4f, 6f, 8f);
    [Tooltip("The same, when there are three or four screens.")]
    public ZoomRange quarterZoom = new ZoomRange(6f, 8f, 14f);

    [Header("Sharing a view")]
    [Tooltip("Players share a screen while the zoom needed to keep all their characters in view is no more than this. Higher shares a screen over greater distances.")]
    public float shareZoom = 10f;
    [Tooltip("Players that already share a screen stay together until the zoom needed is this much more than Share Zoom, as a fraction of it. Stops two players walking at the edge flicking between one screen and two.")]
    [Range(0f, 0.5f)]
    public float splitHysteresis = 0.15f;
    [Tooltip("The space, in units, kept round the characters in a shared view.")]
    public float fitPadding = 3f;

    [Header("Layout")]
    [Tooltip("With two screens: side by side if ticked, one above the other if not.")]
    public bool horizontalSplit;

    [Header("Pins")]
    [Tooltip("Shows a pin above each character that is pinned to a screen of its own, so it can be told from the others.")]
    public bool showPinMarkers = true;
    [Tooltip("How big the pin is, as a multiple of its normal size.")]
    [Min(0.1f)]
    public float pinMarkerScale = 0.8f;

    [Header("Labels")]
    [Tooltip("Shows, in the top right of each screen, which player's view it is, for a few seconds whenever the screens change. A pinned screen always says Pinned.")]
    public bool showViewLabels = true;
    [Tooltip("How long, in seconds, a label stays after its screen changes.")]
    [Min(0.5f)]
    public float labelSeconds = 3f;

    [Header("Debugging")]
    [Tooltip("Shows on the screen how the players are being grouped and why, and logs it each time the number of screens changes.")]
    public bool showDebug;

    private enum Layout
    {
        Full,
        Stacked,
        SideBySide,
        Quarters
    }

    private struct Entry
    {
        public Player player;
        public Character character;
    }

    // One screen: either the players who share it, or a character pinned to it.
    private class View
    {
        public int camera = -1;

        // For a view of players
        public readonly List<int> playerIds = new List<int>();
        public readonly List<Character> characters = new List<Character>();
        public int signature;

        // For a pinned view
        public Character pinned;
        public Vector2 pinnedLook;
        public int pinnedBy;

        public float preferredZoom;
        public bool following;
        public int followedSignature;

        // A view that has only just been given a camera starts where the view it came from was, and so grows out of it.
        public View parent;
        public bool fresh;

        public Rect slot;
    }

    private struct CameraPose
    {
        public Vector3 position;
        public float size;
        public Rect rect;
    }

    private static readonly Rect rectFull = new Rect(0, 0, 1, 1);
    private static readonly Rect rectTop = new Rect(0, 0.5f, 1, 0.5f);
    private static readonly Rect rectBottom = new Rect(0, 0, 1, 0.5f);
    private static readonly Rect rectLeft = new Rect(0, 0, 0.5f, 1);
    private static readonly Rect rectRight = new Rect(0.5f, 0, 0.5f, 1);
    private static readonly Rect rectTopLeft = new Rect(0, 0.5f, 0.5f, 0.5f);
    private static readonly Rect rectTopRight = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
    private static readonly Rect rectBottomLeft = new Rect(0, 0, 0.5f, 0.5f);
    private static readonly Rect rectBottomRight = new Rect(0.5f, 0, 0.5f, 0.5f);

    private const int MaxScreens = 4;

    private Camera[] cams;

    // The views of players, as of the last frame, and the pinned views.
    private List<View> groupViews = new List<View>();
    private readonly List<View> pins = new List<View>();

    // The character each player is choosing to pin, while they hold the pin button, and the arrow drawn over it.
    private readonly Dictionary<int, Character> pinChoices = new Dictionary<int, Character>();
    private readonly Dictionary<int, PinMarker> choiceMarkers = new Dictionary<int, PinMarker>();
    private readonly List<int> staleChoices = new List<int>();
    private GUIStyle choiceStyle;

    // The pin drawn above each pinned character.
    private readonly Dictionary<Character, PinMarker> pinMarkers = new Dictionary<Character, PinMarker>();
    private readonly List<Character> staleMarkers = new List<Character>();

    // What each camera's screen says in its top right corner, what it was last time so a change can be seen, and when it goes.
    private readonly string[] labelText = new string[MaxScreens];
    private readonly string[] labelKey = new string[MaxScreens];
    private readonly bool[] labelPermanent = new bool[MaxScreens];
    private readonly float[] labelUntil = new float[MaxScreens];
    private GUIStyle labelStyle;
    private readonly List<int> labelNumbers = new List<int>();

    private readonly Dictionary<int, Vector2> looks = new Dictionary<int, Vector2>();
    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Vector2> points = new List<Vector2>();

    private ZoomRange currentRange;

    private readonly List<string> debugPairs = new List<string>();
    private string debugText = "";
    private string debugLastKey = "";

    void Start()
    {
        cams = CameraManager.GetCameras();
        currentRange = fullScreenZoom;

        if (cams == null || cams.Length < MaxScreens)
        {
            Debug.LogError("The group camera needs the Camera Manager to have four cameras.", this);
            enabled = false;
            return;
        }

        for (var i = 0; i < MaxScreens; i++)
        {
            if (!cams[i])
            {
                Debug.LogError($"The group camera needs the Camera Manager's camera {i + 1}, which is not set.", this);
                enabled = false;
                return;
            }
        }
    }

    // After everything has moved, so the view is of where the characters are this frame.
    void LateUpdate()
    {
        GatherEntries();
        DropBadPins();
        ShowPinMarkers();
        ShowChoiceMarkers();

        if (entries.Count == 0)
        {
            // Nobody has a character to show, such as for a moment at a hub. Leave things as they are.
            if (showDebug)
            {
                debugText = "No player has a character, so the cameras are left as they are.";
            }

            return;
        }

        var previousViewOf = new Dictionary<int, View>();
        foreach (var view in groupViews)
        {
            foreach (var id in view.playerIds)
            {
                previousViewOf[id] = view;
            }
        }

        var groups = BuildGroups(previousViewOf);
        var newGroupViews = AssignViews(groups, previousViewOf);

        var all = new List<View>(newGroupViews);
        all.AddRange(pins);

        if (all.Count > MaxScreens || !GiveCameras(all))
        {
            if (showDebug)
            {
                debugText = $"Could not give every screen a camera ({all.Count} screens), so the cameras are left as they are.";
            }

            return;
        }

        groupViews = newGroupViews;

        var layout = GetLayout(all.Count);
        var range = GetRange(layout);
        currentRange = range;

        for (var i = 0; i < all.Count; i++)
        {
            all[i].slot = GetSlot(layout, i);

            if (all[i].preferredZoom <= 0f)
            {
                all[i].preferredZoom = range.start;
            }

            all[i].preferredZoom = Mathf.Clamp(all[i].preferredZoom, range.min, range.max);
        }

        StartFreshViews(all);

        foreach (var view in all)
        {
            UpdateView(view, range);
        }

        UpdateUnusedCameras(all);
        UpdateViewLabels(all);

        if (showDebug)
        {
            UpdateDebugText(all, layout);
        }
    }

    // ----- Labels -----

    // Works out what each screen says. A screen's label comes up whenever who is on it changes, or the number of screens does,
    // and goes after a few seconds. A pinned screen says so for as long as it's pinned.
    private void UpdateViewLabels(List<View> all)
    {
        var shown = new bool[MaxScreens];

        foreach (var view in all)
        {
            var screen = view.camera;

            if (screen < 0 || screen >= MaxScreens)
            {
                continue;
            }

            shown[screen] = true;

            string key;
            string text;

            if (view.pinned)
            {
                key = $"pinned|{view.pinned.name}";
                text = $"Pinned: {view.pinned.name}";
            }
            else
            {
                labelNumbers.Clear();

                foreach (var id in view.playerIds)
                {
                    var player = Player.FindById(id);

                    if (player)
                    {
                        labelNumbers.Add(player.playerNum);
                    }
                }

                labelNumbers.Sort();
                text = labelNumbers.Count == 1
                    ? $"Player {labelNumbers[0]}"
                    : $"Players {string.Join(", ", labelNumbers)}";
                key = $"{all.Count}|{text}";
            }

            labelPermanent[screen] = view.pinned;

            if (key != labelKey[screen])
            {
                labelKey[screen] = key;
                labelText[screen] = text;
                labelUntil[screen] = Time.unscaledTime + labelSeconds;
            }
        }

        // A camera with no screen has nothing to say, and starts afresh when it's given one.
        for (var i = 0; i < MaxScreens; i++)
        {
            if (!shown[i])
            {
                labelKey[i] = null;
                labelText[i] = null;
            }
        }
    }

    private void DrawViewLabels()
    {
        if (!showViewLabels || cams == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
        }

        labelStyle.fontSize = Mathf.Clamp(Screen.height / 28, 14, 48);

        for (var i = 0; i < MaxScreens; i++)
        {
            if (string.IsNullOrEmpty(labelText[i]) || !cams[i] || !cams[i].enabled)
            {
                continue;
            }

            var remaining = labelUntil[i] - Time.unscaledTime;

            if (!labelPermanent[i] && remaining <= 0f)
            {
                continue;
            }

            // Fades out over its last half second.
            var alpha = labelPermanent[i] ? 1f : Mathf.Clamp01(remaining / 0.5f);

            // The camera's screen, turned to the top-down numbering the GUI uses, with a margin inside its top right corner.
            var pixels = cams[i].pixelRect;
            var margin = labelStyle.fontSize * 0.5f;
            var area = new Rect(pixels.xMin, Screen.height - pixels.yMax + margin, pixels.width - margin, labelStyle.fontSize * 2f);

            // A dark copy behind it, so it reads against any background.
            GUI.color = new Color(0f, 0f, 0f, 0.8f * alpha);
            GUI.Label(new Rect(area.x + 2f, area.y + 2f, area.width, area.height), labelText[i], labelStyle);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(area, labelText[i], labelStyle);
        }

        GUI.color = Color.white;
    }

    // ----- Debugging -----

    private void UpdateDebugText(List<View> all, Layout layout)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"{entries.Count} player(s) with a character, {pins.Count} pinned of {PinCapacity()} possible, {all.Count} screen(s), layout {layout}");

        foreach (var view in all)
        {
            var camera = cams[view.camera];
            var what = view.pinned
                ? $"pinned {view.pinned.name}"
                : $"players {string.Join(" ", view.playerIds)} (characters {string.Join(", ", view.characters.ConvertAll(c => c.name))})";

            text.AppendLine($"  camera {view.camera}: {what}, zoom {camera.orthographicSize:0.0} (wants {view.preferredZoom:0.0}), screen {camera.rect}");
        }

        text.AppendLine("Pairs of players (zoom needed to share, and the most allowed):");

        foreach (var pair in debugPairs)
        {
            text.AppendLine("  " + pair);
        }

        debugText = text.ToString();

        // Written to the Console only when the number of screens changes, so it can be copied from there.
        var key = $"{entries.Count}/{pins.Count}/{all.Count}";

        if (key != debugLastKey)
        {
            debugLastKey = key;
            Debug.Log("Group camera changed:\n" + debugText, this);
        }
    }

    void OnGUI()
    {
        DrawViewLabels();
        DrawPinChoices();

        if (!showDebug || string.IsNullOrEmpty(debugText))
        {
            return;
        }

        GUI.Box(new Rect(10, 10, 760, 20 + 16 * debugText.Split('\n').Length), debugText);
    }

    // ----- Who is on screen -----

    private void GatherEntries()
    {
        entries.Clear();

        if (PlayerManager.players == null)
        {
            return;
        }

        foreach (var player in PlayerManager.players)
        {
            if (!player)
            {
                continue;
            }

            var character = player.character;

            if (character && character.InGame)
            {
                entries.Add(new Entry { player = player, character = character });
            }
        }
    }

    private static int LocalPlayerCount()
    {
        var count = 0;

        if (PlayerManager.players != null)
        {
            foreach (var player in PlayerManager.players)
            {
                if (player)
                {
                    count++;
                }
            }
        }

        return Mathf.Max(1, count);
    }

    // How many characters can be pinned: what's left of the four screens after one for each player.
    private static int PinCapacity()
    {
        return Mathf.Max(0, MaxScreens - LocalPlayerCount());
    }

    void OnDisable()
    {
        ClearPinMarkers();
        pinChoices.Clear();
        ShowChoiceMarkers();
    }

    // Keeps an arrow over the character each player is choosing, and takes it away when they've done.
    private void ShowChoiceMarkers()
    {
        staleChoices.Clear();

        foreach (var pair in pinChoices)
        {
            if (!pair.Value || !Player.FindById(pair.Key))
            {
                staleChoices.Add(pair.Key);
            }
        }

        foreach (var id in staleChoices)
        {
            pinChoices.Remove(id);
        }

        foreach (var pair in pinChoices)
        {
            if (choiceMarkers.TryGetValue(pair.Key, out var marker) && marker)
            {
                marker.Retarget(pair.Value);
            }
            else
            {
                choiceMarkers[pair.Key] = PinMarker.Create(pair.Value, pinMarkerScale, 510, true);
            }
        }

        staleChoices.Clear();

        foreach (var pair in choiceMarkers)
        {
            if (!pinChoices.ContainsKey(pair.Key))
            {
                staleChoices.Add(pair.Key);
            }
        }

        foreach (var id in staleChoices)
        {
            if (choiceMarkers[id])
            {
                Destroy(choiceMarkers[id].gameObject);
            }

            choiceMarkers.Remove(id);
        }
    }

    // The player is holding the pin button and has the arrow on this character. Null when they let go.
    public void SetPinChoice(Player player, Character candidate)
    {
        if (!player)
        {
            return;
        }

        if (candidate)
        {
            pinChoices[player.id] = candidate;
        }
        else
        {
            pinChoices.Remove(player.id);
        }
    }

    // Tells each player who is choosing what the choice is, in their own screen.
    private void DrawPinChoices()
    {
        if (cams == null || pinChoices.Count == 0 || Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (choiceStyle == null)
        {
            choiceStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
        }

        choiceStyle.fontSize = Mathf.Clamp(Screen.height / 24, 16, 56);

        foreach (var pair in pinChoices)
        {
            var view = ViewOf(Player.FindById(pair.Key));

            if (view == null || view.camera < 0 || !cams[view.camera])
            {
                continue;
            }

            var text = GameText.Current;
            var line = GameText.Fill(IsPinned(pair.Value) ? text.pinChoiceUnpin : text.pinChoicePin, character: pair.Value.name);

            var pixels = cams[view.camera].pixelRect;
            var area = new Rect(pixels.xMin, Screen.height - pixels.yMax + choiceStyle.fontSize * 2.5f, pixels.width, choiceStyle.fontSize * 2f);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(area.x + 2f, area.y + 2f, area.width, area.height), line, choiceStyle);

            GUI.color = new Color(1f, 0.9f, 0.3f, 1f);
            GUI.Label(area, line, choiceStyle);
        }

        GUI.color = Color.white;
    }

    // Keeps a pin above each pinned character, and takes it away when they're no longer pinned.
    private void ShowPinMarkers()
    {
        if (!showPinMarkers)
        {
            ClearPinMarkers();
            return;
        }

        foreach (var pin in pins)
        {
            if (!pin.pinned)
            {
                continue;
            }

            // A marker that has been destroyed counts as none, so it's made again.
            if (!pinMarkers.TryGetValue(pin.pinned, out var marker) || !marker)
            {
                pinMarkers[pin.pinned] = PinMarker.Create(pin.pinned, pinMarkerScale, 500);
            }
        }

        staleMarkers.Clear();

        foreach (var entry in pinMarkers)
        {
            if (!entry.Key || !pins.Exists(p => p.pinned == entry.Key))
            {
                staleMarkers.Add(entry.Key);
            }
        }

        foreach (var character in staleMarkers)
        {
            if (pinMarkers[character])
            {
                Destroy(pinMarkers[character].gameObject);
            }

            pinMarkers.Remove(character);
        }
    }

    private void ClearPinMarkers()
    {
        foreach (var marker in pinMarkers.Values)
        {
            if (marker)
            {
                Destroy(marker.gameObject);
            }
        }

        pinMarkers.Clear();
    }

    // A pinned character that's been put away or has gone has nothing to show. If a player joins and takes a screen, the
    // oldest pins give way.
    private void DropBadPins()
    {
        pins.RemoveAll(p => !p.pinned || !p.pinned.InGame);

        var capacity = PinCapacity();
        while (pins.Count > capacity)
        {
            pins.RemoveAt(0);
        }
    }

    // ----- Sharing views -----

    // Works out which players share a screen. Starting with everybody on their own, the two groups that would fit in the
    // smallest view are joined, over and over, until no two groups would fit in one view together.
    private List<List<Entry>> BuildGroups(Dictionary<int, View> previousViewOf)
    {
        var groups = new List<List<Entry>>();
        foreach (var entry in entries)
        {
            groups.Add(new List<Entry> { entry });
        }

        var aspect = FullAspect();
        var firstPass = true;
        debugPairs.Clear();

        while (groups.Count > 1)
        {
            var bestA = -1;
            var bestB = -1;
            var bestNeeded = float.MaxValue;

            for (var a = 0; a < groups.Count; a++)
            {
                for (var b = a + 1; b < groups.Count; b++)
                {
                    var needed = ZoomNeededToShare(groups[a], groups[b], aspect);

                    // Players already sharing a screen are allowed a little further apart before they're split up.
                    var limit = WereTogether(groups[a], groups[b], previousViewOf)
                        ? shareZoom * (1f + splitHysteresis)
                        : shareZoom;

                    if (showDebug && firstPass)
                    {
                        debugPairs.Add($"{groups[a][0].character.name} (player {groups[a][0].player.playerNum}) and {groups[b][0].character.name} (player {groups[b][0].player.playerNum}): needs {needed:0.0}, allowed {limit:0.0}, so {(needed <= limit ? "share" : "apart")}");
                    }

                    if (needed <= limit && needed < bestNeeded)
                    {
                        bestA = a;
                        bestB = b;
                        bestNeeded = needed;
                    }
                }
            }

            firstPass = false;

            if (bestA < 0)
            {
                break;
            }

            groups[bestA].AddRange(groups[bestB]);
            groups.RemoveAt(bestB);
        }

        // Players in the order they joined, and screens in the order of their first player.
        foreach (var group in groups)
        {
            group.Sort((x, y) => x.player.playerNum.CompareTo(y.player.playerNum));
        }

        groups.Sort((x, y) => x[0].player.playerNum.CompareTo(y[0].player.playerNum));

        return groups;
    }

    private static bool WereTogether(List<Entry> a, List<Entry> b, Dictionary<int, View> previousViewOf)
    {
        foreach (var first in a)
        {
            if (!previousViewOf.TryGetValue(first.player.id, out var firstView))
            {
                continue;
            }

            foreach (var second in b)
            {
                if (previousViewOf.TryGetValue(second.player.id, out var secondView) && secondView == firstView)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float ZoomNeededToShare(List<Entry> a, List<Entry> b, float aspect)
    {
        points.Clear();

        foreach (var entry in a)
        {
            points.Add(entry.character.transform.position);
        }

        foreach (var entry in b)
        {
            points.Add(entry.character.transform.position);
        }

        return ZoomNeeded(BoundsCenter(points), points, aspect);
    }

    // The zoom (half the height of the view) at which every point is in view, centred on the centre, with room to spare.
    private float ZoomNeeded(Vector2 center, List<Vector2> pointList, float aspect)
    {
        var needed = 0f;

        foreach (var point in pointList)
        {
            needed = Mathf.Max(needed, Mathf.Abs(point.y - center.y), Mathf.Abs(point.x - center.x) / aspect);
        }

        return needed + fitPadding;
    }

    private static Vector2 BoundsCenter(List<Vector2> pointList)
    {
        var min = pointList[0];
        var max = pointList[0];

        foreach (var point in pointList)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return (min + max) * 0.5f;
    }

    // Gives each group of players a view. A group keeps the view its players were in, so its camera carries on smoothly. If a
    // view splits in two, one half keeps it and the other gets a new view that grows out of it. If views join, the
    // joined view carries on from whichever had most of its players, and takes the widest zoom of them.
    private List<View> AssignViews(List<List<Entry>> groups, Dictionary<int, View> previousViewOf)
    {
        var result = new List<View>();
        var claimed = new HashSet<View>();

        foreach (var group in groups)
        {
            var votes = new Dictionary<View, int>();

            foreach (var entry in group)
            {
                if (previousViewOf.TryGetValue(entry.player.id, out var previous))
                {
                    votes.TryGetValue(previous, out var count);
                    votes[previous] = count + 1;
                }
            }

            View winner = null;
            var winnerVotes = 0;
            View mostVoted = null;
            var mostVotes = 0;

            foreach (var pair in votes)
            {
                if (pair.Value > mostVotes || (pair.Value == mostVotes && pair.Key.camera < mostVoted.camera))
                {
                    mostVoted = pair.Key;
                    mostVotes = pair.Value;
                }

                if (claimed.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value > winnerVotes || (pair.Value == winnerVotes && pair.Key.camera < winner.camera))
                {
                    winner = pair.Key;
                    winnerVotes = pair.Value;
                }
            }

            View view;

            if (winner != null)
            {
                view = winner;
                claimed.Add(winner);

                foreach (var pair in votes)
                {
                    if (pair.Key != winner)
                    {
                        view.preferredZoom = Mathf.Max(view.preferredZoom, pair.Key.preferredZoom);
                    }
                }
            }
            else
            {
                view = new View
                {
                    parent = mostVoted,
                    preferredZoom = mostVoted != null ? mostVoted.preferredZoom : 0f
                };
            }

            view.playerIds.Clear();
            view.characters.Clear();

            var signature = 17;
            foreach (var entry in group)
            {
                view.playerIds.Add(entry.player.id);
                view.characters.Add(entry.character);
                signature = signature * 31 + entry.character.GetHashCode();
            }

            view.signature = signature;
            result.Add(view);
        }

        return result;
    }

    // Gives a camera to every view that hasn't got one, taking one nobody is using. False if there's somehow none.
    private bool GiveCameras(List<View> all)
    {
        var used = new bool[MaxScreens];

        foreach (var view in all)
        {
            if (view.camera >= 0)
            {
                used[view.camera] = true;
            }
        }

        foreach (var view in all)
        {
            if (view.camera >= 0)
            {
                continue;
            }

            var free = Array.IndexOf(used, false);

            if (free < 0)
            {
                Debug.LogError("The group camera has run out of cameras.", this);
                return false;
            }

            view.camera = free;
            view.fresh = true;
            used[free] = true;
        }

        return true;
    }

    // ----- Layout -----

    private Layout GetLayout(int screens)
    {
        if (screens <= 1)
        {
            return Layout.Full;
        }

        if (screens == 2)
        {
            return horizontalSplit ? Layout.SideBySide : Layout.Stacked;
        }

        return Layout.Quarters;
    }

    private ZoomRange GetRange(Layout layout)
    {
        switch (layout)
        {
            case Layout.Stacked:
                return stackedZoom;
            case Layout.SideBySide:
                return sideBySideZoom;
            case Layout.Quarters:
                return quarterZoom;
            default:
                return fullScreenZoom;
        }
    }

    // The part of the screen the view in this position goes in. Three screens use the first three quarters, and the
    // fourth is left blank.
    private static Rect GetSlot(Layout layout, int index)
    {
        switch (layout)
        {
            case Layout.Stacked:
                return index == 0 ? rectTop : rectBottom;
            case Layout.SideBySide:
                return index == 0 ? rectLeft : rectRight;
            case Layout.Quarters:
                switch (index)
                {
                    case 0: return rectTopLeft;
                    case 1: return rectTopRight;
                    case 2: return rectBottomLeft;
                    default: return rectBottomRight;
                }
            default:
                return rectFull;
        }
    }

    private static float FullAspect()
    {
        return Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
    }

    private static float SlotAspect(Rect slot)
    {
        if (Screen.height <= 0 || slot.height <= 0f)
        {
            return 16f / 9f;
        }

        return slot.width * Screen.width / (slot.height * Screen.height);
    }

    // ----- Moving the cameras -----

    // A view that's new starts as a copy of the one it grew out of, so a screen splits apart from it rather than appearing.
    // Every copy is taken before any camera is changed, in case two views start from the same one.
    private void StartFreshViews(List<View> all)
    {
        var poses = new List<KeyValuePair<View, CameraPose>>();

        foreach (var view in all)
        {
            if (!view.fresh || view.parent == null || view.parent.camera < 0)
            {
                continue;
            }

            var parentCamera = cams[view.parent.camera];

            poses.Add(new KeyValuePair<View, CameraPose>(view, new CameraPose
            {
                position = parentCamera.transform.position,
                size = parentCamera.orthographicSize,
                rect = parentCamera.rect
            }));
        }

        foreach (var pair in poses)
        {
            var camera = cams[pair.Key.camera];
            camera.transform.position = pair.Value.position;
            camera.orthographicSize = pair.Value.size;
            camera.rect = pair.Value.rect;
            pair.Key.following = false;
        }
    }

    private void UpdateView(View view, ZoomRange range)
    {
        var camera = cams[view.camera];
        var zoomScale = view.preferredZoom / range.max;

        Vector2 targetPosition;
        float targetZoom = view.preferredZoom;

        if (view.pinned)
        {
            targetPosition = (Vector2)view.pinned.transform.position + view.pinnedLook * lookDist * zoomScale;
        }
        else
        {
            points.Clear();
            foreach (var character in view.characters)
            {
                points.Add(character.transform.position);
            }

            var center = BoundsCenter(points);
            targetPosition = center + GetLook(view) * lookDist * zoomScale;

            // A view of several characters zooms out as far as it takes to keep them all in it, whatever the player had it set
            // to, and never in far enough to cut one off. The look stick moves the view without changing this.
            if (points.Count > 1)
            {
                targetZoom = Mathf.Max(targetZoom, ZoomNeeded(center, points, SlotAspect(view.slot)));
            }
        }

        var cameraTransform = camera.transform;

        if (view.fresh)
        {
            view.fresh = false;

            if (view.parent == null || view.parent.camera < 0)
            {
                // The first time anything is shown: straight there.
                cameraTransform.position = new Vector3(targetPosition.x, targetPosition.y, cameraTransform.position.z);
                camera.orthographicSize = targetZoom;
                camera.rect = view.slot;
                view.following = true;
            }

            view.followedSignature = view.signature;
            view.parent = null;
        }

        // Moves to a different set of characters at the slower speed, so the view pans across rather than snapping, then
        // keeps up closely once it's arrived.
        if (view.signature != view.followedSignature)
        {
            view.followedSignature = view.signature;
            view.following = false;
        }

        var position = (Vector2)cameraTransform.position;

        if (position != targetPosition)
        {
            var speed = view.following ? followSpeed : transitionSpeed;
            position = Vector2.MoveTowards(position, targetPosition, speed * Time.deltaTime);
            cameraTransform.position = new Vector3(position.x, position.y, cameraTransform.position.z);
        }
        else
        {
            view.following = true;
        }

        camera.orthographicSize = Mathf.MoveTowards(camera.orthographicSize, targetZoom, zoomChangeSpeed * Time.deltaTime);
        camera.rect = MoveRect(camera.rect, view.slot);
        camera.transform.eulerAngles = Vector3.zero;
        camera.enabled = true;
    }

    // Cameras with no view are switched off. If there are three screens, the fourth quarter has a camera left turned
    // around to look at nothing, so it shows the background rather than whatever was last drawn there.
    private void UpdateUnusedCameras(List<View> all)
    {
        var used = new bool[MaxScreens];
        foreach (var view in all)
        {
            used[view.camera] = true;
        }

        var blankWanted = all.Count == 3;

        for (var i = 0; i < MaxScreens; i++)
        {
            if (used[i])
            {
                continue;
            }

            if (blankWanted)
            {
                blankWanted = false;
                cams[i].rect = rectBottomRight;
                cams[i].transform.eulerAngles = new Vector3(0, 180, 0);
                cams[i].enabled = true;
            }
            else
            {
                cams[i].enabled = false;
            }
        }
    }

    // Moves a view's place on the screen towards where it should be. It takes the same time whatever the frame rate.
    private static Rect MoveRect(Rect current, Rect target)
    {
        var step = CameraManager.ViewSwitchSpeed * 60f * Time.deltaTime;

        return new Rect(
            Mathf.MoveTowards(current.x, target.x, step),
            Mathf.MoveTowards(current.y, target.y, step),
            Mathf.MoveTowards(current.width, target.width, step),
            Mathf.MoveTowards(current.height, target.height, step)
        );
    }

    // The strongest push of the look stick by any of the players sharing the view.
    private Vector2 GetLook(View view)
    {
        var strongest = Vector2.zero;

        foreach (var id in view.playerIds)
        {
            if (looks.TryGetValue(id, out var look) && look.sqrMagnitude > strongest.sqrMagnitude)
            {
                strongest = look;
            }
        }

        return strongest;
    }

    // ----- What players can do -----

    public void Look(Player player, Vector2 lookValue)
    {
        if (player)
        {
            looks[player.id] = lookValue;
        }
    }

    // Any player sharing a view can zoom it. Zooming in stops where everyone would still be in it.
    public void Zoom(Player player, int zoomDir)
    {
        var view = ViewOf(player);

        if (view == null)
        {
            return;
        }

        var change = zoomDir * zoomSpeed * 60f * Time.deltaTime;
        view.preferredZoom = Mathf.Clamp(view.preferredZoom + change, currentRange.min, currentRange.max);
    }

    // Whether this character is pinned to a screen of its own.
    public bool IsPinned(Character character)
    {
        return character && pins.Exists(p => p.pinned == character);
    }

    // The player who pinned this character, or null if it isn't pinned, or they've left.
    public Player PinOwner(Character character)
    {
        var pin = pins.Find(p => p.pinned == character);
        return pin != null ? Player.FindById(pin.pinnedBy) : null;
    }

    // Pins a character to a screen of its own, for this player. It doesn't have to be one they're controlling, or anyone is: a
    // pin is only a view, and stays on the character whoever controls it.
    public PinResult Pin(Player owner, Character character)
    {
        if (!owner || !character || !character.InGame)
        {
            return PinResult.Invalid;
        }

        if (IsPinned(character))
        {
            return PinResult.AlreadyPinned;
        }

        if (pins.Count >= PinCapacity())
        {
            return PinResult.NoScreenToSpare;
        }

        var from = ViewOf(owner);

        pins.Add(new View
        {
            pinned = character,
            pinnedBy = owner.id,
            pinnedLook = looks.TryGetValue(owner.id, out var look) ? look : Vector2.zero,
            preferredZoom = from != null && from.camera >= 0 ? cams[from.camera].orthographicSize : 0f,
            parent = from
        });

        return PinResult.Pinned;
    }

    // Takes the pin off a character. A pin belongs to the player who made it, so only they can, unless they've gone.
    public UnpinResult Unpin(Player requester, Character character)
    {
        var existing = pins.Find(p => p.pinned == character);

        if (existing == null)
        {
            return UnpinResult.NotPinned;
        }

        if (requester && existing.pinnedBy != requester.id && Player.FindById(existing.pinnedBy))
        {
            return UnpinResult.NotYours;
        }

        pins.Remove(existing);
        return UnpinResult.Removed;
    }

    // The camera showing this player's view, which they might share with others. Null if they have none yet.
    public Camera GetCameraFor(Player player)
    {
        var view = ViewOf(player);

        return view != null && view.camera >= 0 && cams != null ? cams[view.camera] : null;
    }

    private View ViewOf(Player player)
    {
        if (!player)
        {
            return null;
        }

        foreach (var view in groupViews)
        {
            if (view.playerIds.Contains(player.id))
            {
                return view;
            }
        }

        return null;
    }
}
