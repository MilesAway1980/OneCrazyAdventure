using System;
using Characters;
using UnityEngine;

public class LeverSwitch : InteractableObject
{
    [Range(0, 180f)]
    public float degreesOfMovement;
    [Tooltip("Switch goes from left to right.  Will go from right to left if false.")]
    public bool leftToRight;
    [Tooltip("The game object the lever pivots on when activated")]
    public GameObject pivotObject;
    [Tooltip("How quickly the lever moves from off to on.")]
    public float moveSpeed;
    [Tooltip("Switch can only be used once, and stays used.")]
    public bool oneTime;
    [Tooltip("When the switch is on, it will constantly reapply the switch action.")]
    public bool staysActiveWhenOn;

    public SwitchableObject[] switchableObjects;

    [Header("Timing")]
    [Min(0f)]
    [Tooltip("When this switches more than one thing, how many seconds it waits between one and the next, in the order of the list: the first at once, the second after this long, the third after twice as long, and so on. For lights that come on one by one. 0 switches them all at the same moment.")]
    public float delayBetweenObjects;

    [Tooltip("How the things are switched off, if there's a delay between them: all at once, one after another in the same order, or one after another with the last first. Only matters when Delay Between Objects is more than 0.")]
    public SwitchOffOrder switchOffOrder = SwitchOffOrder.AllAtOnce;

    protected override System.Collections.Generic.IEnumerable<SwitchableObject> SwitchTargets
    {
        get { return switchableObjects; }
    }

    // The arrows to the things are numbered in the order they're switched, when there's a delay between them.
    protected override bool NumberSwitchTargets
    {
        get { return delayBetweenObjects > 0f; }
    }

    // Switches the things one after another, when there's a delay between them.
    private readonly SwitchSequence sequence = new SwitchSequence();
    private static readonly Func<SwitchableObject, bool> usable = thing => thing.enabled;

    private float angle;
    private bool interactedWith;
    private bool activated;
    private float leftAngle;
    private float rightAngle;

    private BoxCollider2D triggerCollider;
    

    void Start()
    {
        leftAngle = 360 + degreesOfMovement * 0.5f;
        rightAngle = 360 - degreesOfMovement * 0.5f;
        
        angle = leftToRight
                    ? leftAngle
                    : rightAngle;
        
        triggerCollider = GetComponent<BoxCollider2D>();
    }

    
    void Update()
    {
        // The things that are waiting for their turn.
        if (sequence.Pending && !GameSession.IsClientOnly)
        {
            sequence.Tick(switchableObjects, false, usable);
        }

        if (interactedWith)
        {
            if (leftToRight)
            {
                angle -= moveSpeed * Time.deltaTime;
                if (angle < rightAngle)
                {
                    angle = rightAngle;
                    if (activated == false || staysActiveWhenOn)
                    {
                        Activate();
                    }
                }
            }
            else
            {
                angle += moveSpeed * Time.deltaTime;
                if (angle > leftAngle)
                {
                    angle = leftAngle;
                    if (activated == false || staysActiveWhenOn)
                    {
                        Activate();
                    }
                }
            }
        }
        else
        {
            if (leftToRight)
            {
                angle += moveSpeed * Time.deltaTime;
                if (angle > leftAngle)
                {
                    angle = leftAngle;
                    if (activated)
                    {
                        Deactivate();
                    }
                }
            }
            else
            {
                angle -= moveSpeed * Time.deltaTime;
                if (angle < rightAngle)
                {
                    angle = rightAngle;
                    if (activated)
                    {
                        Deactivate();
                    }
                }
            }
        }
        
        pivotObject.transform.eulerAngles = new Vector3(0, 0, angle);
    }

    public override bool Interact(Character interacter)
    {
        if (!triggerCollider)
        {
            Debug.LogError($"{gameObject.name} has no Box Collider 2D to say how close a character has to be. Add one (set to Trigger) to the lever.", this);
            return false;
        }

        if (!triggerCollider.bounds.Contains(interacter.transform.position))
        {
            print($"{this.gameObject} is out of range");
            //Character is not within the range to interact
            return false;
        }
        
        print($"Interacting with: {this.gameObject} ");

        if (oneTime)
        {
            interactedWith = true;
        }
        else
        {
            interactedWith = !interactedWith;
        }

        return true;
    }

    // Whether the lever has been thrown, which is all another machine needs to show it.
    public bool IsThrown
    {
        get { return interactedWith; }
    }

    // For a machine that has joined someone else's game: the lever is shown thrown or not as the host says, and it moves to
    // match. What it switches is the host's to do.
    public void SetThrownFromNetwork(bool thrown)
    {
        interactedWith = thrown;
    }

    void Activate()
    {
        if (GameSession.IsClientOnly)
        {
            return;
        }

        var starting = !activated;
        activated = true;

        // One after another, if there's a delay between them.
        if (delayBetweenObjects > 0f)
        {
            if (starting)
            {
                sequence.Begin(switchableObjects, true, delayBetweenObjects, false, usable);
            }

            sequence.Tick(switchableObjects, staysActiveWhenOn, usable);
            return;
        }

        sequence.Cancel();

        foreach (var switchableObject in switchableObjects)
        {
            if (!switchableObject)
            {
                continue;
            }

            if (switchableObject.enabled)
            {
                switchableObject.Activate();
            }
        }
    }

    void Deactivate()
    {
        if (GameSession.IsClientOnly)
        {
            return;
        }

        activated = false;

        // One after another, if there's a delay between them and they're to go off that way.
        if (delayBetweenObjects > 0f && switchOffOrder != SwitchOffOrder.AllAtOnce)
        {
            sequence.Begin(switchableObjects, false, delayBetweenObjects, switchOffOrder == SwitchOffOrder.ReverseOrder, usable);
            sequence.Tick(switchableObjects, false, usable);
            return;
        }

        sequence.Cancel();

        foreach (var switchableObject in switchableObjects)
        {
            if (!switchableObject)
            {
                continue;
            }

            if (switchableObject.enabled)
            {
                switchableObject.Deactivate();
            }
        }
    }
}
