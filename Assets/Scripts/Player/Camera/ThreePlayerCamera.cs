using System;
using System.Collections.Generic;
using UnityEngine;
using Characters;

public class ThreePlayerCamera : MonoBehaviour
{
    [Tooltip("How fast, in units a second, the camera moves to a new character. Used until it has caught up with them.")]
    public float transitionSpeed;
    [Tooltip("How fast, in units a second, the camera keeps up with a character once it has caught up with them.")]
    public float followSpeed;
    [Tooltip("How far the look stick moves the camera, at the widest zoom.")]
    public float lookDist;
    [Tooltip("How far apart, in units, the characters can get before the screen splits into one view for each of them.")]
    public float splitDistance;
    
    [Tooltip("The players this camera follows. Filled in when the game starts: nothing to set here.")]
    public Player[] players;

    [Tooltip("Where the camera is heading, in the world. Worked out as the game runs: for watching, not for setting.")]
    public Vector2[] targetPosition;
    [Tooltip("How far the look stick is pushed at the moment. Worked out as the game runs: for watching, not for setting.")]
    public Vector2[] lookPosition;

    [Tooltip("The characters being followed, one for each player. Filled in as the game runs: nothing to set here.")]
    public Character[] characters;

    [Tooltip("How far apart the characters are, for each pair of them. Worked out as the game runs: for watching, not for setting.")]
    public float[] distance;
    
    [Tooltip("The widest the camera can zoom out to (the largest camera size).")]
    public float maxZoom;
    [Tooltip("The camera size when the game starts.")]
    public float startZoom;
    [Tooltip("The closest the camera can zoom in to (the smallest camera size).")]
    public float minZoom;
    
    [Tooltip("How much the zoom buttons change the camera size each time they're pressed.")]
    public float zoomSpeed;
    
    private bool lastSplit;
    private bool active;
    
    private Camera[] playerCam;
    private Character lockedCharacter;
    private float lockedZoomSetting;
    private Vector2 lockedLookPosition;
    
    private float[] camTransitionSpeed;
    private float[] camFollowSpeed;
    private bool[] following;
    private Character[] currentCharacter;

    void Start()
    {
        playerCam = CameraManager.GetCameras();

        for (int i = 0; i < playerCam.Length; i++)
        {
            if (playerCam[i])
            {
                playerCam[i].orthographicSize = startZoom;
            }
            else
            {
                print($"THREE PLAYER CAMERA NUMBER {i + 1} IS NULL");
            }
        }

        players = new Player[3];
        lookPosition = new Vector2[3];
        targetPosition = new Vector2[3];
        characters = new Character[3];
        distance = new float[3];
        lockedCharacter = null;

        camTransitionSpeed = new float[3];
        camFollowSpeed = new float[3];
        following = new bool[3];
        currentCharacter = new Character[3];

        for (int i = 0; i < 3; i++)
        {
            camTransitionSpeed[i] = transitionSpeed;
            camFollowSpeed[i] = followSpeed;
        }
    }

    void Update()
    {
        if (!active)
        {
            return;
        }
        
        if (!players[0] || !players[1] || !players[2])
        {
            players[0] = Player.GetPlayer(1);
            players[1] = Player.GetPlayer(2);
            players[2] = Player.GetPlayer(3);
            return;
        }
        
        for (int i = 0; i < 3; i++)
        {
            characters[i] = players[i].GetCharacter();
            if (!characters[i])
            {
                return;
            }

            if (currentCharacter[i] != characters[i])
            {
                currentCharacter[i] = characters[i];
                following[i] = false;
            }
        }
        
        distance[0] = Vector3.Distance(characters[0].transform.position, characters[1].transform.position);
        distance[1] = Vector3.Distance(characters[0].transform.position, characters[2].transform.position);
        distance[2] = Vector3.Distance(characters[1].transform.position, characters[2].transform.position);

        float greatestDistance = Mathf.Max(distance[0], distance[1], distance[2]);

        if (greatestDistance <= splitDistance)
        {
            CameraManager.SetRect(playerCam[0], CameraManager.rectFullScreen);
            playerCam[1].enabled = false;
            playerCam[2].enabled = false;
            playerCam[3].enabled = false;
                    
            CenterTarget();
        }
        else
        {
            playerCam[1].enabled = true;
            playerCam[2].enabled = true;
            playerCam[3].enabled = true;

            CameraManager.SetRect(playerCam[0], CameraManager.rectFourWayTopLeft);
            CameraManager.SetRect(playerCam[1], CameraManager.rectFourWayTopRight);
            CameraManager.SetRect(playerCam[2], CameraManager.rectFourWayBottomLeft);
            CameraManager.SetRect(playerCam[3], CameraManager.rectFourWayBottomRight);
            
            SetTarget(0);
            SetTarget(1);
            SetTarget(2);
        }

        if (lockedCharacter)
        {
            playerCam[3].transform.eulerAngles = new Vector3(0, 0, 0);
            playerCam[3].orthographicSize = lockedZoomSetting;

            var zoomVal = lockedZoomSetting / maxZoom;
            var look = new Vector2(
                lockedLookPosition.x * lookDist * zoomVal,
                lockedLookPosition.y * lookDist * zoomVal
            );

            playerCam[3].transform.position = new Vector3(
                lockedCharacter.transform.position.x + look.x,
                lockedCharacter.transform.position.y + look.y,
                playerCam[3].transform.position.z
            );
        }
        else
        {
            playerCam[3].transform.eulerAngles = new Vector3(0, 180, 0);
        }
    }

    public void CenterTarget()
    {
        var positions = new List<Vector3>(){
            players[0].GetCharacter().transform.position,
            players[1].GetCharacter().transform.position,
            players[2].GetCharacter().transform.position
        };


        var center = GameMath.FindCenter(positions);
        
        targetPosition[0] = center;

        MoveCamera(0);
    }

    public void SetTarget(int whichPlayer)
    {
        if (players != null && whichPlayer < players.Length)
        {
            var character = players[whichPlayer].GetCharacter();
            if (character)
            {
                targetPosition[whichPlayer] = character.transform.position;
            }
        }

        MoveCamera(whichPlayer);
    }

    public void MoveCamera(int whichPlayer)
    {
        float zoomVal;
        zoomVal = playerCam[whichPlayer].orthographicSize / maxZoom;
            
        var look = new Vector2(lookPosition[whichPlayer].x * lookDist * zoomVal, lookPosition[whichPlayer].y * lookDist * zoomVal);
        if (lookPosition[whichPlayer] != Vector2.zero)
        {
            targetPosition[whichPlayer] = new Vector2(
                targetPosition[whichPlayer].x + look.x,
                targetPosition[whichPlayer].y + look.y
            ); 
        }
        
        if ((Vector2)playerCam[whichPlayer].transform.position != targetPosition[whichPlayer])
        {
            float speed;

            if (following[whichPlayer])
            {
                speed = camFollowSpeed[whichPlayer];
            }
            else
            {
                speed = camTransitionSpeed[whichPlayer];
            }

            var destination = new Vector3(
                targetPosition[whichPlayer].x,
                targetPosition[whichPlayer].y,
                playerCam[whichPlayer].transform.position.z
            );

            playerCam[whichPlayer].transform.position = Vector3.MoveTowards(
                playerCam[whichPlayer].transform.position,
                destination,
                speed * Time.deltaTime
            );
        }
        else
        {
            following[whichPlayer] = true;
        }
    }

    public void CheckZoom(int whichPlayer)
    {
        var currentZoom = playerCam[whichPlayer].orthographicSize;
        
        if (currentZoom < minZoom)
        {
            currentZoom = minZoom;
        }
        
        if (currentZoom > maxZoom)
        {
            currentZoom = maxZoom;
        }
        
        playerCam[whichPlayer].orthographicSize = currentZoom;
    }

    public void Zoom(Player player, float zoomDir)
    {
        var which = Array.FindIndex(players, p => p == player);
        var currentZoom = playerCam[which].orthographicSize;

        if (zoomDir < 0)
        {
            currentZoom -= zoomSpeed;
        }
        else if (zoomDir > 0)
        {
            currentZoom += zoomSpeed;
        }
        
        playerCam[which].orthographicSize = currentZoom;
        
        CheckZoom(which);
    }

    public void Look(Player player, Vector2 lookValue)
    {
        var which = Array.FindIndex(players, p => p == player);
        lookPosition[which] = lookValue;
    }

    public void Lock(Player player)
    {
        var which = Array.FindIndex(players, p => p == player);
        var character = players[which].GetCharacter();
        if (character)
        {
            if (lockedCharacter == character)
            {
                lockedCharacter = null;
            }
            else
            {
                if (!lockedCharacter)
                {
                    lockedZoomSetting = playerCam[which].orthographicSize;
                    lockedLookPosition = lookPosition[which];
                    lockedCharacter = character;
                }
            }
        }
    }
    
    public void Active(bool setting)
    {
        active = setting;
    }
}

//Schedule meeting with Renil for going over next tasks (documentation, code cleanup)
//Schedule with Kevin 
//