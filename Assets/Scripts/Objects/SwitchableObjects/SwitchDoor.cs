using UnityEngine;

// A door, gate or any wall that moves out of the way when it is switched on, and back when it's switched off. A button,
// a lever or anything else that works switchable objects operates it.
//
//  - With a button, tick Stays Active When On on the button, so that it keeps telling the door while it's pressed. The door
//    closes again once it is let go.
//  - With a lever, the door opens when the lever is thrown, and closes when it's thrown back. A lever that is One Time
//    leaves it open.
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

    [Min(0.01f)]
    [Tooltip("How fast, in units per second, it opens and closes.")]
    public float speed = 4f;

    private Vector2 closedPosition;
    private Vector2 openPosition;
    private Rigidbody2D body;

    private bool wantsOpen;
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
        var next = Vector2.MoveTowards(current, wantsOpen ? openPosition : closedPosition, speed * Time.fixedDeltaTime);

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

    void OnDrawGizmosSelected()
    {
        var start = door ? (Vector2)door.position : (Vector2)transform.position;

        // Where it goes when it's open, drawn from where it is now, or where it started if the game is running.
        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, start + openOffset);
        Gizmos.DrawWireSphere(start + openOffset, 0.2f);
    }
}
