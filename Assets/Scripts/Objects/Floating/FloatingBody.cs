using System.Collections.Generic;
using UnityEngine;

// Keeps a body that floating objects hold up from falling while they're still being attached. The floats come on a few at a time as the scene starts, and
// without this the body starts to fall (and tip) under the first of them, and by the time the last is tied it's already off level, and stays that way.
//
// While a body is held it is made Kinematic, so physics does nothing to it. Once every float tied to it has come on and put itself in place, and a moment
// more (Release Delay on the anchor points), it goes back to what it was and the floats take its weight. It's added to a body by its first anchor point, which
// is what has the settings; it never goes on a prefab.
[DisallowMultipleComponent]
public class FloatingBody : MonoBehaviour
{
    // The longest it's held, in seconds, if a float never gets into place, so that one that has gone wrong doesn't leave the body stuck in the air.
    private const float LongestHold = 5f;

    private Rigidbody2D body;
    private readonly List<FloatingAnchorPoint> anchors = new List<FloatingAnchorPoint>();

    private RigidbodyType2D original;
    private bool held;
    private float startedAt;
    private float readyAt = -1f;

    // The way it was turned when it was first found, in degrees.
    public float RestRotation { get; private set; }

    // Whether the body is being held still, and so is neither Dynamic nor holding anything up yet.
    public bool Waiting
    {
        get { return held; }
    }

    // The one on this anchor point's body, made if there isn't one.
    public static FloatingBody For(FloatingAnchorPoint anchor)
    {
        var target = anchor.Body;

        if (!target)
        {
            return null;
        }

        var found = target.GetComponent<FloatingBody>();

        if (!found)
        {
            found = target.gameObject.AddComponent<FloatingBody>();
        }

        return found;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        RestRotation = body ? body.rotation : 0f;
    }

    // An anchor point on this body has come on. The first one that wants it starts the hold, straight away, before any physics step can act.
    public void Register(FloatingAnchorPoint anchor)
    {
        if (!anchors.Contains(anchor))
        {
            anchors.Add(anchor);
        }

        if (!warnedColliders && body && body.attachedColliderCount == 0)
        {
            // Without one, physics gives it next to no resistance to turning, so the slightest uneven push spins it wildly, whatever its mass is set to.
            warnedColliders = true;
            Debug.LogWarning($"{name}: this has a Rigidbody2D but no collider, so it has almost no resistance to turning (its mass is just a number to physics, " +
                             "not a size), and floats holding it up will spin it. Give it a Collider 2D, such as a Box Collider 2D the size of its picture.", this);
        }

        if (!held && !started && anchor.holdUntilAttached && body && body.bodyType == RigidbodyType2D.Dynamic && !GameSession.IsClientOnly)
        {
            original = body.bodyType;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            held = true;
            started = true;
            startedAt = Time.time;
            readyAt = -1f;
        }
    }

    private bool warnedColliders;

    // It's only ever held once, as the scene starts. A float that comes on later is just one more, and doesn't hold it up again.
    private bool started;

    public void Unregister(FloatingAnchorPoint anchor)
    {
        anchors.Remove(anchor);

        if (held && anchors.Count == 0)
        {
            Release();
        }
    }

    void FixedUpdate()
    {
        if (!held)
        {
            return;
        }

        // In someone else's game the host decides where it is and what it's doing: nothing here.
        if (GameSession.IsClientOnly)
        {
            held = false;
            return;
        }

        FloatingObject waitingFor = null;
        var delay = 0f;

        foreach (var anchor in anchors)
        {
            if (!anchor)
            {
                continue;
            }

            delay = Mathf.Max(delay, anchor.releaseDelay);

            foreach (var thing in anchor.Floats)
            {
                if (thing && thing.isActiveAndEnabled && !thing.IsPlaced)
                {
                    waitingFor = thing;
                }
            }
        }

        if (waitingFor)
        {
            readyAt = -1f;

            if (Time.time - startedAt > LongestHold)
            {
                Debug.LogWarning($"{name}: {waitingFor.name} didn't get into place within {LongestHold:0} seconds, so what it's tied to was let go anyway.", waitingFor);
                Release();
            }

            return;
        }

        // Every float is in place: a moment longer for them to settle, and then it's let go.
        if (readyAt < 0f)
        {
            readyAt = Time.time + delay;
        }

        if (Time.time >= readyAt)
        {
            Release();
        }
    }

    // Back to what it was, standing still, and awake so that it can start to sink or rise.
    private void Release()
    {
        if (!held)
        {
            return;
        }

        held = false;

        if (body)
        {
            body.bodyType = original;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.WakeUp();
        }
    }

    void OnDestroy()
    {
        Release();
    }
}
