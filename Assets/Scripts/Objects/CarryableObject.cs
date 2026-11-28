using System.Collections.Generic;
using Characters;
using UnityEngine;

// Something a character can pick up and carry. It needs a Rigidbody2D, a Fixed Joint 2D that it hangs from the carrier's carry point by, a
// solid collider, and a trigger collider that's how near a character has to be to pick it up. Adding this to an object gives it the
// ones it doesn't have: the body and the joint are required, and Reset adds the colliders, sized to its picture. It's also a
// PushableSolid, so that characters walking up to it aren't stopped short by it as if it were a wall. See Docs/Carrying.md.
[RequireComponent(typeof(Rigidbody2D), typeof(FixedJoint2D), typeof(PushableSolid))]
public class CarryableObject : MonoBehaviour
{
    [Tooltip("The character carrying this at the moment. Set as the game runs: for watching, not for setting.")]
    public Character carrier;
    private FixedJoint2D fixedJoint;
    //public Collider2D carryCollider;
    private bool beingCarried;

    // The characters within reach of it. Made here and not in Start, as a character can come into reach before Start has run, such as
    // one already standing there as the scene opens.
    private readonly List<Character> characters = new List<Character>();

    // While it's held, its solid colliders are switched off and gravity doesn't pull on it, and they go back as they were when
    // it's put down.
    private float gravityBeforeCarry;
    private readonly List<Collider2D> switchedOff = new List<Collider2D>();

    private static readonly Collider2D[] overlaps = new Collider2D[16];

#if UNITY_EDITOR
    // When this is first added (or Reset is chosen): what it needs to be carried is added if it isn't there. A body that still has all
    // of Unity's defaults is given a crate's weight, and gravity that makes it fall at a good pace.
    void Reset()
    {
        var size = Vector2.one;
        var picture = GetComponent<SpriteRenderer>();

        if (picture && picture.sprite)
        {
            size = picture.sprite.bounds.size;
        }

        var hasSolid = false;
        var hasReach = false;

        foreach (var own in GetComponentsInChildren<Collider2D>())
        {
            if (own.isTrigger)
            {
                hasReach = true;
            }
            else
            {
                hasSolid = true;
            }
        }

        if (!hasSolid)
        {
            UnityEditor.Undo.AddComponent<BoxCollider2D>(gameObject).size = size;
        }

        // How near someone has to be to pick it up, which is what the carry script looks for.
        if (!hasReach)
        {
            var reach = UnityEditor.Undo.AddComponent<BoxCollider2D>(gameObject);
            reach.isTrigger = true;
            reach.size = size * 1.6f;
        }

        var body = GetComponent<Rigidbody2D>();

        if (body && body.mass == 1f && body.gravityScale == 1f && body.collisionDetectionMode == CollisionDetectionMode2D.Discrete)
        {
            body.mass = 3f;
            body.gravityScale = 2f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // Off until it's picked up: the joint is switched on and joined to the carrier's carry point then.
        var joint = GetComponent<FixedJoint2D>();

        if (joint)
        {
            joint.autoConfigureConnectedAnchor = false;
            joint.enabled = false;
        }
    }

    // In the editor, when something is changed in the Inspector or the scene is opened: a joint that has Auto Configure Connected Anchor on
    // has it switched off, and it's said so. It's done a moment later, as changing other components while this is called isn't allowed to be
    // relied on. (When playing, Start does it.)
    void OnValidate()
    {
        var joint = GetComponent<FixedJoint2D>();

        if (Application.isPlaying || !joint || !joint.autoConfigureConnectedAnchor)
        {
            return;
        }

        var carryable = this;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!carryable || !joint || !joint.autoConfigureConnectedAnchor)
            {
                return;
            }

            UnityEditor.Undo.RecordObject(joint, "Switch Off Auto Configure Connected Anchor");
            joint.autoConfigureConnectedAnchor = false;
            UnityEditor.EditorUtility.SetDirty(joint);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(joint);

            Debug.LogWarning($"{carryable.name}'s Fixed Joint 2D had Auto Configure Connected Anchor on, so it has been switched off. With it on, " +
                             "the object stays where it was in relation to the carry point when it's picked up, instead of going to the point.", joint);
        };
    }
#endif

    void Start()
    {
        fixedJoint = GetComponent<FixedJoint2D>();
        if (fixedJoint)
        {
            // Never worked out from where it is, which keeps it where it was instead of taking it to the carry point.
            fixedJoint.autoConfigureConnectedAnchor = false;
            fixedJoint.enabled = false;
        }

        // Characters stop walking when they sense a wall close ahead, and a crate on the Ground layer would be one: they'd stop 0.75 short of it
        // and never get near. A PushableSolid is looked through, so they walk up to it and push it. (One that was made before this was
        // required doesn't have it yet.)
        if (!GetComponent<PushableSolid>())
        {
            gameObject.AddComponent<PushableSolid>();
        }

        // Something that can be carried by a character is carried by a platform it stands on, too.
        PlatformRider.EnsureOn(gameObject);

        // And floats or sinks in a liquid: as wood, unless it's been given something else to be made of.
        FloatsInFluid.EnsureOn(gameObject);

        // And if it falls into a Kill Zone, it comes back to where it started, so it can't be lost.
        ReturnsToStart.EnsureOn(gameObject);

        /*var allColliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in allColliders)
        {
            if (col.isTrigger)
            {
                carryCollider = col;
            }
        }*/
    }

    void Update()
    {

    }

    private Rigidbody2D ownBody;

    // How much it weighs: the mass of its Rigidbody2D. What a character who carries it has added to their own mass (less what their
    // Carry Strength takes off it).
    public float Mass
    {
        get
        {
            if (!ownBody)
            {
                ownBody = GetComponent<Rigidbody2D>();
            }

            return ownBody ? ownBody.mass : 0f;
        }
    }

    // Whether a character is holding it.
    public bool IsCarried
    {
        get { return beingCarried; }
    }

    // What it's hanging from, if it's hanging from a rope: the end of the Balloon's string, which has taken hold of it. It's a body of its own then, with its
    // gravity and its solid colliders, pulled by the rope, so it can't be carried as well.
    public GrabPoint HungFrom { get; private set; }

    public bool IsHung
    {
        get { return HungFrom; }
    }

    // Set by the grab point that has taken hold of it, and cleared when it lets go.
    public void SetHung(GrabPoint point)
    {
        HungFrom = point;
    }

    // How far, in units, a point is from the nearest part of it: 0 if it's inside. Its solid colliders, not the trigger round it that's for reaching.
    public float DistanceFrom(Vector2 point)
    {
        var nearest = float.MaxValue;

        foreach (var own in GetComponentsInChildren<Collider2D>())
        {
            if (own.isTrigger || !own.enabled)
            {
                continue;
            }

            nearest = Mathf.Min(nearest, Vector2.Distance(own.ClosestPoint(point), point));
        }

        return nearest == float.MaxValue ? Vector2.Distance(transform.position, point) : nearest;
    }

    // Joins this to the character's carry point. False if it can't be carried by them: there is no joint on this, or the
    // character has no carry point to hold it by. Nothing is changed then.
    public bool SetCarrier(Character newCarrier)
    {
        if (!newCarrier)
        {
            return false;
        }

        if (!fixedJoint)
        {
            Debug.LogWarning($"Cannot carry {gameObject.name} because it has no Fixed Joint 2D.", this);
            return false;
        }

        var carryBody = newCarrier.GetCarryPoint();

        // A character who can carry things has to have somewhere to hold them, so this is a mistake in how they're set up, and
        // it's reported as an error. One who can't carry things isn't asked, so never gets here.
        if (!carryBody)
        {
            Debug.LogError($"{newCarrier.name} can carry objects, but has no Carry Attachment Point to hold {gameObject.name} by. " +
                           "Give it one, or untick Able To Carry Objects on the character.", newCarrier);
            return false;
        }

        beingCarried = true;
        carrier = newCarrier;
        SwitchOffCollisions();

        // Auto Configure Connected Anchor is off first, or joining it to the carry point would work the anchor out from where this is now.
        fixedJoint.autoConfigureConnectedAnchor = false;
        fixedJoint.enabled = true;
        fixedJoint.connectedBody = carryBody;
        fixedJoint.connectedAnchor = Vector2.zero;

        print($"Being carried by {carrier.name}");
        return true;
    }

    public void Drop()
    {
        beingCarried = false;
        SwitchOnCollisions();

        if (fixedJoint)
        {
            fixedJoint.enabled = false;
            fixedJoint.connectedBody = null;
        }

        print($"Being dropped by {(carrier ? carrier.name : "nobody")}");
        carrier = null;
    }

    // Held, it goes through everything, the carrier and the tiles alike. It hangs from a joint to a point that moves with the
    // carrier, so if it could be stopped by a wall or the edge of a ledge as the carrier jumps over or drops past it, it would
    // be left behind, sliding along the surface, until there was a clear way to the carrier and then snapped to them.
    private void SwitchOffCollisions()
    {
        SwitchOnCollisions();

        var body = GetComponent<Rigidbody2D>();

        if (body)
        {
            gravityBeforeCarry = body.gravityScale;
            body.gravityScale = 0f;
        }

        // Only the solid ones: the trigger around it is how a character is found to be in reach.
        foreach (var own in GetComponentsInChildren<Collider2D>())
        {
            if (!own.isTrigger && own.enabled)
            {
                own.enabled = false;
                switchedOff.Add(own);
            }
        }
    }

    private void SwitchOnCollisions()
    {
        if (switchedOff.Count == 0)
        {
            return;
        }

        var body = GetComponent<Rigidbody2D>();

        if (body)
        {
            body.gravityScale = gravityBeforeCarry;
        }

        foreach (var own in switchedOff)
        {
            if (own)
            {
                own.enabled = true;
            }
        }

        switchedOff.Clear();
    }

    // Whether there's room to put it down where it is. Held, it goes through walls, so it could be inside one, and putting it
    // down there would leave it stuck in the ground or thrown out of it.
    public bool CanPutDown()
    {
        var picture = GetComponentInChildren<SpriteRenderer>();

        if (!picture)
        {
            return true;
        }

        var bounds = picture.bounds;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(LayerMask.GetMask("Ground"));

        // A little smaller than it is, so resting on the ground or against a wall isn't being inside it.
        var count = Physics2D.OverlapBox(bounds.center, bounds.size * 0.9f, transform.eulerAngles.z, filter, overlaps);
        var myBody = GetComponent<Rigidbody2D>();

        for (var i = 0; i < count; i++)
        {
            var other = overlaps[i];

            if (other.isTrigger || (myBody && other.attachedRigidbody == myBody))
            {
                continue;
            }

            if (carrier && other.transform.IsChildOf(carrier.transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // Whether a character who is in reach of this, and able to carry things, is trying for something that is more than they can lift: its mass is
    // over their Maximum Carry Weight. (That's its own mass, before their Carry Strength takes any off.) They reach for it, and it doesn't
    // move.
    public bool TooHeavyFor(Character characterCheck)
    {
        return !beingCarried && !IsHung && characterCheck && characterCheck.ableToCarryObjects && characterCheck.maxCarryWeight > 0f &&
               Mass > characterCheck.maxCarryWeight && characters.Contains(characterCheck);
    }

    // Picks this up for the character, if they are in reach and can hold it. Returns this if they did, and null if not, so that
    // a character who couldn't carry it isn't taken for one who is, and whatever else they could do with Interact is tried.
    public CarryableObject Carry(Character characterCheck)
    {
        if (beingCarried || IsHung)
        {
            //Is being carried by someone else, or hanging from a rope
            return null;
        }

        // Not able to carry anything, so there's nothing to try, and no carry point is needed.
        if (!characterCheck || !characterCheck.ableToCarryObjects)
        {
            return null;
        }

        // More than they can lift: nothing happens to it, and nothing is joined.
        if (TooHeavyFor(characterCheck))
        {
            return null;
        }

        if (characters.Contains(characterCheck) && SetCarrier(characterCheck))
        {
            return this;
        }

        return null;
    }

    void OnDisable()
    {
        characters.Clear();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var character = other.GetComponent<Character>();
        if (character && !characters.Contains(character))
        {
            characters.Add(character);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var character = other.GetComponent<Character>();
        if (character && characters.Contains(character))
        {
            characters.Remove(character);
        }
    }
}
