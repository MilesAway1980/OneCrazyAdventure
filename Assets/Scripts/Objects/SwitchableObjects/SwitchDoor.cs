using UnityEngine;

// A door, gate or any wall that moves out of the way when it is switched on, and back when it's switched off. A button,
// a lever or anything else that works switchable objects operates it.
//
//  - With a button, tick Stays Active When On on the button, so that it keeps telling the door while it's pressed. The door
//    closes again once it is let go.
//  - A button set to Allow Partial Presses opens it by how far down the button is pressed: a button pressed 40 percent of the way
//    opens it 40 percent of the way.
//  - With a lever, the door opens when the lever is thrown, and closes when it's thrown back. A lever that is One Time
//    leaves it open.
//  - Use Own Speed, if it's turned off, takes it out of the door's hands: it goes where the switch says, one to one, the fraction the switch is on.
//  - Stay Switched On, on this, leaves it open once it has been opened. Switch Off Time closes it that many seconds after the
//    last time it was told to open.
//
// It moves by its own body if it has a kinematic Rigidbody2D, so anything standing on it is carried, and by its transform if
// not. The way it moves is shown in the scene view when it is selected.
public class SwitchDoor : SwitchableObject
{
    [Tooltip("What moves. If it's empty, this object moves.")]
    public Transform door;

    [Tooltip("How far, and which way, it moves when it opens, in world units from where it starts. A door that rises into the ceiling might go (0, 3).")]
    public Vector2 openOffset = new Vector2(0f, 3f);

    [Tooltip("On: it opens and closes at its own Speed, whatever the switch does. Off: it goes exactly with the switch, one to one, so it is open by the same " +
             "fraction as the switch is on: a lever half way through its swing has it half open, and a button pressed a quarter of the way down has it a " +
             "quarter open (with or without Allow Partial Presses on the button). It has no speed of its own then, and moves as fast as the switch " +
             "does, so a lever that swings slowly opens it slowly. A switch that is only on or off (a walk-in area) has no halfway to follow, so it " +
             "takes the door all the way at once.")]
    public bool useOwnSpeed = true;

    [Min(0.01f)]
    [Tooltip("How fast, in units per second, it opens and closes. Only used when Use Own Speed is on.")]
    public float speed = 4f;

    private Vector2 closedPosition;
    private Vector2 openPosition;
    private Rigidbody2D body;

    private bool wantsOpen;

    // How much of the way it opens, from 0 to 1, when it's wanted open. 1 unless a button that passes on an amount says otherwise.
    private float openAmount = 1f;
    private float lastActivated = float.NegativeInfinity;

    protected override void Start()
    {
        base.Start();

        if (!door)
        {
            door = transform;
        }

        body = door.GetComponent<Rigidbody2D>();

        closedPosition = body ? body.position : (Vector2)door.position;
        openPosition = closedPosition + openOffset;
    }

    // Not the base class's Update, which switches the object off again by itself every frame unless something keeps switching
    // it on. This one only goes by being told.
    protected override void Update()
    {
        // In someone else's game the host decides, and where the door is comes from the network.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        if (!staySwitchedOn && switchOffTime > 0f && wantsOpen && Time.time - lastActivated >= switchOffTime)
        {
            wantsOpen = false;
        }
    }

    void FixedUpdate()
    {
        if (GameSession.IsClientOnly)
        {
            return;
        }

        var current = body ? body.position : (Vector2)door.position;
        var goal = wantsOpen ? Vector2.Lerp(closedPosition, openPosition, openAmount) : closedPosition;

        // Its own speed, or exactly where the switch says.
        var next = useOwnSpeed ? Vector2.MoveTowards(current, goal, speed * Time.fixedDeltaTime) : goal;

        if (body)
        {
            body.MovePosition(next);
        }
        else
        {
            door.position = new Vector3(next.x, next.y, door.position.z);
        }
    }

    public override void Activate()
    {
        base.Activate();

        wantsOpen = true;
        openAmount = 1f;
        lastActivated = Time.time;
    }

    public override bool AcceptsAmount
    {
        get { return true; }
    }

    public override bool FollowsSwitchPosition
    {
        get { return !useOwnSpeed; }
    }

    // Opens it that fraction of the way: a button that is half pressed opens it half way. One that's let go closes it again, unless
    // it stays switched on, which keeps it as open as it has been.
    public override void SetAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (amount <= 0f)
        {
            Deactivate();
            return;
        }

        base.Activate();

        wantsOpen = true;
        openAmount = staySwitchedOn ? Mathf.Max(openAmount, amount) : amount;
        lastActivated = Time.time;
    }

    public override void Deactivate()
    {
        base.Deactivate();

        if (!staySwitchedOn)
        {
            wantsOpen = false;
        }
    }

    public override Vector3 GizmoPoint
    {
        get { return door ? door.position : transform.position; }
    }

    void OnDrawGizmosSelected()
    {
        var start = door ? (Vector2)door.position : (Vector2)transform.position;

        // Where it goes when it's open, drawn from where it is now, or where it started if the game is running.
        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, start + openOffset);
        Gizmos.DrawWireSphere(start + openOffset, 0.2f);
    }
}
