using UnityEngine;

// Puts something back where it began if it ends up somewhere it can't be got back from, such as a crate that is knocked off a ledge into a
// Kill Zone. Without it, whatever falls out of the world is gone for good, and if the player needed it, so is their way forward.
//
// It remembers where the object was when the game started: its place, and which way up it was. A Kill Zone does the sending back, when
// the object touches it (it needs a layer that collides with this object's, as it does with the characters). Anything else can send it
// back too, by calling ReturnToStart.
//
// Added by itself to anything carryable. To make something else come back, such as a pushable block, add it by hand.
[RequireComponent(typeof(Rigidbody2D))]
public class ReturnsToStart : MonoBehaviour
{
    [Tooltip("Where it comes back to, as set when the game starts: for watching, not for setting.")]
    public Vector2 startPosition;

    [Tooltip("The angle it comes back at, in degrees, as set when the game starts: for watching, not for setting.")]
    public float startAngle;

    private Rigidbody2D body;
    private CarryableObject carryable;

    public static void EnsureOn(GameObject thing)
    {
        // A character has its own way back.
        if (thing && !thing.GetComponent<ReturnsToStart>() && thing.GetComponent<Rigidbody2D>() && !thing.GetComponent<Characters.Character>())
        {
            thing.AddComponent<ReturnsToStart>();
        }
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        carryable = GetComponent<CarryableObject>();

        // Before anything has had the chance to move it. If it's added later, it's wherever it is then.
        startPosition = transform.position;
        startAngle = transform.eulerAngles.z;
    }

    // Whether it can be put back right now: only by the machine that is in charge of the game, as the others are shown where the host puts
    // it, and not while a character is holding it. It's joined to them then, so if they are in a Kill Zone it's they who are sent back,
    // and it goes with them.
    public bool CanReturn
    {
        get { return isActiveAndEnabled && !GameSession.IsClientOnly && !(carryable && carryable.IsCarried); }
    }

    // Puts it back at its starting place, at rest, at once, and on every other machine too. Returns whether it did.
    public bool ReturnToStart()
    {
        if (!CanReturn)
        {
            return false;
        }

        var place = new Vector3(startPosition.x, startPosition.y, transform.position.z);

        transform.SetPositionAndRotation(place, Quaternion.Euler(0f, 0f, startAngle));

        if (body)
        {
            body.position = startPosition;
            body.rotation = startAngle;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.WakeUp();
        }

        // Sent straight there, rather than sliding across the world from where it fell.
        var sync = GetComponent<Mirror.NetworkTransformBase>();

        if (sync && sync.isServer)
        {
            sync.ServerTeleport(transform.position, transform.rotation);
        }

        return true;
    }

    void OnDrawGizmosSelected()
    {
        // Where it will come back to, once the game is running. Before then it's just where it is.
        var at = Application.isPlaying ? (Vector3)startPosition : transform.position;

        Gizmos.color = new Color(0.3f, 0.9f, 1f, 1f);
        Gizmos.DrawWireSphere(at, 0.3f);
    }
}
