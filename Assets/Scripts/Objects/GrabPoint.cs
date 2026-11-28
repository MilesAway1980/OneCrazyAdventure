using System.Collections.Generic;
using Characters;
using UnityEngine;

// Something a character can take hold of with Interact and hang from, such as the end of the Balloon's string. Interact
// again lets go. With a Rope Anchor, the Balloon himself can take hold of something that can be carried, too (a crate), which hangs from it in just the same
// way. Only one thing hangs from it at a time, a character or a crate, as many as Max Grabbers allows. Normally the character is joined to the body this is on, so his weight is carried by whatever it hangs from. With a Rope Anchor
// he's instead joined to that body, at a point of it, by a rope as long as Rope Length: only a limit, so that it does nothing while it's slack, and when
// it's tight the two pull on each other as heavy as each is. The Balloon's string does that, with the Balloon himself as the anchor.
[RequireComponent(typeof(Rigidbody2D))]
public class GrabPoint : InteractableObject
{
    [Tooltip("How close, in units, a character's body has to be to take hold of this: from the nearest part of him, not his middle, so a big one can reach " +
             "it as well as a small one.")]
    public float reach = 1f;

    [Tooltip("How many can hold on at once, characters and crates together. Each one's weight is carried by whatever this hangs from.")]
    public int maxGrabbers = 1;

    [Tooltip("If there is one, what's holding on is joined to it by a rope, not to this. For the Balloon's string: the Balloon.")]
    public Rigidbody2D ropeAnchor;

    [Tooltip("Where on the Rope Anchor the rope is tied, in its own space.")]
    public Vector2 ropeAnchorOffset;

    [Tooltip("How long, in units, the rope to the Rope Anchor is.")]
    public float ropeLength = 1f;

    // Whether the rope is pulled tight, which whatever it is on the end of is told, as it's being held up by it. Set by whatever looks after the rope.
    [HideInInspector]
    public bool ropeTaut;

    private Rigidbody2D body;
    private readonly Dictionary<Character, Joint2D> holds = new Dictionary<Character, Joint2D>();
    private readonly List<Character> gone = new List<Character>();

    // What the Balloon has taken hold of that isn't a character, and the rope it hangs by.
    private readonly Dictionary<CarryableObject, Joint2D> heldThings = new Dictionary<CarryableObject, Joint2D>();
    private readonly List<CarryableObject> goneThings = new List<CarryableObject>();

    // How many are holding on or being held, of both kinds.
    private int HoldCount
    {
        get { return holds.Count + heldThings.Count; }
    }

    // A rope, from what hangs by it to the anchor, as long as the rope is, or as far as it is if it's further (it's been taken hold of from where it was): it
    // shortens to its length by itself, not at once.
    private DistanceJoint2D MakeRope(GameObject hanging, Vector2 where)
    {
        var rope = hanging.AddComponent<DistanceJoint2D>();
        rope.autoConfigureDistance = false;
        rope.connectedBody = ropeAnchor;
        rope.anchor = Vector2.zero;
        rope.connectedAnchor = ropeAnchorOffset;
        rope.distance = Mathf.Max(ropeLength, Vector2.Distance(where, ropeAnchor.transform.TransformPoint(ropeAnchorOffset)));
        rope.maxDistanceOnly = true;
        rope.enableCollision = false;
        return rope;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public override bool Interact(Character grabber)
    {
        if (
            !grabber.ableToGrabObjects ||
            IsGrabbedBy(grabber) ||
            HoldCount >= maxGrabbers ||
            grabber.DistanceFromBody(transform.position) > reach
        )
        {
            return false;
        }

        Joint2D joint;

        if (ropeAnchor)
        {
            // Held by a rope, as far from the anchor as it goes and no further. It's only a limit: slack, it's no hold at all.
            joint = MakeRope(grabber.gameObject, grabber.transform.position);
        }
        else
        {
            // His centre is held to the grab point, and he can swing around it. He doesn't collide with the one body he's
            // joined to, or he'd be pushed off it.
            var hinge = grabber.gameObject.AddComponent<HingeJoint2D>();
            hinge.autoConfigureConnectedAnchor = false;
            hinge.connectedBody = body;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = Vector2.zero;
            hinge.enableCollision = false;
            joint = hinge;
        }

        holds[grabber] = joint;
        grabber.SetGrab(this);

        return true;
    }

    // The Balloon takes hold of something that can be carried, with the end of his string: it hangs from the rope as a character does, with its own weight and
    // its solid colliders, and the string goes to it. False if there's no rope here, or it isn't free to be taken (someone has it, or it's hanging already),
    // or there's already something on the end.
    public bool HoldThing(CarryableObject thing)
    {
        if (!ropeAnchor || !thing || thing.IsCarried || thing.IsHung || HoldCount >= maxGrabbers || !thing.GetComponent<Rigidbody2D>())
        {
            return false;
        }

        heldThings[thing] = MakeRope(thing.gameObject, thing.transform.position);
        thing.SetHung(this);

        return true;
    }

    public void ReleaseThing(CarryableObject thing)
    {
        if (heldThings.TryGetValue(thing, out var joint))
        {
            heldThings.Remove(thing);

            if (joint)
            {
                Destroy(joint);
            }
        }

        if (thing && thing.HungFrom == this)
        {
            thing.SetHung(null);
        }
    }

    public void Release(Character grabber)
    {
        if (holds.TryGetValue(grabber, out var joint))
        {
            holds.Remove(grabber);

            if (joint)
            {
                Destroy(joint);
            }
        }
    }

    // Lets go of everyone hanging from it. Whoever is holding it lets go, with Interact; this is for whoever it's hanging from letting them go. Says
    // whether there was anyone.
    public bool ReleaseAll()
    {
        ForgetGone();

        if (HoldCount == 0)
        {
            return false;
        }

        var everyone = new List<Character>(holds.Keys);

        foreach (var grabber in everyone)
        {
            Release(grabber);

            if (grabber)
            {
                grabber.SetGrab(null);
            }
        }

        foreach (var thing in new List<CarryableObject>(heldThings.Keys))
        {
            ReleaseThing(thing);
        }

        return true;
    }

    // How fast, in units a second, a rope that was taken hold of when it was stretched shortens to its length.
    private const float RopeShortenSpeed = 1f;

    void FixedUpdate()
    {
        if (!ropeAnchor)
        {
            return;
        }

        var anchor = (Vector2)ropeAnchor.transform.TransformPoint(ropeAnchorOffset);

        foreach (var hold in holds)
        {
            if (hold.Value is DistanceJoint2D rope && rope && rope.distance > ropeLength)
            {
                rope.distance = Mathf.MoveTowards(rope.distance, ropeLength, RopeShortenSpeed * Time.fixedDeltaTime);
            }
        }

        foreach (var hold in heldThings)
        {
            if (hold.Value is DistanceJoint2D rope && rope && rope.distance > ropeLength)
            {
                rope.distance = Mathf.MoveTowards(rope.distance, ropeLength, RopeShortenSpeed * Time.fixedDeltaTime);
            }
        }

        // Something that has been put somewhere else in one go (a crate sent back to where it began) is no longer on the rope, and would pull him after it: it's
        // let go.
        ForgetGone();
        goneThings.Clear();

        foreach (var hold in heldThings)
        {
            if (Vector2.Distance(hold.Key.transform.position, anchor) > ropeLength * 2f + 2f)
            {
                goneThings.Add(hold.Key);
            }
        }

        foreach (var thing in goneThings)
        {
            ReleaseThing(thing);
        }
    }

    // How hard the rope is pulling on whoever is hanging from it, in force, as of the last step: for watching.
    public float Tension()
    {
        ForgetGone();

        foreach (var hold in holds)
        {
            if (hold.Value)
            {
                return hold.Value.GetReactionForce(1f / Mathf.Max(Time.fixedDeltaTime, 0.0001f)).magnitude;
            }
        }

        foreach (var hold in heldThings)
        {
            if (hold.Value)
            {
                return hold.Value.GetReactionForce(1f / Mathf.Max(Time.fixedDeltaTime, 0.0001f)).magnitude;
            }
        }

        return 0f;
    }

    // Adds the bodies of everything hanging from it, characters and crates, to a list.
    public void GetHeldBodies(List<Rigidbody2D> into)
    {
        ForgetGone();

        foreach (var hold in holds)
        {
            var heldBody = hold.Key.GetComponent<Rigidbody2D>();

            if (heldBody)
            {
                into.Add(heldBody);
            }
        }

        foreach (var hold in heldThings)
        {
            var heldBody = hold.Key.GetComponent<Rigidbody2D>();

            if (heldBody)
            {
                into.Add(heldBody);
            }
        }
    }

    // Adds the characters hanging from it to a list.
    public void GetHolders(List<Character> into)
    {
        ForgetGone();

        foreach (var hold in holds)
        {
            into.Add(hold.Key);
        }
    }

    // Whether anyone, or anything, is hanging from it.
    public bool IsHeld
    {
        get
        {
            ForgetGone();
            return HoldCount > 0;
        }
    }

    public bool IsGrabbedBy(Character grabber)
    {
        ForgetGone();
        return holds.ContainsKey(grabber);
    }

    // Anyone who was destroyed while holding on has taken his joint with him.
    private void ForgetGone()
    {
        gone.Clear();

        foreach (var hold in holds)
        {
            if (!hold.Key || !hold.Value)
            {
                gone.Add(hold.Key);
            }
        }

        foreach (var character in gone)
        {
            holds.Remove(character);
        }

        goneThings.Clear();

        foreach (var hold in heldThings)
        {
            if (!hold.Key || !hold.Value)
            {
                goneThings.Add(hold.Key);
            }
        }

        foreach (var thing in goneThings)
        {
            heldThings.Remove(thing);

            if (thing && thing.HungFrom == this)
            {
                thing.SetHung(null);
            }
        }
    }

    // A joint left joined to nothing would pin the character to the world.
    void OnDestroy()
    {
        foreach (var hold in holds)
        {
            if (hold.Value)
            {
                Destroy(hold.Value);
            }
        }

        foreach (var hold in heldThings)
        {
            if (hold.Value)
            {
                Destroy(hold.Value);
            }

            if (hold.Key && hold.Key.HungFrom == this)
            {
                hold.Key.SetHung(null);
            }
        }
    }
}
