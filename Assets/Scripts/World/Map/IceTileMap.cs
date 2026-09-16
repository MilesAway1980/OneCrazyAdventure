using System.Collections.Generic;
using UnityEngine;

public class IceTileMap : TileColorChange
{
    public float minBrightOffset;
    public float maxBrightOffset;
    public float glowSpeedSeconds;

    protected override void Start()
    {
        
        //TODO differentiate bright tile sources so that dark rooms aren't brightened by heat sources and visa versa.
        
        var heatSources = FindObjectsByType<HeatSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        var ego = new List<BrightTileSource>();
        foreach (var heatSource in heatSources)
        {
            var bts = heatSource.gameObject.AddComponent<BrightTileSource>();
            bts.minBrightness = heatSource.GetRange() + minBrightOffset;
            bts.maxBrightness = heatSource.GetRange() + maxBrightOffset;
            bts.glowSpeedSeconds = glowSpeedSeconds;
            ego.Add(bts);
        }
        
        emanatingGameObjects = ego;
        
        base.Start();
    }

    /*
    protected override void Update()
    {
        base.Update();
    }
    */
}