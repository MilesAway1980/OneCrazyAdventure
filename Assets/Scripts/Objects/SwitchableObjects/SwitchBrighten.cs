using UnityEngine;

public class SwitchBrighten : SwitchableObject
{
    public DarkTileMap darkTileMap;

    public Color brightenColorDarkest;
    public Color brightenColorLightest;

    // Whether this switch is the one currently holding the scene brighter.
    private bool brightening;

    // Held brighter by an amount, which a button that passes on how far it's pressed keeps telling it, and ends by letting go.
    private bool holdingAmount;
    private float amountShown;

    public override void Activate()
    {
        if (darkTileMap)
        {
            darkTileMap.darkest = brightenColorDarkest;
            darkTileMap.lightest = brightenColorLightest;
            brightening = true;
            amountShown = 1f;
        }

        base.Activate();
    }

    public override bool AcceptsAmount
    {
        get { return true; }
    }

    // Brightens the scene that fraction of the way: a button that is half pressed gives half the change. One that's let go puts
    // it back, unless it stays switched on, which keeps it as bright as it has been.
    public override void SetAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (amount <= 0f)
        {
            Deactivate();
            return;
        }

        if (darkTileMap)
        {
            amountShown = staySwitchedOn ? Mathf.Max(amountShown, amount) : amount;
            darkTileMap.BlendColors(brightenColorDarkest, brightenColorLightest, amountShown);
            brightening = true;
        }

        holdingAmount = true;
        base.Activate();
    }

    // While it's held by an amount, it isn't switched off by itself every frame: the button says when to stop.
    protected override void Update()
    {
        if (holdingAmount)
        {
            return;
        }

        base.Update();
    }

    public override void Deactivate()
    {
        holdingAmount = false;
        base.Deactivate();

        // The base only switches off once its timer or "stay on" setting allows it.
        if (brightening && !activated)
        {
            darkTileMap.RestoreOriginalColors();
            brightening = false;
            amountShown = 0f;
        }
    }
}
