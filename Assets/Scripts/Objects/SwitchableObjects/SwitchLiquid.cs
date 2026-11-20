using UnityEngine;

// Raises and lowers the liquid in a Liquid Area when it is switched on and off: a pool that drains when a lever is thrown, or fills while a
// button is held. A button, a lever or anything else that works switchable objects operates it.
//
// The liquid is the part of the area below a level surface, and it's switched between two levels as a fraction of the area's height, from
// its lowest point to its highest:
//  - On Level is how full it is when this is switched on. It's 100 percent by default: the area as it was drawn.
//  - Off Level is how full it is when this is switched off. It's 0 by default, so the liquid sinks from the top of the area to the bottom.
//  Swap them (On Level 0, Off Level 1) for a pool that is full to begin with and drains when it's switched on.
//
// It moves the surface at Rise Speed, and at Lower Speed going down, if that's set. Anything in the liquid, such as a floating crate or a
// character, responds as the liquid goes, since what's liquid changes with the level.
//
//  - With a button, tick Stays Active When On on the button, so that it keeps telling this while it's pressed. The level goes back when
//    it's let go.
//  - A button set to Allow Partial Presses sets the level by how far down it's pressed: half pressed is half way between Off Level and On
//    Level.
//  - With a lever, the level is raised when the lever is thrown, and lowered when it's thrown back.
//  - Stay Switched On, on this, leaves it at On Level once it has been switched on. Switch Off Time sends it back that many seconds after
//    the last time it was told.
//
// Put it on the Liquid Area's object, or give it the area to work. It runs on the host: another machine is sent the level
// (LiquidLevelNetwork, added by Tools > Network > Make World Networked).
public class SwitchLiquid : SwitchableObject
{
    [Tooltip("The Liquid Area to raise and lower. If it's empty, the one on this object.")]
    public LiquidArea area;

    [Range(0f, 1f)]
    [Tooltip("How full the area is when this is switched on, as a fraction of its height: 1 is the whole area, as drawn.")]
    public float onLevel = 1f;

    [Range(0f, 1f)]
    [Tooltip("How full the area is when this is switched off, as a fraction of its height: 0 is empty. It starts the game here.")]
    public float offLevel = 0f;

    [Min(0.01f)]
    [Tooltip("How fast the surface rises, in units a second.")]
    public float riseSpeed = 1f;

    [Min(0f)]
    [Tooltip("How fast the surface sinks, in units a second. 0 is the same as Rise Speed.")]
    public float lowerSpeed = 0f;

    // How full it is now, as a fraction of the area's height.
    private float level;

    // How far on it's wanted, from 0 (off) to 1 (on).
    private float wantedAmount;
    private float lastActivated = float.NegativeInfinity;

    private LiquidArea Target
    {
        get
        {
            if (!area)
            {
                area = GetComponent<LiquidArea>();
            }

            return area;
        }
    }

    // How full it is now, for the network to send.
    public float Level
    {
        get { return level; }
    }

    protected override void Start()
    {
        base.Start();

        if (!Target)
        {
            Debug.LogWarning($"{name}: the Switch Liquid has no Liquid Area to work. Put it on the area's object, or give it one.", this);
            return;
        }

        // Off, until it's switched on.
        level = offLevel;
        Apply();
    }

    // Not the base class's Update, which switches the object off again by itself every frame unless something keeps switching it on.
    // This one only goes by being told.
    protected override void Update()
    {
        // In someone else's game the host decides, and the level comes from the network.
        if (GameSession.IsClientOnly || !Target)
        {
            return;
        }

        if (!staySwitchedOn && switchOffTime > 0f && wantedAmount > 0f && Time.time - lastActivated >= switchOffTime)
        {
            wantedAmount = 0f;
        }

        var goal = Mathf.Lerp(offLevel, onLevel, wantedAmount);
        var height = Mathf.Max(0.01f, Target.WorldRect.height);
        var rising = goal > level;
        var speed = rising || lowerSpeed <= 0f ? riseSpeed : lowerSpeed;

        level = Mathf.MoveTowards(level, goal, speed / height * Time.deltaTime);
        Apply();
    }

    // On a machine that has joined someone else's game: the level the host says it is.
    public void SetLevelFromNetwork(float newLevel)
    {
        level = Mathf.Clamp01(newLevel);
        Apply();
    }

    private void Apply()
    {
        if (Target)
        {
            Target.Fill = level;
        }
    }

    public override void Activate()
    {
        base.Activate();

        wantedAmount = 1f;
        lastActivated = Time.time;
    }

    public override bool AcceptsAmount
    {
        get { return true; }
    }

    // Takes it that fraction of the way from Off Level to On Level: a button that is half pressed raises it half way. One that's let go
    // sends it back, unless it stays switched on, which keeps it as far as it has been.
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
    }

    public override void Deactivate()
    {
        base.Deactivate();

        if (!staySwitchedOn)
        {
            wantedAmount = 0f;
        }
    }

    // The arrows to this point at the middle of the area it works.
    public override Vector3 GizmoPoint
    {
        get
        {
            var shown = area ? area : GetComponent<LiquidArea>();

            return shown ? (Vector3)shown.WorldRect.center : transform.position;
        }
    }

    // Where the surface is at each of the two levels, across the area, in the scene view while this is selected: blue for on, white for off.
    void OnDrawGizmosSelected()
    {
        var shown = area ? area : GetComponent<LiquidArea>();

        if (!shown)
        {
            return;
        }

        var rect = shown.WorldRect;

        Gizmos.color = new Color(0.3f, 0.7f, 1f, 1f);
        DrawLevel(rect, onLevel);

        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        DrawLevel(rect, offLevel);
    }

    private static void DrawLevel(Rect rect, float fraction)
    {
        var y = Mathf.Lerp(rect.yMin, rect.yMax, fraction);

        Gizmos.DrawLine(new Vector3(rect.xMin, y, 0f), new Vector3(rect.xMax, y, 0f));
    }
}
