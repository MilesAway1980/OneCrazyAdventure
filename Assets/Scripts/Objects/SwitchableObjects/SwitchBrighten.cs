using UnityEngine;

public class SwitchBrighten : SwitchableObject
{
    public DarkTileMap darkTileMap;

    public Color brightenColorDarkest;
    public Color brightenColorLightest;

    // Whether this switch is the one currently holding the scene brighter.
    private bool brightening;

    public override void Activate()
    {
        if (darkTileMap)
        {
            darkTileMap.darkest = brightenColorDarkest;
            darkTileMap.lightest = brightenColorLightest;
            brightening = true;
        }

        base.Activate();
    }

    public override void Deactivate()
    {
        base.Deactivate();

        // The base only switches off once its timer or "stay on" setting allows it.
        if (brightening && !activated)
        {
            darkTileMap.RestoreOriginalColors();
            brightening = false;
        }
    }
}
