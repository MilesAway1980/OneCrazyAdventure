using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Characters;

public class TwoPlayerCamera : MonoBehaviour
{
    public float transitionSpeed;
    public float followSpeed;
    public float lookDist;
    public float splitDistance;

    public Player[] players;

    public Vector2[] targetPosition;
    public Vector2[] lookPosition;

    public Character[] characters;

    public float horizontalMaxZoom;
    public float horizontalStartZoom;
    public float horizontalMinZoom;

    public float verticalMaxZoom;
    public float verticalStartZoom;
    public float verticalMinZoom;

    public float zoomSpeed;
    public bool horizontalSplit;

    private bool active;
    private float distance;

    private Camera[] playerCam;

    private float[] zoomSettings;
    private Vector2[] lookSettings;

    private Dictionary<Player, List<Character>> lockedCharacters;

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
                if (horizontalSplit)
                {
                    playerCam[i].orthographicSize = horizontalStartZoom;
                }
                else
                {
                    playerCam[i].orthographicSize = verticalStartZoom;
                }
            }
            else
            {
                print($"TWO PLAYER CAMERA NUMBER {i + 1} IS NULL");
            }
        }

        players = new Player[2];
        lookPosition = new Vector2[2];
        targetPosition = new Vector2[2];
        characters = new Character[2];

        zoomSettings = new float[2];
        lookSettings = new Vector2[2];
        lockedCharacters = new Dictionary<Player, List<Character>>();

        camTransitionSpeed = new float[2];
        camFollowSpeed = new float[2];
        following = new bool[2];
        currentCharacter = new Character[2];

        for (int i = 0; i < 2; i++)
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

        if (!players[0] || !players[1])
        {
            players[0] = Player.GetPlayer(1);
            players[1] = Player.GetPlayer(2);

            return;
        }

        for (int i = 0; i < 2; i++)
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

        distance = Vector3.Distance(characters[0].transform.position, characters[1].transform.position);

        var currentCount = lockedCharacters?.Values.Sum(l => l.Count);
        if (currentCount == 0)
        {
            playerCam[2].enabled = false;
            playerCam[3].enabled = false;

            if (distance <= splitDistance)
            {
                playerCam[1].enabled = false;
                playerCam[0].rect = new Rect(0, 0, 1, 1);

                CenterTarget();
                CheckZoom(0);
            }
            else
            {
                playerCam[1].enabled = true;

                SetTarget(0);
                SetTarget(1);

                CheckZoom(0);
                CheckZoom(1);

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
            }
        }
        else
        {
            CameraManager.SetRect(playerCam[0], CameraManager.rectFourWayTopLeft);
            CameraManager.SetRect(playerCam[1], CameraManager.rectFourWayTopRight);
            CameraManager.SetRect(playerCam[2], CameraManager.rectFourWayBottomLeft);
            CameraManager.SetRect(playerCam[3], CameraManager.rectFourWayBottomRight);

            playerCam[0].enabled = true;
            playerCam[1].enabled = true;
            playerCam[2].enabled = true;
            playerCam[3].enabled = true;

            SetTarget(0);
            SetTarget(1);

            CheckZoom(0);
            CheckZoom(1);

            Character char1 = null;
            Character char2 = null;

            int cam;
            foreach (var p in lockedCharacters)
            {
                foreach (var c in p.Value)
                {
                    if (!char1)
                    {
                        char1 = c;
                        cam = 0;
                    }
                    else
                    {
                        char2 = c;
                        cam = 1;
                    }

                    playerCam[cam].orthographicSize = zoomSettings[cam];

                    var zoomVal = zoomSettings[cam] / verticalMaxZoom;
                    var look = new Vector2(lookSettings[cam].x * lookDist * zoomVal,
                        lookSettings[cam].y * lookDist * zoomVal);

                    playerCam[cam + 2].transform.position = new Vector3(
                        c.transform.position.x + look.x,
                        c.transform.position.y + look.y,
                        playerCam[cam + 2].transform.position.z
                    );
                }
            }

            if (char1 && !char2)
            {
                playerCam[3].transform.eulerAngles = new Vector3(0, 180, 0);
            }
            else
            {
                playerCam[3].transform.eulerAngles = new Vector3(0, 0, 0);
            }
        }
    }

    public void CenterTarget()
    {
        var pos1 = players[0].GetCharacter().transform.position;
        var pos2 = players[1].GetCharacter().transform.position;

        var center = Vector3.Lerp(pos1, pos2, 0.5f);
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
        if (horizontalSplit)
        {
            zoomVal = playerCam[whichPlayer].orthographicSize / horizontalMaxZoom;
        }
        else
        {
            zoomVal = playerCam[whichPlayer].orthographicSize / verticalMaxZoom;
        }

        var look = new Vector2(lookPosition[whichPlayer].x * lookDist * zoomVal,
            lookPosition[whichPlayer].y * lookDist * zoomVal);
        if (lookPosition[whichPlayer] != Vector2.zero)
        {
            targetPosition[whichPlayer] = new Vector2(
                targetPosition[whichPlayer].x + look.x,
                targetPosition[whichPlayer].y + look.y
            );
        }

        float speed;

        if (following[whichPlayer])
        {
            speed = camFollowSpeed[whichPlayer];
        }
        else
        {
            speed = camTransitionSpeed[whichPlayer];
        }

        if ((Vector2)playerCam[whichPlayer].transform.position != targetPosition[whichPlayer])
        {
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
        if (horizontalSplit)
        {
            if (currentZoom < horizontalMinZoom)
            {
                currentZoom = horizontalMinZoom;
            }

            if (currentZoom > horizontalMaxZoom)
            {
                currentZoom = horizontalMaxZoom;
            }
        }
        else
        {
            if (currentZoom < verticalMinZoom)
            {
                currentZoom = verticalMinZoom;
            }

            if (currentZoom > verticalMaxZoom)
            {
                currentZoom = verticalMaxZoom;
            }
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
        if (!player)
        {
            return;
        }

        var which = Array.FindIndex(players, p => p == player);

        var character = players[which].GetCharacter();

        var currentCount = lockedCharacters.Values.Sum(l => l.Count);

        if (character)
        {
            List<Character> charList;
            if (!lockedCharacters.TryGetValue(players[which], out charList))
            {
                charList = new List<Character>();
                lockedCharacters.Add(players[which], charList);
            }

            if (charList.Contains(character))
            {
                charList.Remove(character);
            }
            else
            {
                if (currentCount == 2)
                {
                    return;
                }

                zoomSettings[currentCount] = playerCam[which].orthographicSize;
                lookSettings[currentCount] = lookPosition[which];

                charList.Add(character);
            }
        }
    }

    public void Active(bool setting)
    {
        active = setting;
    }
}
