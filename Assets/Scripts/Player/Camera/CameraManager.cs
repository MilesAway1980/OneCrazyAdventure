using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public float characterReacquireTime;
    private float lastReacquireTime;
    public float cameraViewSwitchSpeed;

    public int numPlayers;

    private SinglePlayerCamera singlePlayerCamera;
    private TwoPlayerCamera twoPlayerCamera;
    private ThreePlayerCamera threePlayerCamera;
    private FourPlayerCamera fourPlayerCamera;

    private static CameraManager instance;

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
    
    //horizontalRectSize[0] = new Rect(0, 0, 0.5f, 1);
    //horizontalRectSize[1] = new Rect(0.5f, 0, 0.5f, 1);

    //verticalRectSize[0] = new Rect(0, 0.5f, 1, 0.5f);
    //verticalRectSize[1] = new Rect(0, 0, 1, 0.5f);
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        singlePlayerCamera = GetComponent<SinglePlayerCamera>();
        twoPlayerCamera = GetComponent<TwoPlayerCamera>();
        threePlayerCamera = GetComponent<ThreePlayerCamera>();
        fourPlayerCamera = GetComponent<FourPlayerCamera>();
        
        numPlayers = PlayerManager.GetPlayerCount();
        instance = this;

        foreach (var cam in cameras)
        {
            if (!cam)
            {
                print("Camera Manager Cameras Not Set");
            }
        }
        
        rectFullScreen = new Rect(0, 0, 1, 1);

        rectHorizontalTop = new Rect(0, 0, 0.5f, 1);
        rectHorizontalBottom = new Rect(0.5f, 0, 0.5f, 1);
        
        rectVerticalLeft = new Rect(0, 0.5f, 1, 0.5f);
        rectVerticalRight = new Rect(0, 0, 1, 0.5f);

        rectFourWayTopLeft = new Rect(0, 0.5f, 0.5f, 0.5f);;
        rectFourWayTopRight = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
        rectFourWayBottomLeft = new Rect(0, 0, 0.5f, 0.5f);
        rectFourWayBottomRight = new Rect(0.5f, 0, 0.5f, 0.5f);
    }

    // Update is called once per frame
    void Update()
    {
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

    public static void Look(Player player, Vector2 lookValue)
    {
        if (!instance)
        {
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
                instance.threePlayerCamera.Look(player, lookValue);
                break;
        }
    }

    public static void Lock(Player player)
    {
        if (!instance)
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
                instance.threePlayerCamera.Lock(player);
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
                instance.threePlayerCamera.Zoom(player, zoomDir);
                break;
        }
        
    }

    public static Camera[] GetCameras()
    {
        return instance.cameras;
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

/*public class CameraManager : MonoBehaviour
{
    private class CharacterDistance
    {
        public Character firstCharacter;
        public Character secondCharacter;
        public float distance;
        public bool farthest;
    }

    public float characterReacquireTimer = 2.0f;
    public float distanceBeforeSplit;

    public bool horizontalSplit;
    
    private List<Character> characters;
    private float lastReacquireTime;
    
    private List<CharacterDistance> characterDistances;

    public Camera mainCamera;
    public Camera camera2;
    public Camera camera3;
    public Camera camera4;

    public float switchDelayTimer;

    [Range(1, 15)]
    public float singleScreenZoomDist;
    [Range(-8, 8)]
    public float singleScreenDistanceAboveOffset;
    [Range(0.5f, 5)]
    public float singleScreenOrthoIncrease;
    
    [Range(1, 15)]
    public float horizontalTwoScreenMinZoomDist;
    [Range(0.5f, 5)]
    public float horizontalTwoScreenOrthoIncrease;
    [Range(-8, 8)]
    public float horizontalTwoScreenDistanceAboveOffset;
    [Range(1, 15)]
    public float verticalTwoScreenMinZoomDist;
    [Range(0.5f, 5)]
    public float verticalTwoScreenOrthoIncrease;
    [Range(-8, 8)]
    public float verticalTwoScreenDistanceAboveOffset;
    
    [Range(1, 15)]
    public float fourScreenZoomDist;
    [Range(-8, 8)]
    public float fourScreenDistanceAboveOffset;

    private static CameraManager instance;
    
    
    
     //      Look ahead
     //          camera.localPosition = new Vector3(
     //                                      Mathf.Lerp(target.localPosition.x, lookAheadAmount * dir, lookAheadShiftSpeed * Time.deltaTime),
     //                                      target.localPosition.y,
     //                                      target.localPosition.z);
     
    
    

    void Start()
    {
        lastReacquireTime = Time.realtimeSinceStartup - characterReacquireTimer;
        characterDistances = new List<CharacterDistance>();
        instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.realtimeSinceStartup - lastReacquireTime >= characterReacquireTimer)
        {
            characters = FindObjectsByType<Character>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID).ToList();
        }
        
        if (characters.Count <= 1 || PlayerManager.GetPlayerCount() == 1)
        {
            SetNumCameras(1);
            if (singleScreenOrthoIncrease >= 1)
            {
                SetCameraPosition(mainCamera, characters, singleScreenZoomDist, singleScreenOrthoIncrease,
                    singleScreenDistanceAboveOffset);
            }
            else
            {
                SetCameraOnCharacter(mainCamera, characters, singleScreenZoomDist, singleScreenOrthoIncrease,
                    singleScreenDistanceAboveOffset);
            }

            return;
        }
        
        characterDistances.Clear();
        
        float farthestDist = float.MinValue;

        foreach (var character in characters)
        {
            foreach (var other in characters)
            {
                if (other.gameObject != character.gameObject)
                {
                    var dist = Vector3.Distance(other.transform.position, character.transform.position);

                    if (characterDistances.Any(d => d.firstCharacter == other && d.secondCharacter == character))
                    {
                        //This already exists in the reverse
                        continue;
                    }

                    characterDistances.Add(
                        new CharacterDistance()
                        {
                            firstCharacter = character,
                            secondCharacter = other,
                            distance = dist,
                            farthest = false
                        }
                    );

                    if (dist > farthestDist)
                    {
                        farthestDist = dist;
                    }
                }
            }
        }
        
        //It's extremely unlikely that there will be more than one occurrence, unless two or more characters are at the 
        //EXACT same coordinates.  In either case, the CharacterDistance will still contain the furthest character
        var farthest = characterDistances
                                        .Where(c => c.distance == farthestDist)
                                        .ToList()
                                        .First();
        farthest.farthest = true;
        var player1 = characters.First(c => c.GetCharacterNum() == 0);
        
        if (farthestDist < distanceBeforeSplit)
        {
            //Only one camera needed
            SetNumCameras(1);
            SetCameraPosition(mainCamera, characters, singleScreenZoomDist, singleScreenOrthoIncrease, singleScreenDistanceAboveOffset);
        }
        else
        {
            if (characters.Count == 2)
            {
                //There's only two characters, just put a camera on each and call it a day.
                SetNumCameras(2);

                float zoomDist;
                float distanceAboveOffset;

                if (horizontalSplit)
                {
                    zoomDist = horizontalTwoScreenMinZoomDist;
                    distanceAboveOffset = horizontalTwoScreenDistanceAboveOffset;
                }
                else
                {
                    zoomDist = verticalTwoScreenMinZoomDist;
                    distanceAboveOffset = verticalTwoScreenDistanceAboveOffset;
                }
                
                mainCamera.orthographicSize = zoomDist;
                camera2.orthographicSize = zoomDist;
                
                mainCamera.transform.position = new Vector3(characters[0].transform.position.x, characters[0].transform.position.y + distanceAboveOffset, -10);
                camera2.transform.position = new Vector3(characters[1].transform.position.x, characters[1].transform.position.y + distanceAboveOffset, -10);
            }
            else if (characters.Count == 3)
            {
                var middleCharacter = characterDistances.Where(c => !c.farthest).ToList();
                farthestDist = float.MaxValue;
                CharacterDistance closest = null;
                foreach (var m in middleCharacter)
                {
                    if (m.distance < farthestDist)
                    {
                        farthestDist = m.distance;
                        closest = m;
                    }
                }

                if (farthestDist < distanceBeforeSplit)
                {
                    //Two cameras
                    SetNumCameras(2);
                    
                    float orthoOffset;
                    float minZoomDist;
                    float distanceAboveOffset;

                    if (horizontalSplit)
                    {
                        minZoomDist = horizontalTwoScreenMinZoomDist;
                        orthoOffset = horizontalTwoScreenOrthoIncrease;
                        distanceAboveOffset = horizontalTwoScreenDistanceAboveOffset;
                    }
                    else
                    {
                        minZoomDist = verticalTwoScreenMinZoomDist;
                        orthoOffset = verticalTwoScreenOrthoIncrease;
                        distanceAboveOffset = verticalTwoScreenDistanceAboveOffset;
                        
                    }

                    if (closest.firstCharacter == player1 || closest.secondCharacter == player1)
                    {
                        //The main player is in the "group"
                        SetCameraPosition(mainCamera, new List<Character>() { closest.firstCharacter, closest.secondCharacter }, minZoomDist, orthoOffset, distanceAboveOffset);
                        var otherChar = characters.Where(c => c != closest.firstCharacter && c!= closest.secondCharacter);
                        SetCameraPosition(camera2, otherChar, minZoomDist, orthoOffset, distanceAboveOffset);
                        
                    }
                    else
                    {
                        //The main player is by themselves
                        SetCameraPosition(mainCamera, new List<Character>() { player1 }, minZoomDist,orthoOffset, distanceAboveOffset);
                        SetCameraPosition(camera2, new List<Character>() { closest.firstCharacter, closest.secondCharacter }, minZoomDist,orthoOffset, distanceAboveOffset);
                    }
                }
                else
                {
                    //Four Cameras
                    SetNumCameras(3);
                    mainCamera.orthographicSize = fourScreenZoomDist;
                    camera2.orthographicSize = fourScreenZoomDist;
                    camera3.orthographicSize = fourScreenZoomDist;
                    
                    mainCamera.transform.position = new Vector3(characters[0].transform.position.x, characters[0].transform.position.y + fourScreenDistanceAboveOffset, -10);
                    camera2.transform.position = new Vector3(characters[1].transform.position.x, characters[1].transform.position.y + fourScreenDistanceAboveOffset, -10);
                    camera3.transform.position = new Vector3(characters[2].transform.position.x, characters[2].transform.position.y + fourScreenDistanceAboveOffset, -10);
                }
            }
            else
            {
                var fourCameras = true;
                var firstCharacter = characterDistances.Where(c => c.firstCharacter == characters.First()).ToList();
                
                foreach (var d in firstCharacter)
                {
                    if (d.distance < distanceBeforeSplit)
                    {
                        fourCameras = false;
                    }
                }

                if (fourCameras)
                {
                    //Everyone is too far apart to share a camera.  Set up 4 cameras.
                    SetNumCameras(4);
                    mainCamera.orthographicSize = fourScreenZoomDist;
                    camera2.orthographicSize = fourScreenZoomDist;
                    camera3.orthographicSize = fourScreenZoomDist;
                    camera4.orthographicSize = fourScreenZoomDist;
                    
                    mainCamera.transform.position = new Vector3(characters[0].transform.position.x, characters[0].transform.position.y + fourScreenDistanceAboveOffset, -10);
                    camera2.transform.position = new Vector3(characters[1].transform.position.x, characters[1].transform.position.y + fourScreenDistanceAboveOffset, -10);
                    camera3.transform.position = new Vector3(characters[2].transform.position.x, characters[2].transform.position.y + fourScreenDistanceAboveOffset, -10);
                    camera4.transform.position = new Vector3(characters[3].transform.position.x, characters[3].transform.position.y + fourScreenDistanceAboveOffset, -10);
                }
                else
                {
                    //Need to figure out which characters are close to each other.
                    SetNumCameras(2);
                    
                     

                    //print(farthest.firstCharacter + " " + farthest.secondCharacter);
                    var firstDistances = characterDistances
                                                                .Where(c => 
                                                                            (farthest.firstCharacter == c.firstCharacter && c.secondCharacter != farthest.secondCharacter) || 
                                                                            (farthest.firstCharacter == c.secondCharacter && c.firstCharacter != farthest.secondCharacter)
                                                                ).ToList();

                    var secondDistances = characterDistances
                                                                        .Where(c => 
                                                                            (farthest.secondCharacter == c.firstCharacter && c.secondCharacter != farthest.firstCharacter) || 
                                                                            (farthest.secondCharacter == c.secondCharacter && c.firstCharacter != farthest.firstCharacter)
                                                                        ).ToList();

                    var firstList = new List<Character>{farthest.firstCharacter};
                    var secondList = new List<Character>{farthest.secondCharacter};

                    foreach (var first in firstDistances)
                    {
                        var other = first.firstCharacter == farthest.firstCharacter ? first.secondCharacter : first.firstCharacter;
                        var otherDist = secondDistances
                                                .First(c => c.firstCharacter == other || c.secondCharacter == other)
                                                .distance;

                        if (first.distance < otherDist)
                        {
                            firstList.Add(other);
                        }
                        else
                        {
                            secondList.Add(other);
                        }
                    }
                    
                    float orthoOffset;
                    float minZoomDist;
                    float distanceAboveOffset;

                    if (horizontalSplit)
                    {
                        minZoomDist = horizontalTwoScreenMinZoomDist;
                        orthoOffset = horizontalTwoScreenOrthoIncrease;
                        distanceAboveOffset = horizontalTwoScreenDistanceAboveOffset;
                    }
                    else
                    {
                        minZoomDist = verticalTwoScreenMinZoomDist;
                        orthoOffset = verticalTwoScreenOrthoIncrease;
                        distanceAboveOffset = verticalTwoScreenDistanceAboveOffset;
                        
                    }

                    if (farthest.firstCharacter == player1)
                    {
                        //The main player is in the first group
                        SetCameraPosition(mainCamera, firstList, minZoomDist, orthoOffset, distanceAboveOffset);
                        SetCameraPosition(camera2, secondList, minZoomDist, orthoOffset, distanceAboveOffset);
                        
                    }
                    else
                    {
                        //The main player is in the second group
                        SetCameraPosition(camera2, firstList, minZoomDist, orthoOffset, distanceAboveOffset);
                        SetCameraPosition(mainCamera, secondList, minZoomDist, orthoOffset, distanceAboveOffset);
                    }
                }
            }
        }
    }

    private void SetNumCameras(int numCameras)
    {
        if (numCameras == 1)
        {
            mainCamera.rect = new Rect(0, 0, 1, 1);
            camera2.enabled = false;
            camera3.enabled = false;
            camera4.enabled = false;
        }
        else if (numCameras == 2)
        {
            camera2.enabled = true;
            camera3.enabled = false;
            camera4.enabled = false;

            if (!horizontalSplit)
            {
                mainCamera.rect = new Rect(0, 0, 0.5f, 1);
                camera2.rect = new Rect(0.5f, 0, 0.5f, 1);
            }
            else
            {
                mainCamera.rect = new Rect(0, 0.5f, 1, 0.5f);
                camera2.rect = new Rect(0, 0, 1, 0.5f);
            }
        }
        else if (numCameras == 3)
        {
            camera2.enabled = true;
            camera3.enabled = true;
            camera4.enabled = true;

            mainCamera.rect = new Rect(0, 0.5f, 0.5f, 0.5f);
            camera2.rect = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
            camera3.rect = new Rect(0, 0, 0.5f, 0.5f);
            camera4.rect = new Rect(0.5f, 0, 0.5f, 0.5f);

            camera4.transform.position = new Vector3(0, 0, 0);
        }
        else
        {
            camera2.enabled = true;
            camera3.enabled = true;
            camera4.enabled = true;

            mainCamera.rect = new Rect(0, 0.5f, 0.5f, 0.5f);
            camera2.rect = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
            camera3.rect = new Rect(0, 0, 0.5f, 0.5f);
            camera4.rect = new Rect(0.5f, 0, 0.5f, 0.5f);
        }
    }

    private Vector2 GetCenterOfCharacters(List<Character> charList)
    {
        float right = float.MinValue;
        float left = float.MaxValue;
        
        float up = float.MinValue;
        float down = float.MaxValue;

        foreach (var character in charList)
        {
            if (character.transform.position.x > right)
            {
                right = character.transform.position.x;
            }
                
            if (character.transform.position.x < left)
            {
                left = character.transform.localPosition.x;
            }

            if (character.transform.position.y > up)
            {
                up = character.transform.position.y;
            }
                
            if (character.transform.position.y < down)
            {
                down = character.transform.position.y;
            }
        }
        
        var center = new Vector2(
                        (right + left) * 0.5f,
                        (down + up) * 0.5f
                    );
            
        return center;
    }

    private void SetCameraOnCharacter(Camera camera, IEnumerable<Character> charList, float minZoomDist, float orthoIncrease, float distanceAboveOffset)
    {
        var charMove = FindFirstObjectByType<CharacterMove>();
        Character target = null;
        if (charMove)
        {
            target = charMove.GetCurrentCharacter();
        }
        
        camera.transform.position = new Vector3(target.transform.position.x, target.transform.position.y + distanceAboveOffset, -10);
        camera.orthographicSize = minZoomDist;
        
    }
    
    private void SetCameraPosition(Camera camera, IEnumerable<Character> charList, float minZoomDist, float orthoIncrease, float distanceAboveOffset)
    {
        Bounds bounds = new Bounds(charList.First().transform.position, Vector3.zero);
        foreach (var character in charList)
        {
            bounds.Encapsulate(character.transform.position);
        }
            
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y + distanceAboveOffset, -10);
        
        float boundsWidth = bounds.size.x;
        float boundsHeight = bounds.size.y;
        float boundsAspect = boundsWidth / boundsHeight;
        
        float orthoSize;

        if (boundsAspect > camera.aspect)
        {
            orthoSize = (boundsWidth / camera.aspect) * 0.5f;
        }
        else
        {
            orthoSize = boundsHeight * 0.5f;
        }

        orthoSize *= orthoIncrease;
        
        if (orthoSize < minZoomDist)
        {
            orthoSize = minZoomDist;
        }

        camera.orthographicSize = orthoSize;
    }

    public static void ZoomOut(int playerNum)
    {
        instance.singleScreenOrthoIncrease += 0.01f;
    }
    
    public static void ZoomIn(int playerNum)
    {
        instance.singleScreenOrthoIncrease -= 0.01f;
    }
}
 */