using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileColorChange : MonoBehaviour
{
    public Color darkest;
    public Color lightest;
    public float colorChangeSpeed;
    public Tilemap tilemap;

    public float glowDiffuse;

    public bool limitToObjectBounds;

    private float glowStartTime;
    private bool glowUp;
    
    private float redDiff;
    private float blueDiff;
    private float greenDiff;
    private float alphaDiff;

    private Dictionary<Vector3Int, Color> tileColors;
    private List<Vector3Int> tileColorKeys;

    protected IEnumerable<BrightTileSource> emanatingGameObjects;

    //private bool processedKeys = false;
    
    protected virtual void Start()
    {
        var mapBounds = tilemap.cellBounds;

        tileColors = new Dictionary<Vector3Int, Color>();

        float leftX = transform.position.x - transform.localScale.x * 0.5f;
        float rightX = transform.position.x + transform.localScale.x * 0.5f;
        
        float upperY = transform.position.y - transform.localScale.y * 0.5f;
        float lowerY = transform.position.y + transform.localScale.y * 0.5f;
        
        for (int x = mapBounds.xMin; x < mapBounds.xMax; x++)
        {
            for (int y = mapBounds.yMin; y < mapBounds.yMax; y++)
            {
                if (limitToObjectBounds)
                {
                    if (
                        x < leftX ||
                        x >= rightX ||
                        y < upperY ||
                        y >= lowerY
                       )
                    {
                        continue;
                    }
                }

                var pos = new Vector3Int(x, y, 0);

                if (tilemap.HasTile(pos))
                {
                    tilemap.SetTileFlags(pos, TileFlags.None);  //Sets the tile flags to editable
                    //tileColors[pos] = new Color();
                    tileColors[pos] = darkest;
                }
            }
        }
        
        tileColorKeys = tileColors.Keys.ToList();
        //processedKeys = true;

        glowStartTime = Time.realtimeSinceStartup;
        tilemap.color = Color.white;
        glowUp = true;
        
        redDiff = Mathf.Abs(darkest.r - lightest.r);
        blueDiff = Mathf.Abs(darkest.b - lightest.b);
        greenDiff = Mathf.Abs(darkest.g - lightest.g);
        alphaDiff = Mathf.Abs(darkest.a - lightest.a);

        colorChangeSpeed = Mathf.Abs(colorChangeSpeed);
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        MapGlow();
        GameObjectsGlow();
        DrawColors();
    }

    void MapGlow()
    {
        float diff;
        if (colorChangeSpeed > 0)
        {
            float changeTime = Time.realtimeSinceStartup - glowStartTime;

            if (changeTime >= colorChangeSpeed)
            {
                glowStartTime = Time.realtimeSinceStartup;
                glowUp = !glowUp;
                changeTime = 0;
            }

            diff = changeTime / colorChangeSpeed;
        }
        else
        {
            diff = 1;
        }

        float red, green, blue, alpha;

        if (glowUp)
        {
            red = darkest.r + redDiff * diff;
            green = darkest.g + greenDiff * diff;
            blue = darkest.b + blueDiff * diff;
            alpha = darkest.a + alphaDiff * diff;
        }
        else
        {
            red = lightest.r - redDiff * diff;
            green = lightest.g - greenDiff * diff;
            blue = lightest.b - blueDiff * diff;
            alpha = lightest.a - alphaDiff * diff;
        }

        //print($"{glowUp} {changeTime} {(int)(red * 255)}, {(int)(green * 255)}, {(int)(blue * 255)}, {(int)(alpha * 255)}");

        //darkTilemap.color = new Color(red, green, blue, alpha);
        var newColor = new Color(red, green, blue, alpha);
        
        foreach (var key in tileColorKeys)
        {
            tileColors[key] = newColor;
        }
    }

    void GameObjectsGlow()
    {
        if (emanatingGameObjects == null)
        {
            return;
        }

        foreach (var key in tileColorKeys)
        {
            foreach (var ego in emanatingGameObjects)
            {
                var glowDistance = ego.GetIntensity();
                var dist = Vector3.Distance(ego.transform.position, key);

                if (dist < glowDistance)
                {
                    var curColor = tileColors[key];
                    var diff = (glowDistance - dist) * glowDiffuse;
                    var newColor = new Color(curColor.r + diff, curColor.g + diff, curColor.b + diff, curColor.a );
                    tileColors[key] = newColor;
                }
            }
        }
    }

    void DrawColors()
    {
        foreach (var tileColor in tileColors)
        {
            var pos = tileColor.Key;
            var color = tileColor.Value;

            tilemap.SetColor(pos, color);
        }
    }
}

/*
 *      3, 6            12, 6
 *
 *                8, 2  
 *
 *      3, -3           12, -3
 */
