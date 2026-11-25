using System.Collections.Generic;
using Characters;
using UnityEngine;

public class CarryableObject : MonoBehaviour
{
    [Tooltip("The character carrying this at the moment. Set as the game runs: for watching, not for setting.")]
    public Character carrier;
    private FixedJoint2D fixedJoint;
    //public Collider2D carryCollider;
    private bool beingCarried;

    private List<Character> characters;

    // While it's held, its solid colliders are switched off and gravity doesn't pull on it, and they go back as they were when
    // it's put down.
    private float gravityBeforeCarry;
    private readonly List<Collider2D> switchedOff = new List<Collider2D>();

    private static readonly Collider2D[] overlaps = new Collider2D[16];

    void Start()
    {
        fixedJoint = GetComponent<FixedJoint2D>();
        if (fixedJoint)
        {
            fixedJoint.enabled = false;
        }

        characters = new List<Character>();

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

    // Whether a character is holding it.
    public bool IsCarried
    {
        get { return beingCarried; }
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

    // Picks this up for the character, if they are in reach and can hold it. Returns this if they did, and null if not, so that
    // a character who couldn't carry it isn't taken for one who is, and whatever else they could do with Interact is tried.
    public CarryableObject Carry(Character characterCheck)
    {
        if (beingCarried)
        {
            //Is being carried by someone else
            return null;
        }

        // Not able to carry anything, so there's nothing to try, and no carry point is needed.
        if (!characterCheck || !characterCheck.ableToCarryObjects)
        {
            return null;
        }

        if (characters.Contains(characterCheck) && SetCarrier(characterCheck))
        {
            return this;
        }

        return null;
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
