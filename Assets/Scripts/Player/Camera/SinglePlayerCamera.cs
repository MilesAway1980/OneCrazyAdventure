using System.Collections.Generic;
using UnityEngine;
using Characters;

public class SinglePlayerCamera : MonoBehaviour
{
    public Vector2 targetPosition;
    public float transitionSpeed;
    public float followSpeed;
    public float lookDist;

    public Player player;
    public Vector2 lookPosition;
    public float maxZoom;
    public float startZoom;
    public float minZoom;
    public float zoomSpeed;

    public bool horizontalSplit;
    
    private bool active;
    private Camera[] playerCam;

    private float[] zoomSettings;
    private Vector2[] lookSettings;

    private List<Character> lockedCharacters;

    private bool following;
    private Character currentCharacter;
    
    void Start()
    {
        playerCam = CameraManager.GetCameras();
        lockedCharacters = new List<Character>();
        
        playerCam[0].orthographicSize = startZoom;

        zoomSettings = new float[3];
        lookSettings = new Vector2[3];
    }

    void Update()
    {
        if (!active)
        {
            return;
        }

        if (!player)
        {
            player = Player.GetPlayer(1);
            return;     //This will only happen once or twice, but will make it so we don't have to check to see if the player is not null before continuing.
        }

        var character = player.GetCharacter();
        if (character)
        {
            targetPosition = new Vector2(
                character.transform.position.x,
                character.transform.position.y
            );

            if (currentCharacter != character)
            {
                following = false;
                currentCharacter = character;
            }
        }
        else
        {
            print("Player has no character");
        }
        
        
        var zoomVal = playerCam[0].orthographicSize / maxZoom;
        var look = new Vector2(lookPosition.x * lookDist * zoomVal, lookPosition.y * lookDist * zoomVal);
        if (lookPosition != Vector2.zero)
        {
            targetPosition = new Vector3(
                targetPosition.x + look.x,
                targetPosition.y + look.y
            ); 
        }

        float speed;
        if (following)
        {
            speed = followSpeed;
        }
        else
        {
            speed = transitionSpeed;
        }
        
        if ((Vector2)playerCam[0].transform.position != targetPosition)
        {
            playerCam[0].transform.position = Vector3.MoveTowards(
                playerCam[0].transform.position,
                new Vector3(targetPosition.x, targetPosition.y, playerCam[0].transform.position.z),
                speed * Time.deltaTime
            );
        }
        else
        {
            following = true;
        }

        if (lockedCharacters.Count == 0)
        {
            CameraManager.SetRect(playerCam[0], CameraManager.rectFullScreen);
            playerCam[1].enabled = false;
            playerCam[2].enabled = false;
            playerCam[3].enabled = false;
        }
        else
        {
            if (lockedCharacters.Count == 1)
            {
                //Split Screen
                if (!horizontalSplit)
                {
                    CameraManager.SetRect(playerCam[0], CameraManager.rectVerticalLeft);
                    CameraManager.SetRect(playerCam[1], CameraManager.rectVerticalRight);
                }
                else
                {
                    CameraManager.SetRect(playerCam[0], CameraManager.rectHorizontalTop);
                    CameraManager.SetRect(playerCam[1], CameraManager.rectHorizontalBottom);
                }

                playerCam[1].enabled = true;
                playerCam[2].enabled = false;
                playerCam[3].enabled = false;
            }

            if (lockedCharacters.Count == 2)
            {
                //4 way screen
                CameraManager.SetRect(playerCam[0], CameraManager.rectFourWayTopLeft);
                CameraManager.SetRect(playerCam[1], CameraManager.rectFourWayTopRight);
                CameraManager.SetRect(playerCam[2], CameraManager.rectFourWayBottomLeft);
                CameraManager.SetRect(playerCam[3], CameraManager.rectFourWayBottomRight);

                //playerCam[0].enabled = true;
                playerCam[1].enabled = true;
                playerCam[2].enabled = true;
                playerCam[3].enabled = true;

                playerCam[3].transform.eulerAngles = new Vector3(0, 180, 0);
            }
            
            if (lockedCharacters.Count == 3)
            {
                //4 way screen

                CameraManager.SetRect(playerCam[0], CameraManager.rectFourWayTopLeft);
                CameraManager.SetRect(playerCam[1], CameraManager.rectFourWayTopRight);
                CameraManager.SetRect(playerCam[2], CameraManager.rectFourWayBottomLeft);
                CameraManager.SetRect(playerCam[3], CameraManager.rectFourWayBottomRight);

                //playerCam[0].enabled = true;
                playerCam[1].enabled = true;
                playerCam[2].enabled = true;
                playerCam[3].enabled = true;

                playerCam[3].transform.eulerAngles = new Vector3(0, 0, 0);
            }

            for (int i = 0; i < lockedCharacters.Count; i++)
            {
                playerCam[i + 1].orthographicSize = zoomSettings[i];

                look = new Vector2(lookSettings[i].x * lookDist * zoomVal, lookSettings[i].y * lookDist * zoomVal);

                playerCam[i + 1].transform.position = new Vector3(
                    lockedCharacters[i].transform.position.x + look.x,
                    lockedCharacters[i].transform.position.y + look.y,
                    playerCam[i + 1].transform.position.z
                );
            }
        }
    }

    public void Look(Vector2 lookValue)
    {
        lookPosition = lookValue;
    }

    public void Zoom(int zoomDir)
    {
        var currentZoom = playerCam[0].orthographicSize;
        if (zoomDir < 0)
        {
            currentZoom -= zoomSpeed;
            if (currentZoom < minZoom)
            {
                currentZoom = minZoom;
            }
        }
        else if (zoomDir > 0)
        {
            currentZoom += zoomSpeed;
            if (currentZoom > maxZoom)
            {
                currentZoom = maxZoom;
            }
        }
        
        playerCam[0].orthographicSize = currentZoom;
    }

    public void Lock()
    {
        if (!player)
        {
            return;
        }
        
        var character = player.GetCharacter();

        if (lockedCharacters.Contains(character))
        {
            lockedCharacters.Remove(character);
            return;
        }

        if (lockedCharacters.Count < 3)
        {
            lockedCharacters.Add(character);
            zoomSettings[lockedCharacters.Count - 1] = playerCam[0].orthographicSize;
            lookSettings[lockedCharacters.Count - 1] = lookPosition;
        }
    }

    public void Active(bool setting)
    {
        active = setting;
    }
}
