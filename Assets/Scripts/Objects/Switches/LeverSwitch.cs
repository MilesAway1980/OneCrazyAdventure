using System;
using Characters;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class LeverSwitch : InteractableObject
{
    [Range(0, 180f)]
    [Tooltip("How far, in degrees, the lever swings between off and on.")]
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

    [Tooltip("The things this lever switches. With a delay set below, they are switched in the order of this list.")]
    public SwitchableObject[] switchableObjects;

    [Header("Signal")]
    [Tooltip("How this lever's signal is put with the signals of the other switches that work the same things. Min/Max: it sends its own signal, from the Minimum Signal when it's off to the Maximum Signal when it's on, and a thing goes by the switch that spoke last. Add: while it's on, it adds the Maximum Signal to what the things get, along with every other switch that adds, so four that are each 25 give 25, 50, 75, 100. Subtract: takes the Maximum Signal off while it's on.")]
    public SignalMode signalMode = SignalMode.MinMax;

    [Range(0, 100)]
    [Tooltip("Min/Max only. How much of its full effect the things get while the lever is off, as a percentage. 0 is the ordinary off. Above 0, the things that can be set to a part of their range (a door, a liquid level, a moving platform, a light, a fan) are held at that part while the lever is off, instead of at nothing: 50 with a liquid level, and it only goes down half way. Add and Subtract add or take off nothing while off.")]
    public int minimumSignal = 0;

    [Range(0, 100)]
    [Tooltip("How much of its full effect the things get while the lever is on, as a percentage. 100 is the ordinary on. Below 100, the things that can be set to a part of their range go that far and no further. In Add and Subtract this is how much the lever adds or takes off. Anything that can only be on or off is switched as usual (on once all the Adds reach 100), whatever these are set to.")]
    public int maximumSignal = 100;

    [Header("Timing")]
    [Min(0f)]
    [Tooltip("How many seconds it waits before the first thing is switched. 0 switches the first at once, and the rest follow, Delay Between Objects apart. The same wait comes before the first when they go off one after another.")]
    public float initialDelay;

    [Min(0f)]
    [Tooltip("When this switches more than one thing, how many seconds it waits between one and the next, in the order of the list, starting from the first: the second this long after the first, the third twice as long, and so on. For lights that come on one by one. 0 switches them all at the same moment.")]
    public float delayBetweenObjects;

    [Tooltip("How the things are switched off, if there's a delay between them: all at once, one after another in the same order, or one after another with the last first. Only matters when there's an Initial Delay or a Delay Between Objects.")]
    public SwitchOffOrder switchOffOrder = SwitchOffOrder.AllAtOnce;

    protected override System.Collections.Generic.IEnumerable<SwitchableObject> SwitchTargets
    {
        get { return switchableObjects; }
    }

    // The arrows to the things are numbered in the order they're switched, when there's a delay between them.
    protected override bool NumberSwitchTargets
    {
        get { return Sequenced; }
    }

    // Switches the things one after another, when there's a delay before them or between them.
    private readonly SwitchSequence sequence = new SwitchSequence();

    private bool Sequenced
    {
        get { return initialDelay > 0f || delayBetweenObjects > 0f; }
    }
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
        
        // Off, with a lowest signal to send: the things are held at it, all the time, and not just when the lever is thrown. That's also how
        // they start. Not while they're being switched off one after another, which would put them all there at once.
        if (!activated && SwitchSignal.HoldsMinimum(signalMode, minimumSignal) && !sequence.Pending && !GameSession.IsClientOnly)
        {
            SwitchSignal.HoldMinimum(switchableObjects, minimumSignal, usable, this);
        }

        pivotObject.transform.eulerAngles = new Vector3(0, 0, angle);
    }

    // The amount, from 0 to 1, to give the things that can take one while the lever is on or off, or below 0 for no amount: switching
    // them on or off as usual. The ordinary on and off are used when the signals are 0 and 100, because they mean something more to some
    // things (a platform that is on goes round its path, and one that is only taken part of the way doesn't).
    private float AmountFor(bool on)
    {
        return SwitchSignal.Amount(signalMode, on, minimumSignal, maximumSignal);
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

        // One after another, if there's a delay before them or between them.
        if (Sequenced)
        {
            if (starting)
            {
                sequence.Begin(switchableObjects, true, initialDelay, delayBetweenObjects, false, usable, AmountFor(true), this, signalMode);
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
                switchableObject.Signal(this, signalMode, true, AmountFor(true));
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

        // One after another, if there's a delay and they're to go off that way.
        if (Sequenced && switchOffOrder != SwitchOffOrder.AllAtOnce)
        {
            sequence.Begin(switchableObjects, false, initialDelay, delayBetweenObjects, switchOffOrder == SwitchOffOrder.ReverseOrder, usable, AmountFor(false), this, signalMode);
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
                switchableObject.Signal(this, signalMode, false, AmountFor(false));
            }
        }
    }
}
