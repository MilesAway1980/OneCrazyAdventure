using UnityEngine;

public class DarkTileMap : TileColorChange
{
    protected override void Start()
    {
        //print("Dark Tile Map Start");
        emanatingGameObjects = FindObjectsByType<BrightTileSource>(FindObjectsSortMode.None);
        base.Start();
    }

    /*
    protected override void Update()
    {
        base.Update();
    }
    */
}
