using System;
using System.Collections.Generic;
using UnityEngine;
using Characters;

public class FourPlayerCamera : MonoBehaviour
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
    [Tooltip("Whether each view is locked to its character. Changes as the game runs: for watching, not for setting.")]
    public bool[] locked;
    
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
                print($"FOUR PLAYER CAMERA NUMBER {i + 1} IS NULL");
            }
        }

        players = new Player[4];
        locked = new bool[4];
        lookPosition = new Vector2[4];
        targetPosition = new Vector2[4];
        characters = new Character[4];
        distance = new float[6];
        
        camTransitionSpeed = new float[4];
        camFollowSpeed = new float[4];
        following = new bool[4];
        currentCharacter = new Character[4];

        for (int i = 0; i < 4; i++)
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
        
        if (!players[0] || !players[1] || !players[2] || !players[3])
        {
            players[0] = Player.GetPlayer(1);
            players[1] = Player.GetPlayer(2);
            players[2] = Player.GetPlayer(3);
            players[3] = Player.GetPlayer(4);
            return;
        }
        
        for (int i = 0; i < 4; i++)
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
        distance[2] = Vector3.Distance(characters[0].transform.position, characters[3].transform.position);
        distance[3] = Vector3.Distance(characters[1].transform.position, characters[2].transform.position);
        distance[4] = Vector3.Distance(characters[1].transform.position, characters[3].transform.position);
        distance[5] = Vector3.Distance(characters[2].transform.position, characters[3].transform.position);

        float greatestDistance = float.MinValue;
        for (int i = 0; i < 6; i++)
        {
            if (distance[i] > greatestDistance)
            {
                greatestDistance = distance[i];
            }
        }

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
            
            //3 player turns this camera around, so make sure its faced back.
            playerCam[3].transform.eulerAngles = new Vector3(0, 0, 0);

            SetTarget(0);
            SetTarget(1);
            SetTarget(2);
            SetTarget(3);
        }
    }

    public void CenterTarget()
    {
        var positions = new List<Vector3>(){
            players[0].GetCharacter().transform.position,
            players[1].GetCharacter().transform.position,
            players[2].GetCharacter().transform.position,
            players[3].GetCharacter().transform.position
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
        if (locked[which])
        {
            return;
        }

        lookPosition[which] = lookValue;
    }

    public void Lock(Player player)
    {
        var which = Array.FindIndex(players, p => p == player);
        locked[which] = !locked[which];
    }
    
    public void Active(bool setting)
    {
        if (active == false && setting == true)
        {
            //Was another setting and is now this one.
            playerCam[3].transform.eulerAngles = new Vector3(0, 0, 0);
        }

        active = setting;
    }
}
