using Characters;
using UnityEngine;

// Looks after the cameras. By default the GroupCamera does the work: it shares views between players whose characters are close
// together, and gives everyone else their own. The older cameras (one for each number of players) are still here, and
// are used instead if Use Group Camera is turned off, so the two can be compared.
[RequireComponent(typeof(SinglePlayerCamera), typeof(TwoPlayerCamera))]
[RequireComponent(typeof(ThreePlayerCamera), typeof(FourPlayerCamera))]
public class CameraManager : MonoBehaviour
{
    [Tooltip("How often, in seconds, the older cameras look again to see how many players there are.")]
    public float characterReacquireTime;
    private float lastReacquireTime;
    [Tooltip("How fast the views change size and place as players come together and split apart, as a fraction of the screen each frame.")]
    public float cameraViewSwitchSpeed;

    [Tooltip("Use the new camera, which shares a view between any players whose characters are close together. Turn off to use the older camera for however many players there are.")]
    public bool useGroupCamera = true;

    [Range(1f, 4f)]
    [Tooltip("How the look stick's push is turned into how far the view looks. 1 is straight across: half way out looks half the distance. Higher is more gradual: with 2, half way out looks a quarter of the distance, so it's easy to look just a little way, and the far edge is only reached at the end of the stick. It works the same for every camera.")]
    public float lookCurve = 2f;

    [Tooltip("How many players there are, as the game runs: for watching, not for setting.")]
    public int numPlayers;

    private SinglePlayerCamera singlePlayerCamera;
    private TwoPlayerCamera twoPlayerCamera;
    private ThreePlayerCamera threePlayerCamera;
    private FourPlayerCamera fourPlayerCamera;
    private GroupCamera groupCamera;

    private static CameraManager instance;

    [Tooltip("The four cameras, one for each possible screen. Every one has to be set.")]
    public Camera[] cameras;

    public static Rect rectFullScreen;
    public static Rect rectHorizontalTop;
    public static Rect rectHorizontalBottom;
    public static Rect rectVerticalLeft;
    public static Rect rectVerticalRight;

    public static Rect rectFourWayTopLeft;
    public static Rect rectFourWayTopRight;
    public static Rect rectFourWayBottomLeft;
    public static Rect rectFourWayBottomRight;

    // How fast the views change size and place as players come together and split apart, as a fraction of the screen per
    // frame at 60 frames a second.
    public static float ViewSwitchSpeed => instance ? instance.cameraViewSwitchSpeed : 0.01f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Before anything that asks for the cameras.
        instance = this;

        singlePlayerCamera = GetComponent<SinglePlayerCamera>();
        twoPlayerCamera = GetComponent<TwoPlayerCamera>();
        threePlayerCamera = GetComponent<ThreePlayerCamera>();
        fourPlayerCamera = GetComponent<FourPlayerCamera>();

        // One put on this object by hand keeps the settings it was given. Without one, it's added with its defaults.
        groupCamera = GetComponent<GroupCamera>();
        if (!groupCamera && useGroupCamera)
        {
            groupCamera = gameObject.AddComponent<GroupCamera>();
        }

        numPlayers = PlayerManager.GetPlayerCount();

        foreach (var cam in cameras)
        {
            if (!cam)
            {
                print("Camera Manager Cameras Not Set");
            }
        }

        rectFullScreen = new Rect(0, 0, 1, 1);

        // Left and right halves
        rectHorizontalTop = new Rect(0, 0, 0.5f, 1);
        rectHorizontalBottom = new Rect(0.5f, 0, 0.5f, 1);

        // Top and bottom halves
        rectVerticalLeft = new Rect(0, 0.5f, 1, 0.5f);
        rectVerticalRight = new Rect(0, 0, 1, 0.5f);

        rectFourWayTopLeft = new Rect(0, 0.5f, 0.5f, 0.5f);
        rectFourWayTopRight = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
        rectFourWayBottomLeft = new Rect(0, 0, 0.5f, 0.5f);
        rectFourWayBottomRight = new Rect(0.5f, 0, 0.5f, 0.5f);
    }

    // Whether the group camera is the one in charge right now.
    private static bool GroupActive()
    {
        return instance && instance.useGroupCamera && instance.groupCamera;
    }

    public static bool IsGroupCameraActive
    {
        get { return GroupActive(); }
    }

    // Whether this character is pinned to a screen of its own. Only the group camera pins characters.
    public static bool IsPinned(Character character)
    {
        return GroupActive() && instance.groupCamera.IsPinned(character);
    }

    // Update is called once per frame
    void Update()
    {
        if (useGroupCamera && groupCamera)
        {
            groupCamera.enabled = true;

            singlePlayerCamera.Active(false);
            twoPlayerCamera.Active(false);
            threePlayerCamera.Active(false);
            fourPlayerCamera.Active(false);

            return;
        }

        if (groupCamera)
        {
            groupCamera.enabled = false;
        }

        if (Time.time - lastReacquireTime >= characterReacquireTime)
        {
            lastReacquireTime = Time.time;
            numPlayers = PlayerManager.GetPlayerCount();
        }

        switch (numPlayers)
        {
            case 1:
                singlePlayerCamera.Active(true);
                twoPlayerCamera.Active(false);
                threePlayerCamera.Active(false);
                fourPlayerCamera.Active(false);

                break;
            case 2:
                singlePlayerCamera.Active(false);
                twoPlayerCamera.Active(true);
                threePlayerCamera.Active(false);
                fourPlayerCamera.Active(false);

                break;
            case 3:
                singlePlayerCamera.Active(false);
                twoPlayerCamera.Active(false);
                threePlayerCamera.Active(true);
                fourPlayerCamera.Active(false);

                break;
            case 4:
                singlePlayerCamera.Active(false);
                twoPlayerCamera.Active(false);
                threePlayerCamera.Active(false);
                fourPlayerCamera.Active(true);

                break;
        }
    }

    // How hard the stick is pushed, from 0 (centered) to 1 (all the way out), turned into how far to look, from 0 to 1, along the way
    // it's pushed. The distance grows slower than the push, by Look Curve, so a small push looks only a little way, and the full distance
    // is only reached at the very end of the stick.
    private static Vector2 ShapeLook(Vector2 stick)
    {
        var push = stick.magnitude;

        if (push <= 0f || !instance || instance.lookCurve <= 1f)
        {
            return stick;
        }

        return stick / push * Mathf.Pow(Mathf.Min(push, 1f), instance.lookCurve);
    }

    public static void Look(Player player, Vector2 lookValue)
    {
        if (!instance)
        {
            return;
        }

        lookValue = ShapeLook(lookValue);

        if (GroupActive())
        {
            instance.groupCamera.Look(player, lookValue);
            return;
        }

        switch (instance.numPlayers)
        {
            case 1:
                instance.singlePlayerCamera.Look(lookValue);
                break;
            case 2:
                instance.twoPlayerCamera.Look(player, lookValue);
                break;
            case 3:
                instance.threePlayerCamera.Look(player, lookValue);
                break;
            case 4:
                instance.fourPlayerCamera.Look(player, lookValue);
                break;
        }
    }

    public static PinResult Pin(Player owner, Character character)
    {
        return GroupActive() ? instance.groupCamera.Pin(owner, character) : PinResult.Invalid;
    }

    public static UnpinResult Unpin(Player requester, Character character)
    {
        return GroupActive() ? instance.groupCamera.Unpin(requester, character) : UnpinResult.NotPinned;
    }

    public static Player PinOwner(Character character)
    {
        return GroupActive() ? instance.groupCamera.PinOwner(character) : null;
    }

    public static void SetPinChoice(Player player, Character candidate)
    {
        if (GroupActive())
        {
            instance.groupCamera.SetPinChoice(player, candidate);
        }
    }

    // The older cameras' lock, which is a different thing and doesn't take the player off their character. The group camera pins
    // through Pin and Unpin instead.
    public static void Lock(Player player)
    {
        if (!instance || GroupActive())
        {
            return;
        }

        switch (instance.numPlayers)
        {
            case 1:
                instance.singlePlayerCamera.Lock();
                break;
            case 2:
                instance.twoPlayerCamera.Lock(player);
                break;
            case 3:
                instance.threePlayerCamera.Lock(player);
                break;
            case 4:
                instance.fourPlayerCamera.Lock(player);
                break;
        }
    }

    public static void Zoom(Player player, int zoomDir)
    {
        if (zoomDir == 0)
        {
            return;
        }

        if (zoomDir < 0)
        {
            zoomDir = -1;
        }
        else
        {
            zoomDir = 1;
        }

        if (!instance)
        {
            return;
        }

        if (GroupActive())
        {
            instance.groupCamera.Zoom(player, zoomDir);
            return;
        }

        switch (instance.numPlayers)
        {
            case 1:
                instance.singlePlayerCamera.Zoom(zoomDir);
                break;
            case 2:
                instance.twoPlayerCamera.Zoom(player, zoomDir);
                break;
            case 3:
                instance.threePlayerCamera.Zoom(player, zoomDir);
                break;
            case 4:
                instance.fourPlayerCamera.Zoom(player, zoomDir);
                break;
        }

    }

    public static Camera[] GetCameras()
    {
        return instance.cameras;
    }

    // The camera showing a player's view: their own, or the one they share with the players near them. Null if there's no
    // camera yet.
    public static Camera GetCameraForPlayer(Player player)
    {
        if (!instance || instance.cameras == null || instance.cameras.Length == 0)
        {
            return null;
        }

        if (GroupActive())
        {
            var shared = instance.groupCamera.GetCameraFor(player);

            if (shared)
            {
                return shared;
            }
        }

        var index = player ? Mathf.Clamp(player.playerNum - 1, 0, instance.cameras.Length - 1) : 0;
        var camera = instance.cameras[index];

        return camera && camera.enabled ? camera : instance.cameras[0];
    }

    public static void SetRect(Camera cam, Rect dest)
    {

        var cur = cam.rect;
        if (cur.x < dest.x)
        {
            cur.x += instance.cameraViewSwitchSpeed;
        }
        else if (cur.x > dest.x)
        {
            cur.x -= instance.cameraViewSwitchSpeed;
        }

        if (Mathf.Abs(cur.x - dest.x) < instance.cameraViewSwitchSpeed)
        {
            cur.x = dest.x;
        }

        if (cur.y < dest.y)
        {
            cur.y += instance.cameraViewSwitchSpeed;
        }
        else if (cur.y > dest.y)
        {
            cur.y -= instance.cameraViewSwitchSpeed;
        }

        if (Mathf.Abs(cur.y - dest.y) < instance.cameraViewSwitchSpeed)
        {
            cur.y = dest.y;
        }

        if (cur.width < dest.width)
        {
            cur.width += instance.cameraViewSwitchSpeed;
        }
        else if (cur.width > dest.width)
        {
            cur.width -= instance.cameraViewSwitchSpeed;
        }

        if (Mathf.Abs(cur.width - dest.width) < instance.cameraViewSwitchSpeed)
        {
            cur.width = dest.width;
        }

        if (cur.height < dest.height)
        {
            cur.height += instance.cameraViewSwitchSpeed;
        }
        else if (cur.height > dest.height)
        {
            cur.height -= instance.cameraViewSwitchSpeed;
        }

        if (Mathf.Abs(cur.height - dest.height) < instance.cameraViewSwitchSpeed)
        {
            cur.height = dest.height;
        }

        cam.rect = cur;
    }
}
