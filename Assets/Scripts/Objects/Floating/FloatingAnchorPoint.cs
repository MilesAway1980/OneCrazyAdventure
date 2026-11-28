using System.Collections.Generic;
using UnityEngine;

// A place on something that a floating object's string can be tied to, to hold it up: a platform, a crate, anything with a Rigidbody2D. Put it on the
// object, or on a child of it where the string should be tied (a corner of a platform), and give a Floating Object's string this as its Anchor.
//
// It holds nothing up by itself: it tells the floats tied to it where it is, and lets you see how much they hold up. See Docs/Floating.md.
public class FloatingAnchorPoint : MonoBehaviour
{
    [Tooltip("The body that the floating objects hold up. Found on this object, or the nearest object above it that has one, if it's empty. It has to be a dynamic " +
             "Rigidbody2D, so that it can fall.")]
    public Rigidbody2D body;

    [Tooltip("Hold the body still when the game starts until every floating object tied to it has come on and put itself in place, so that it doesn't start to fall " +
             "(and tip) under the first of them while the rest are still being tied on. Turn it off for a body that should drop at once.")]
    public bool holdUntilAttached = true;

    [Min(0f)]
    [Tooltip("Hold the body for this many seconds more after the last float is in place, for them to settle. Only matters with Hold Until Attached.")]
    public float releaseDelay = 0.3f;

    private readonly List<FloatingObject> floats = new List<FloatingObject>();
    private FloatingBody holder;

    // The way the body was turned when the game started, in degrees, which is what it's levelled back to.
    public float RestRotation
    {
        get { return holder ? holder.RestRotation : 0f; }
    }

    // The floats tied here.
    public IReadOnlyList<FloatingObject> Floats
    {
        get { return floats; }
    }

    // Whether the body is being held still while the floats are attached, so it isn't dynamic yet, and isn't being held up yet either.
    public bool Waiting
    {
        get { return holder && holder.Waiting; }
    }

    // The body held up.
    public Rigidbody2D Body
    {
        get
        {
            if (!body)
            {
                body = GetComponentInParent<Rigidbody2D>();
            }

            return body;
        }
    }

    // Where the strings are tied, in the world.
    public Vector2 WorldPoint
    {
        get { return transform.position; }
    }

    // How much is held up here by the floats tied to it, in mass: what the lifts of all of them add up to.
    public float Lift
    {
        get
        {
            var total = 0f;

            foreach (var thing in floats)
            {
                if (thing)
                {
                    total += thing.ShareFor(this);
                }
            }

            return total;
        }
    }

    // How many floats are tied here.
    public int FloatCount
    {
        get { return floats.Count; }
    }

    // The floats say so as they come on and go off.
    public void Add(FloatingObject thing)
    {
        if (!floats.Contains(thing))
        {
            floats.Add(thing);
        }
    }

    public void Remove(FloatingObject thing)
    {
        floats.Remove(thing);

        // Nothing is holding it up, or less is, and a body that has been asleep in the air stays that way until something wakes it: it should start to sink.
        if (Body)
        {
            Body.WakeUp();
        }
    }

    void Reset()
    {
        body = GetComponentInParent<Rigidbody2D>();
    }

    // As the scene starts, the body is held still before anything can act on it, until all that holds it up is on.
    void OnEnable()
    {
        holder = FloatingBody.For(this);

        if (holder)
        {
            holder.Register(this);
        }
    }

    void OnDisable()
    {
        if (holder)
        {
            holder.Unregister(this);
        }

        holder = null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        var point = (Vector3)WorldPoint;
        Gizmos.DrawWireSphere(point, 0.12f);
        Gizmos.DrawLine(point + Vector3.left * 0.2f, point + Vector3.right * 0.2f);
        Gizmos.DrawLine(point + Vector3.down * 0.2f, point + Vector3.up * 0.2f);
    }

    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        var held = Body;

        // How it's going: what's held up here against what the body weighs, which is the balance that decides whether it rises, sinks or stays. (The
        // lift here is only part of it if there are other anchor points on the same body.)
        UnityEditor.Handles.Label((Vector3)WorldPoint + Vector3.up * 0.4f,
                                  held ? $"Lift here {Lift:0.#} | body mass {held.mass:0.#}" : "No Rigidbody2D to hold up");
#endif
    }
}
