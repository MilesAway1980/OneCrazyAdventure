using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
//using UnityEngine.UI;
using UnityEngine.UIElements;

public class EditorUI : MonoBehaviour
{
    /*private UIDocument editorUIDocument;
    private Button saveButton;
    private Button loadButton;

    private Toggle collapseToggle;
    private Toggle magneticToggle;
    private Toggle rollToggle;
    private Toggle woodToggle;
    private Toggle diggableToggle;

    private FloatField collapseTimeField;
    private FloatField collapseRepairTimeField;

    private FloatField magneticStrengthField;
    private FloatField magneticDistanceField;

    private FloatField rollSpeedNeededField;
    private FloatField rollRepairTimeField;

    private FloatField woodCutTimeField;
    private FloatField woodRepairTimeField;

    private FloatField diggableHardnessField;
    private FloatField diggableRepairTimeField;
    
    private Label tileText;
    private Label scrollSpeedText;

    //private MapEditor mapEditor;

    private Vector2? currentTile;
    private bool visible = false;
    
    private Dictionary<Vector2, MapTileData> tilemapData;
    private string mapData;

    private Camera cam;

    public Vector2 camOffset;
    
    void Start()
    {
        cam = Camera.main;
        mapData = Application.dataPath + "/MapData.txt";
        //print ("mapData: " + mapData);
        //mapEditor = GetComponent<MapEditor>();
        editorUIDocument = GetComponentInChildren<UIDocument>();
        
        saveButton = editorUIDocument.rootVisualElement.Q<Button>("save-button");
        loadButton = editorUIDocument.rootVisualElement.Q<Button>("load-button");
        
        collapseToggle = editorUIDocument.rootVisualElement.Q<Toggle>("collapse-toggle");
        magneticToggle = editorUIDocument.rootVisualElement.Q<Toggle>("magnetic-toggle");
        rollToggle = editorUIDocument.rootVisualElement.Q<Toggle>("roll-toggle");
        woodToggle = editorUIDocument.rootVisualElement.Q<Toggle>("wood-toggle");
        diggableToggle = editorUIDocument.rootVisualElement.Q<Toggle>("diggable-toggle");
        
        collapseTimeField = editorUIDocument.rootVisualElement.Q<FloatField>("collapse-toggle-time-until-collapse");
        collapseRepairTimeField = editorUIDocument.rootVisualElement.Q<FloatField>("collapse-toggle-time-until-repair");

        magneticStrengthField = editorUIDocument.rootVisualElement.Q<FloatField>("magnetic-toggle-strength");
        magneticDistanceField = editorUIDocument.rootVisualElement.Q<FloatField>("magnetic-toggle-distance");

        rollSpeedNeededField = editorUIDocument.rootVisualElement.Q<FloatField>("roll-toggle-speed-needed");
        rollRepairTimeField = editorUIDocument.rootVisualElement.Q<FloatField>("roll-toggle-time-until-repair");

        woodCutTimeField = editorUIDocument.rootVisualElement.Q<FloatField>("wood-toggle-cut-time");
        woodRepairTimeField = editorUIDocument.rootVisualElement.Q<FloatField>("wood-toggle-time-until-repair");

        diggableHardnessField = editorUIDocument.rootVisualElement.Q<FloatField>("diggable-toggle-dig-time");
        diggableRepairTimeField = editorUIDocument.rootVisualElement.Q<FloatField>("diggable-toggle-time-until-repair");

        tileText = editorUIDocument.rootVisualElement.Q<Label>("tile-text");
        tileText.text = "Tile: -, -";

        scrollSpeedText = editorUIDocument.rootVisualElement.Q<Label>("scroll-speed-text");

        collapseToggle.toggleOnLabelClick = true;
        magneticToggle.toggleOnLabelClick = true;
        rollToggle.toggleOnLabelClick = true;
        woodToggle.toggleOnLabelClick = true;
        
        collapseToggle.RegisterCallback<ChangeEvent<bool>>(t => ToggleClicked("collapse", t));
        magneticToggle.RegisterCallback<ChangeEvent<bool>>(t => ToggleClicked("magnetic", t));
        rollToggle.RegisterCallback<ChangeEvent<bool>>(t => ToggleClicked("roll", t));
        woodToggle.RegisterCallback<ChangeEvent<bool>>(t => ToggleClicked("wood", t));
        diggableToggle.RegisterCallback<ChangeEvent<bool>>(t => ToggleClicked("diggable", t));
        
        collapseTimeField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("collapseTime", t));
        collapseRepairTimeField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("collapseRepairTime", t));
        magneticStrengthField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("magneticStrength", t));
        magneticDistanceField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("magneticDistance", t));
        rollSpeedNeededField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("rollSpeedNeeded", t));
        rollRepairTimeField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("rollRepairTime", t));
        woodCutTimeField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("woodCutTime", t));
        woodRepairTimeField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("woodRepairTime", t));
        diggableHardnessField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("diggableHardness", t));
        diggableRepairTimeField.RegisterCallback<ChangeEvent<float>>(t => NumberChanged("diggableRepairTime", t));
        
        saveButton.clicked += SaveTileData;
        loadButton.clicked += LoadTileData;
        
        tilemapData = new Dictionary<Vector2, MapTileData>();
        LoadTileData();
    }

    void Update()
    {
        //print (collapseTimeField.value);
        if (visible)
        {
            transform.position = new Vector3(cam.transform.position.x + camOffset.x, cam.transform.position.y + camOffset.y, 0);
        }
        else
        {
            transform.position = new Vector3(cam.transform.position.x - 1000, cam.transform.position.y - 1000, 0);
        }
        
        //var collapseTime = editorUIDocument.rootVisualElement.Q<FloatField>("collapse-toggle-time-until-collapse");
        //var repairTime = editorUIDocument.rootVisualElement.Q<FloatField>("collapse-toggle-time-until-collapse");
    }

    void SaveTileData()
    {
        var keys = tilemapData.Keys;
        using (StreamWriter writer = new StreamWriter(mapData))
        {
            foreach (var key in keys)
            {
                var mapData = tilemapData[key];
                if (mapData.HasValues())
                {
                    var value = mapData.GetValueString();
                    writer.WriteLine($"{key.x},{key.y},{value}");
                }
            }
        }
    }

    void LoadTileData()
    {
        tilemapData.Clear();
        using (StreamReader reader = new StreamReader(mapData))
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null)
                {
                    var (pos, mapTileData) = MapTileData.LoadTileData(line);

                    if (tilemapData.ContainsKey(pos))
                    {
                        print ("ERROR: Duplicate tilemap data found: " + line);
                        continue;
                    }
                    
                    tilemapData[pos] = mapTileData;
                }
            }
        }
    }

    void NumberChanged(string floatFieldName, ChangeEvent<float> field)
    {
        if (!currentTile.HasValue)
        {
            return;
        }
        
        Vector2 cur = (Vector2)currentTile;
        if (!tilemapData.ContainsKey(cur))
        {
            tilemapData[cur] = new MapTileData();
        }
            
        var data = tilemapData[cur];
        
        switch (floatFieldName)
        {
            case "collapseTime":
                data.collapseTime = field.newValue;
                break;
            case "collapseRepairTime":
                data.collapseRepairTime = field.newValue;
                break;
            case "magneticStrength":
                data.magneticStrength = field.newValue;
                break;
            case "magneticDistance":
                data.magneticDistance = field.newValue;
                break;
            case "rollSpeedNeeded":
                data.rollSpeedNeeded = field.newValue;
                break;
            case "rollRepairTime":
                data.rollRepairTime = field.newValue;
                break;
            case "woodCutTime":
                data.woodCutTime = field.newValue;
                break;
            case "woodRepairTime":
                data.woodRepairTime = field.newValue;
                break;
            case "diggableHardness":
                data.diggableHardness = field.newValue;
                break;
            case "diggableRepairTime":
                data.diggableRepairTime = field.newValue;
                break;
        }
    }

    void ToggleClicked(string toggleName, ChangeEvent<bool> toggle)
    {
        if (!currentTile.HasValue)
        {
            return;
        }

        Vector2 cur = (Vector2)currentTile;
        if (!tilemapData.ContainsKey(cur))
        {
            tilemapData[cur] = new MapTileData();
        }
            
        var data = tilemapData[cur];

        switch (toggleName)
        {
            case "collapse":
                data.collapse = toggle.newValue;
                break;
            case "magnetic":
                data.magnetic = toggle.newValue;
                break;
            case "roll":
                data.rollable = toggle.newValue;
                break;
            case "wood":
                data.wood = toggle.newValue;
                break;
            case "diggable":
                data.diggable = toggle.newValue;
                break;
        }
        
        tilemapData[cur] = data;
        
        //print($"Tile {currentTile} {toggleName} is now {toggle.newValue}");
    }
    
    public void SetCurrentlyEditedTile(Vector2? tilePos, TileBase tile)
    {
        if (!tile || !tilePos.HasValue)
        {
            currentTile = null;
            SetTile();
            tileText.text = "Tile: -, -";
            return;
        }

        var selected = (Vector2)tilePos;
        tileText.text = $"Tile: ({selected.x}, {selected.y})";
        currentTile = tilePos;

        if (tilemapData.ContainsKey((Vector2)tilePos))
        {
            var tileData = tilemapData[(Vector2)tilePos];
            SetTile(tileData);
        }
        else
        {
            SetTile();
        }
    }

    private void SetTile(MapTileData newData = null)
    {
        if (newData == null)
        {
            print ("ERROR: No tile data provided");
            newData = new MapTileData()
            {
                collapse = false,
                magnetic = false,
                rollable = false,
                wood = false,
                diggable = false,

                collapseTime = 0,
                collapseRepairTime = -1,

                magneticStrength = 0,
                magneticDistance = 0,

                rollSpeedNeeded = 0,
                rollRepairTime = -1,

                woodCutTime = 0,
                woodRepairTime = -1,

                diggableHardness = 0,
                diggableRepairTime = -1
            };
        }
        
        collapseToggle.value = newData.collapse;
        magneticToggle.value = newData.magnetic;
        rollToggle.value = newData.rollable;
        woodToggle.value = newData.wood;
        diggableToggle.value = newData.diggable;
        
        collapseTimeField.value = newData.collapseTime;
        collapseRepairTimeField.value = newData.collapseRepairTime;
        
        magneticStrengthField.value = newData.magneticStrength;
        magneticDistanceField.value = newData.magneticDistance;
        
        rollSpeedNeededField.value = newData.rollSpeedNeeded;
        rollRepairTimeField.value = newData.rollRepairTime;
        
        woodCutTimeField.value = newData.woodCutTime;
        woodRepairTimeField.value = newData.woodRepairTime;
        
        diggableHardnessField.value = newData.diggableHardness;
        diggableHardnessField.value = newData.diggableHardness;
        diggableRepairTimeField.value = newData.diggableRepairTime;
    }

    public void Enable()
    {
        visible = true;
    }

    public void Disable()
    {
        visible = false;
    }

    public void SetScrollSpeedText(int index, float speed)
    {
        scrollSpeedText.text = $"Scroll Speed {index + 1}: {speed}";
    }*/
}
