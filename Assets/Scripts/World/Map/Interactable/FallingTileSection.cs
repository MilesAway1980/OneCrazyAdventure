using System.Collections.Generic;
using UnityEngine;

// A platform on a spring. Whatever stands on it pushes it down by its weight, and it settles lower the more there is on it,
// then springs back up when the weight is taken off. The spring, the weight and how heavy the platform itself is are all set
// here rather than coming from its Rigidbody2D, so they can be tuned without fighting the physics.
//
// It's moved by hand, so it is turned into a kinematic body when the game starts, and nothing pushes it but the weight on it.
[RequireComponent(typeof(Rigidbody2D))]
public class FallingTileSection : MonoBehaviour
{
    [Min(0.01f)]
    [Tooltip("How heavy the platform itself is. A heavier platform is slower to move and slower to settle, and swings more " +
             "slowly if it is bouncy. It doesn't change how far a weight pushes it down, which is decided by the stiffness.")]
    public float mass = 2f;

    [Min(0f)]
    [Tooltip("How hard the spring pulls it back for each unit it is away from where it started. A stiffer spring sinks less " +
             "under the same weight, and is quicker to come back. It sinks by the weight on it, times the weight's push, " +
             "divided by this.")]
    public float stiffness = 100f;

    [Range(0f, 2f)]
    [Tooltip("How quickly the swinging dies away. 0 swings on forever, below 1 bounces a bit before it settles, 1 settles as " +
             "quickly as it can without bouncing, and above 1 is slow and heavy.")]
    public float dampingRatio = 0.6f;

    [Min(0f)]
    [Tooltip("How hard each unit of weight on it pushes it down. Weight is the mass of the Rigidbody2D of what is standing on " +
             "it, and only things with a CanTriggerFloorCollapse that is turned on count, as with tiles that crumble. The " +
             "default is the strength of gravity.")]
    public float weightPush = 9.81f;

    [Min(0f)]
    [Tooltip("The furthest down, in units, it can go. It stops there however much is on it. 0 is no limit.")]
    public float maxDrop = 3f;

    [Min(0f)]
    [Tooltip("The furthest up, in units, it can swing past where it started when the weight comes off. 0 is not at all.")]
    public float maxRise = 0.5f;

    // How far from a contact's lowest point the underside of something can be for it to count as standing on the platform,
    // rather than leaning on the side of it.
    private const float StandingTolerance = 0.1f;

    private Rigidbody2D rigidBody2D;

    // Where it started, how far from there it is now, going up being positive, and how fast it is moving up.
    private Vector2 home;
    private float offset;
    private float velocity;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[64];
    private readonly HashSet<CanTriggerFloorCollapse> standing = new HashSet<CanTriggerFloorCollapse>();

    void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();

        home = rigidBody2D.position;

        // It's moved by hand. Left alone, the physics would push it about as well, and it would never be where the spring
        // says it is. It doesn't go to sleep, as then it would never feel a weight being put on it.
        rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidBody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        rigidBody2D.sleepMode = RigidbodySleepMode2D.NeverSleep;
    }

    void FixedUpdate()
    {
        var step = Time.fixedDeltaTime;

        // The spring pulls it back toward where it started, in proportion to how far it is, and the damping works against
        // however fast it is moving. The weight on it always pushes it down. Down is negative.
        var damping = 2f * dampingRatio * Mathf.Sqrt(stiffness * mass);
        var force = -stiffness * offset - damping * velocity - StandingWeight() * weightPush;

        velocity += force / mass * step;
        offset += velocity * step;

        // It stops dead at either end, rather than bouncing off it.
        if (maxDrop > 0f && offset < -maxDrop)
        {
            offset = -maxDrop;
            velocity = Mathf.Max(velocity, 0f);
        }
        else if (offset > maxRise)
        {
            offset = maxRise;
            velocity = Mathf.Min(velocity, 0f);
        }

        rigidBody2D.MovePosition(home + Vector2.up * offset);
    }

    // The combined weight of everything that is standing on the platform now: whatever it is in contact with that is resting
    // on top of it. Each thing counts once, however many places it touches.
    private float StandingWeight()
    {
        standing.Clear();

        var count = rigidBody2D.GetContacts(contacts);
        var total = 0f;

        for (int i = 0; i < count; i++)
        {
            var contact = contacts[i];

            // Whichever of the two colliders isn't this platform's.
            var other = contact.collider.attachedRigidbody == rigidBody2D ? contact.otherCollider : contact.collider;

            if (other.attachedRigidbody == rigidBody2D || other.isTrigger)
            {
                continue;
            }

            // Resting on top: the contact is at the underside of the thing, and its middle is above it.
            var bounds = other.bounds;
            if (Mathf.Abs(contact.point.y - bounds.min.y) > StandingTolerance || bounds.center.y < contact.point.y)
            {
                continue;
            }

            var counter = other.GetComponentInParent<CanTriggerFloorCollapse>();

            if (counter && counter.triggersCollapse && standing.Add(counter))
            {
                total += counter.Weight;
            }
        }

        return total;
    }
}
