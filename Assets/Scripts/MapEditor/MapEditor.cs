using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class MapEditor : MonoBehaviour
{
    /*public bool editorEnabled;
    public float[] moveSpeed;
    private int currentMoveSpeed;
    
    List<Tilemap> tilemaps;
    LineRenderer mouseLineRenderer;
    LineRenderer selectedLineRenderer;
    
    private InputAction editAction;
    private InputAction leftClickAction;
    private InputAction middleClickAction;
    private InputAction rightClickAction;
    private InputAction scrollWheelAction;
    private InputAction moveAction;
    private InputAction shiftAction;
    
    //private UIDocument editorUIDocument;
    private EditorUI editorUI;
    
    private bool editPress;

    private List<Tile> allTiles;

    private Vector2? selected = null;

    private Camera cam;
    private GameObject viewTarget;

    public static MapEditor mapEditor;
    
    void Start()
    {
        tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None).ToList();
        //mouseLineRenderer = GetComponent<LineRenderer>();
        
        var lineRenderers = GetComponentsInChildren<LineRenderer>();
        
        mouseLineRenderer = lineRenderers[0];
        selectedLineRenderer = lineRenderers[1];
        
        mouseLineRenderer.startWidth = 0.05f;
        mouseLineRenderer.endWidth = 0.05f;
        
        mouseLineRenderer.positionCount = 5;
        
        selectedLineRenderer.startWidth = 0.05f;
        selectedLineRenderer.endWidth = 0.05f;
        
        selectedLineRenderer.positionCount = 5;
        
        editAction = InputSystem.actions.FindAction("Editor");
        leftClickAction = InputSystem.actions.FindAction("Click");
        middleClickAction = InputSystem.actions.FindAction("MiddleClick");
        rightClickAction = InputSystem.actions.FindAction("RightClick");
        scrollWheelAction = InputSystem.actions.FindAction("ScrollWheel");
        moveAction = InputSystem.actions.FindAction("Move");
        shiftAction = InputSystem.actions.FindAction("Shift");
        
        //editorUIDocument = GetComponentInChildren<UIDocument>();
        
        mapEditor = this;
        cam = Camera.main;
        viewTarget = new GameObject();
        editorUI = GetComponent<EditorUI>();

        print("Set Editor Scroll");
        //If we do this immediately, it hasn't been set in the editor and will null reference exception.
        StartCoroutine(SetScrollSpeed());

        //var mapData = "Maps/MapData.txt";

        //var x = Resources.Load(mapData);
        //print(x);


        /*if (File.Exists(mapData))
        {
            file = File.OpenWrite(mapData);
        }
        else
        {
            file = File.Create(mapData);
        }

        //BinaryFormatter bf = new BinaryFormatter();
        //bf.Serialize(file, test);
        //https://discussions.unity.com/t/how-do-you-save-write-and-load-from-a-file/180577/2

        var objBytes = Encoding.UTF8.GetBytes(test.ToString());
        file.Write(objBytes);
        file.Close();*/

        /*if (File.Exists(mapData))
        {
            file = File.OpenRead(mapData);
        }
        else
        {
            file = File.Create(mapData);
        }

        var contents = file.R*/
    //}

    // Update is called once per frame
    /*void Update()
    {
        mouseLineRenderer.enabled = editorEnabled;
        selectedLineRenderer.enabled = editorEnabled;
        
        if (editAction.IsPressed() && editAction.triggered)
        {
            editorEnabled = !editorEnabled;
        }

        if (!editorEnabled)
        {
            //editorUIDocument
            editorUI.Disable();
            return;
        }
        
        editorUI.Enable();
        
        /*
            .IsPressed() returns true if the button is held down and false if it's released  (held down)
            .triggered is only true when it's first pressed or released
            
            IsPressed() && triggered == Just pressed
            !IsPressed() && triggered == Just released 
         */
        
        /*if (leftClickAction.triggered)
        {
            if (leftClickAction.IsPressed())
            {
                print("Left click");
            }
            else
            {
                print("Left released");
            }
        }*/
        
        /*if (middleClickAction.IsPressed())
        {
            print ("Middle Click");
        }
        
        /*if (rightClickAction.IsPressed())
        {
            print ("Right Click");
        }
        
        if (scrollWheelAction.IsInProgress())
        {
            var scrollDir = scrollWheelAction.ReadValue<Vector2>();
            if (scrollDir.x == -1)
            {
                //Scroll wheel tilt left
            }
            else if (scrollDir.x == 1)
            {
                //Scroll wheel tilt right
            }

            if (scrollDir.y == -1)
            {
                //print ("Scroll Down");
            }
            else if (scrollDir.y == 1)
            {
                //print ("Scroll Up");
            }
        }*/

        /*mouseLineRenderer.enabled = true;

        var mouse = Mouse.current;
        var mousePosition = mouse.position.ReadValue();
        var mouseOnMap = cam.ScreenToWorldPoint(mousePosition);

        var mapPosition = TilePositionFromMousePoint(mouseOnMap);

        float zPos = -0.1f;
        
        mouseLineRenderer.SetPosition(0, new Vector3(mapPosition.x, mapPosition.y, zPos));
        mouseLineRenderer.SetPosition(1, new Vector3(mapPosition.x + 1, mapPosition.y, zPos));
        mouseLineRenderer.SetPosition(2, new Vector3(mapPosition.x + 1, mapPosition.y + 1, zPos));
        mouseLineRenderer.SetPosition(3, new Vector3(mapPosition.x, mapPosition.y + 1, zPos));
        mouseLineRenderer.SetPosition(4, new Vector3(mapPosition.x, mapPosition.y, zPos));
        
        var tiles = tilemaps
                                    .Select(t => t.GetTile(mapPosition))
                                    .Where(t => t)
                                    .ToList();
        
        if (rightClickAction.IsPressed() && rightClickAction.triggered)
        {
            var tile = tiles.FirstOrDefault();
            selected = new Vector2(mapPosition.x, mapPosition.y);
            editorUI.SetCurrentlyEditedTile(selected, tile);
        }

        if (selected.HasValue)
        {
            var selectedVector = (Vector2)selected;
            selectedLineRenderer.SetPosition(0, new Vector3(selectedVector.x, selectedVector.y, zPos));
            selectedLineRenderer.SetPosition(1, new Vector3(selectedVector.x + 1, selectedVector.y, zPos));
            selectedLineRenderer.SetPosition(2, new Vector3(selectedVector.x + 1, selectedVector.y + 1, zPos));
            selectedLineRenderer.SetPosition(3, new Vector3(selectedVector.x, selectedVector.y + 1, zPos));
            selectedLineRenderer.SetPosition(4, new Vector3(selectedVector.x, selectedVector.y, zPos));
        }
        
        Vector2 moveValue = moveAction.ReadValue<Vector2>();
        var mapMoveSpeed = moveSpeed[currentMoveSpeed];

        if (shiftAction.IsPressed() && shiftAction.triggered)
        {
            currentMoveSpeed += 1;
            if (currentMoveSpeed >= moveSpeed.Length)
            {
                currentMoveSpeed = 0;
            }
            
            editorUI.SetScrollSpeedText(currentMoveSpeed, moveSpeed[currentMoveSpeed]);
        }

        if (moveValue.x != 0)
        {
            viewTarget.transform.position = new Vector3(viewTarget.transform.position.x + mapMoveSpeed * moveValue.x, viewTarget.transform.position.y, 0);
        }
        
        if (moveValue.y != 0)
        {
            viewTarget.transform.position = new Vector3(viewTarget.transform.position.x, viewTarget.transform.position.y + mapMoveSpeed * moveValue.y, 0);
        }
    }

    IEnumerator SetScrollSpeed()
    {
        yield return new WaitForSeconds(1);
        editorUI.SetScrollSpeedText(0, moveSpeed[0]);
    }

    private Vector3Int TilePositionFromMousePoint(Vector2 mouseOnMap)
    {
        var mapPosition = Vector3Int.zero;
        
        mapPosition = new Vector3Int((int)mouseOnMap.x, (int)mouseOnMap.y);
        
        if (mouseOnMap.x < 0)
        {
            mapPosition.x -= 1;
        }

        if (mouseOnMap.y < 0)
        {
            mapPosition.y -= 1;
        }

        return mapPosition;
    }

    public static bool IsEnabled()
    {
        return mapEditor && mapEditor.editorEnabled;
    }

    public static GameObject GetViewTarget()
    {
        return mapEditor.viewTarget;
    }*/
}
