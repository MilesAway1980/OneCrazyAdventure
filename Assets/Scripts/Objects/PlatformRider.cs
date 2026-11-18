using UnityEngine;

// Lets something the physics moves, such as a crate, be carried sideways by a moving kinematic body it stands on, such as a platform.
//
// Friction would do it if there were enough, but there often isn't: a crate on a platform that starts to move is left behind, and slides
// off the back. So this works out how fast the place under the crate is going, as a character does, and the crate is given that.
//
// What the crate does on its own on the platform is its own: it's the platform's speed that is added, not set, so something that pushes
// the crate along the platform still can, and friction still slows it against the platform. Only a platform's sideways movement is
// carried. Added by itself to anything carryable or pushable.
[RequireComponent(typeof(Rigidbody2D))]
public class PlatformRider : MonoBehaviour
{
    private Rigidbody2D body;
    private readonly SupportTracker support = new SupportTracker();

    // How much of the sideways speed is the platform's, so that it can be told from the crate's own.
    private float carried;
    private bool wasMeasured;

    public static void EnsureOn(GameObject thing)
    {
        // A character is carried by its own code, which also lets it walk about on what carries it, so it isn't given this. (The
        // Brick is a character that is also pushable.)
        if (thing && !thing.GetComponent<PlatformRider>() && thing.GetComponent<Rigidbody2D>() && !thing.GetComponent<Characters.Character>())
        {
            thing.AddComponent<PlatformRider>();
        }
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        // In someone else's game the host moves everything, and it's shown here by where it's sent.
        if (GameSession.IsClientOnly || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated)
        {
            carried = 0f;
            wasMeasured = false;
            return;
        }

        support.Update(body, transform);

        var ride = support.riding && support.measured ? support.velocity.x : 0f;

        if (support.riding && support.measured && !wasMeasured)
        {
            // The first step the platform's speed is known. The crate's speed is the platform's or its own, whichever makes it the
            // slower on it, so one dropped on a platform is carried along, and one that was carried and has hopped isn't carried twice.
            var speed = body.linearVelocity.x;
            carried = Mathf.Abs(speed) < Mathf.Abs(speed - ride) ? 0f : ride;
        }
        else if (!support.riding)
        {
            // Off it. In the air it keeps the speed it had; on the ground, friction takes it.
            carried = 0f;
        }

        wasMeasured = support.riding && support.measured;

        // Only written when it changes, so a crate on a platform that isn't moving is still allowed to go to sleep.
        var change = ride - carried;

        if (change != 0f)
        {
            body.linearVelocityX += change;
        }

        carried = ride;
    }
}
