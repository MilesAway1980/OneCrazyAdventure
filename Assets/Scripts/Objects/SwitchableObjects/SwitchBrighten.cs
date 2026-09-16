using UnityEngine;

public class SwitchBrighten : SwitchableObject
{
    public DarkTileMap darkTileMap;

    public Color brightenColorDarkest;
    public Color brightenColorLightest;

    private Color originalColorDarkest;
    private Color originalColorLightest;
    
    
    /*protected override void Start()
    {
        base.Start();
    }

    
    protected override void Update()
    {
        base.Update();
    }*/

    public override void Activate()
    {
        darkTileMap.darkest = brightenColorDarkest;
        darkTileMap.lightest = brightenColorLightest;
        base.Activate();
    }

    public override void Deactivate()
    {
        darkTileMap.darkest = originalColorDarkest;
        darkTileMap.lightest = originalColorLightest;
        base.Deactivate();
    }
}
