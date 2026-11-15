using System.Collections.Generic;
using Characters;
using UnityEngine;

public class CarryableObject : MonoBehaviour
{
    public Character carrier;
    private FixedJoint2D fixedJoint;
    //public Collider2D carryCollider;
    private bool beingCarried;
    
    private List<Character> characters;

    void Start()
    {
        fixedJoint = GetComponent<FixedJoint2D>();
        if (fixedJoint)
        {
            fixedJoint.enabled = false;
        }

        characters = new List<Character>();
        
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

        fixedJoint.enabled = true;
        fixedJoint.connectedBody = carryBody;
        fixedJoint.connectedAnchor = Vector2.zero;

        print($"Being carried by {carrier.name}");
        return true;
    }

    public void Drop()
    {
        beingCarried = false;

        if (fixedJoint)
        {
            fixedJoint.enabled = false;
            fixedJoint.connectedBody = null;
        }

        print($"Being dropped by {(carrier ? carrier.name : "nobody")}");
        carrier = null;
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
