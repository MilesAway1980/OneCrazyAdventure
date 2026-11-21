using UnityEngine;

// Lets a lever or a button light a LightSource, put it out, or turn it up and down: a sconce that is lit by a switch, or a row of them that
// light one after another (give the switch a Delay Between Objects).
//
// The light's own settings say how it looks. Its **Unlit** look is the lowest it goes, and its **Lit** look is the brightest, each with its
// own radius, intensity, pulsing and colour, and Fade Seconds says how long it takes to change. This only says which of the two the switch
// shows, and how far between:
//  - Off Level is how lit the light is while this is switched off. 0 is the unlit look, which is also what it starts the game as. 1 is the
//    lit look, and in between it's a mix of the two, so a switch can turn a light down instead of out.
//  - On Level is how lit it is while this is switched on. 1 is the lit look. A level of 0 turns it out when switched on.
//  - A button that passes on how far it's pressed, with Allow Partial Presses, sets the level by how far, anywhere between the two.
//
// Put it on the light's object, or give it the light. Stay Switched On and Switch Off Time work as they do on a door. A light's Time Until
// Go Out still puts it out by itself when its time is up. In a network game the host does it, and the light's network script
// (LightSourceNetwork) sends how lit it is, so every machine shows it.
public class SwitchLight : SwitchableObject
{
    [Tooltip("The light to switch. If it's empty, the LightSource on this object. Its Unlit and Lit looks are the lowest and the brightest it goes.")]
    public LightSource lightSource;

    [Range(0f, 1f)]
    [Tooltip("How lit the light is while this is switched off: 0 is its Unlit look, 1 is its Lit look, and in between is a mix of the two. It starts the game like this.")]
    public float offLevel = 0f;

    [Range(0f, 1f)]
    [Tooltip("How lit the light is while this is switched on: 1 is its Lit look, 0 is its Unlit look, and in between is a mix of the two.")]
    public float onLevel = 1f;

    [Min(-1f)]
    [Tooltip("How long, in seconds, it takes to fade from one level to another. Under 0 (the default) uses the light's own Fade Seconds. 0 changes at once.")]
    public float fadeSeconds = -1f;

    // How far on it's wanted, from 0 (off) to 1 (on), and the level last given to the light.
    private float wantedAmount;
    private float lastActivated = float.NegativeInfinity;
    private float givenLevel = -1f;

    private LightSource Target
    {
        get
        {
            if (!lightSource)
            {
                lightSource = GetComponent<LightSource>();
            }

            return lightSource;
        }
    }

    protected override void Start()
    {
        base.Start();

        if (!Target)
        {
            Debug.LogWarning($"{name}: the Switch Light has no LightSource to switch. Put it on the light's object, or give it one.", this);
            return;
        }

        // Switched off, until it's switched on.
        Apply();
    }

    // Not the base class's Update, which switches the object off again by itself every frame unless something keeps switching it on. This
    // one only goes by being told.
    protected override void Update()
    {
        // In someone else's game the host decides how lit it is, and says so.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        if (!staySwitchedOn && switchOffTime > 0f && wantedAmount > 0f && Time.time - lastActivated >= switchOffTime)
        {
            wantedAmount = 0f;
            Apply();
        }
    }

    // Gives the light the level this comes to, if it's changed.
    private void Apply()
    {
        var light = Target;

        if (!light)
        {
            return;
        }

        var level = Mathf.Lerp(offLevel, onLevel, wantedAmount);

        if (Mathf.Approximately(level, givenLevel))
        {
            return;
        }

        givenLevel = level;
        light.SetLevel(level, fadeSeconds >= 0f ? fadeSeconds : (float?)null);
    }

    public override void Activate()
    {
        base.Activate();

        wantedAmount = 1f;
        lastActivated = Time.time;
        Apply();
    }

    public override bool AcceptsAmount
    {
        get { return true; }
    }

    // Takes the light that fraction of the way from Off Level to On Level: a button that is half pressed gives a light half way between.
    // One that's let go sends it back, unless it stays switched on, which keeps it as far as it has been.
    public override void SetAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (amount <= 0f)
        {
            Deactivate();
            return;
        }

        base.Activate();

        wantedAmount = staySwitchedOn ? Mathf.Max(wantedAmount, amount) : amount;
        lastActivated = Time.time;
        Apply();
    }

    public override void Deactivate()
    {
        base.Deactivate();

        if (!staySwitchedOn)
        {
            wantedAmount = 0f;
            Apply();
        }
    }

    // The arrows to this point at the light.
    public override Vector3 GizmoPoint
    {
        get
        {
            var light = lightSource ? lightSource : GetComponent<LightSource>();

            return light ? light.transform.position : transform.position;
        }
    }
}
