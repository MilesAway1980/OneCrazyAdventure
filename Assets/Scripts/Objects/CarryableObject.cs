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

    public void SetCarrier(Character newCarrier)
    {
        if (fixedJoint)
        {
            var carryBody = newCarrier.GetCarryPoint();

            if (carryBody)
            {
                beingCarried = true;
                carrier = newCarrier;

                fixedJoint.enabled = true;
                fixedJoint.connectedBody = carryBody;
                fixedJoint.connectedAnchor = Vector2.zero;

                print($"Being carried by {carrier.name}");
            }
            else
            {
                print ($"Cannot carry {gameObject.name} because {newCarrier.name} does not have a Carry Point");    
            }
        }
        else
        {
            print ($"Cannot carry {gameObject.name} because there is no fixedJoint");
        }
            
    }

    public void Drop()
    {
        beingCarried = false;
        fixedJoint.enabled = false;
        fixedJoint.connectedBody = null;
        print($"Being dropped by {carrier.name}");
    }

    public CarryableObject Carry(Character characterCheck)
    {
        if (beingCarried)
        {
            //Is being carried by someone else
            return null;
        }

        if (characters.Contains(characterCheck))
        {
            SetCarrier(characterCheck);
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
