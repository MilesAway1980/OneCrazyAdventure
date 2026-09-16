using System;
using System.Collections.Generic;
using UnityEngine;
using Characters;

public class ThreePlayerCamera : MonoBehaviour
{
    public float transitionSpeed;
    public float followSpeed;
    public float lookDist;
    public float splitDistance;
    
    public Player[] players;

    public Vector2[] targetPosition;
    public Vector2[] lookPosition;

    public Character[] characters;

    public float[] distance;
    
    public float maxZoom;
    public float startZoom;
    public float minZoom;
    
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

        float greatestDistance;

        if (distance[0] > distance[1] && distance[0] > distance[2])
        {
            greatestDistance = distance[0];
        }
        else if (distance[1] > distance[0] && distance[1] > distance[2])
        {
            greatestDistance = distance[1];
        }
        else
        {
            greatestDistance = distance[2];
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