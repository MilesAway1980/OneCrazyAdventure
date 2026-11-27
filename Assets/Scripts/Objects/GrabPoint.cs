using System.Collections.Generic;
using Characters;
using UnityEngine;

// Something a character can take hold of with Interact and hang from, such as the end of the Balloon's string. Interact
// again lets go. The character is joined to the body this is on, so his weight is carried by whatever it hangs from:
// a light character can be lifted by it and a heavy one can't.
[RequireComponent(typeof(Rigidbody2D))]
public class GrabPoint : InteractableObject
{
    [Tooltip("How close, in units, a character has to be to take hold of this.")]
    public float reach = 1f;

    [Tooltip("How many characters can hold on at once. Each one's weight is carried by whatever this hangs from.")]
    public int maxGrabbers = 1;

    private Rigidbody2D body;
    private readonly Dictionary<Character, HingeJoint2D> holds = new Dictionary<Character, HingeJoint2D>();
    private readonly List<Character> gone = new List<Character>();

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public override bool Interact(Character grabber)
    {
        if (
            !grabber.ableToGrabObjects ||
            IsGrabbedBy(grabber) ||
            holds.Count >= maxGrabbers ||
            Vector2.Distance(grabber.transform.position, transform.position) > reach
        )
        {
            return false;
        }

        // His centre is held to the grab point, and he can swing around it. He doesn't collide with the one body he's
        // joined to, or he'd be pushed off it.
        var joint = grabber.gameObject.AddComponent<HingeJoint2D>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedBody = body;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = Vector2.zero;
        joint.enableCollision = false;

        holds[grabber] = joint;
        grabber.SetGrab(this);

        return true;
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

    // Whether anyone is hanging from it.
    public bool IsHeld
    {
        get
        {
            ForgetGone();
            return holds.Count > 0;
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
    }
}
