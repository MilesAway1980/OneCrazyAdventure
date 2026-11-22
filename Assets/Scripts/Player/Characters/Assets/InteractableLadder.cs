using System.Collections.Generic;
using System.Linq;
using Characters;
using UnityEditor;
using UnityEngine;

public class InteractableLadder : InteractableObject
{
    //public List<Character> charactersThatCantClimb;
    private BoxCollider2D boxCollider;
    //Ladder ladder;

    //False if the ladder currently cannot be climbed
    [Tooltip("Whether the ladder can be climbed at the moment. False while it can't be.")]
    public bool climbable;
    private List<Character> climbingCharacters;
    
    void Start()
    {
        /*var thisCharacter = GetComponent<Character>();
        if (!charactersThatCantClimb.Contains(thisCharacter))
        {
            charactersThatCantClimb.Add(thisCharacter);
        }

        charactersThatCantClimb = charactersThatCantClimb.Where(c => c).ToList();*/
        boxCollider = GetComponent<BoxCollider2D>();
        //ladder = GetComponent<Ladder>();
        
        climbingCharacters = new List<Character>();
    }

    void Update()
    {
        
    }
    
    public override bool Interact(Character climber)
    {
        if (
            //Cannot climb this ladder
            !climbable ||
            //The ladder is the game object, and it cannot climb itself
            climber == gameObject ||
            //The climber is not inside the bounds of the ladder
            !boxCollider.bounds.Contains(climber.transform.position)
        )
        {
            return false;
        }
        
        print($"{climber.name} is trying to climb");
        
        climbingCharacters.Add(climber);
        climber.SetClimb(this);

        return true;
    }

    public Bounds GetBounds()
    {
        return boxCollider.bounds;
    }
}
