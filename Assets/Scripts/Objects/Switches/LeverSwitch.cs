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
    [Min(0.05f)]
    [Tooltip("How many seconds the lever takes to swing from off to on by itself, after a press. Not used with Hold To Operate, which has its own times.")]
    public float secondsToMove = 2f;
    [Tooltip("Switch can only be used once, and stays used.")]
    public bool oneTime;
    [Tooltip("When the switch is on, it will constantly reapply the switch action.")]
    public bool staysActiveWhenOn;

    [Tooltip("The things this lever switches. With a delay set below, they are switched in the order of this list.")]
    public SwitchableObject[] switchableObjects;

    [Header("Hold To Operate")]
    [Tooltip("The character has to keep the Interact button held for the lever to move. It goes as far as they hold it, taking Seconds To Hold to " +
             "go all the way to the other state, and goes back to where it was if they let go before then. Things that can be set to a part of their " +
             "range (a door, a liquid level, a light) follow it, so a lever held half way has a door half open. Without this, one press sets it going " +
             "all the way by itself, taking Seconds To Move.")]
    public bool holdToOperate;

    [Min(0.1f)]
    [Tooltip("Hold To Operate only. How many seconds the Interact button has to be held to take the lever all the way to the other state.")]
    public float secondsToHold = 3f;

    [Min(0.1f)]
    [Tooltip("Hold To Operate only. How many seconds it takes to go all the way back to where it was, when it's let go part of the way.")]
    public float secondsToReturn = 1.5f;

    [Tooltip("Hold To Operate only. For finding out why a character lets go: writes to the Console when one takes hold of the lever and when they " +
             "let go, with the reason.")]
    public bool logHolding;

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

    // Whether a thing is switched by the lever being thrown. With Hold To Operate, those that can take an amount follow how far the lever is
    // instead (see PassOnPosition), and aren't switched on and off at the ends as well.
    private Func<SwitchableObject, bool> usable;

    private bool Counts(SwitchableObject thing)
    {
        return thing && thing.enabled && !(holdToOperate && thing.AcceptsAmount);
    }

    // Holding the lowest signal is for the things that can take an amount, which are the very ones Counts leaves out with Hold To Operate.
    private static readonly Func<SwitchableObject, bool> EnabledThing = thing => thing && thing.enabled;

    private float angle;
    private bool interactedWith;
    private bool activated;
    private float leftAngle;
    private float rightAngle;

    private BoxCollider2D triggerCollider;

    // How many degrees a second the lever swings by itself, to take Seconds To Move to go all the way.
    private float SwingPerSecond
    {
        get { return degreesOfMovement / Mathf.Max(0.05f, secondsToMove); }
    }

    // Hold To Operate. How far the lever is from its off pose to its on pose, from 0 to 1, whatever state it was last left in. Who is
    // holding it, if anyone. And whether amounts are being given to the things that take one, so that they're let go of afterwards.
    private const float PositionMargin = 0.0001f;
    private float position;
    private float networkPosition;
    private Character holder;

    // An ordinary lever: who pressed it, and is working it until it gets where it's going.
    private Character swinger;
    private bool passingAmount;
    private float offAngle;
    private float onAngle;


    void Start()
    {
        leftAngle = 360 + degreesOfMovement * 0.5f;
        rightAngle = 360 - degreesOfMovement * 0.5f;
        
        angle = leftToRight
                    ? leftAngle
                    : rightAngle;
        
        offAngle = leftToRight ? leftAngle : rightAngle;
        onAngle = leftToRight ? rightAngle : leftAngle;
        usable = Counts;

        triggerCollider = GetComponent<BoxCollider2D>();
    }

    
    void Update()
    {
        // The things that are waiting for their turn.
        if (sequence.Pending && !GameSession.IsClientOnly)
        {
            sequence.Tick(switchableObjects, false, usable);
        }

        if (holdToOperate)
        {
            UpdateHeld();
        }
        else if (interactedWith)
        {
            if (leftToRight)
            {
                angle -= SwingPerSecond * Time.deltaTime;
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
                angle += SwingPerSecond * Time.deltaTime;
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
                angle += SwingPerSecond * Time.deltaTime;
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
                angle -= SwingPerSecond * Time.deltaTime;
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
        
        if (swinger && Mathf.Approximately(angle, interactedWith ? onAngle : offAngle))
        {
            ReleaseSwinger();
        }

        // Off, with a lowest signal to send: the things are held at it, all the time, and not just when the lever is thrown. That's also how
        // they start. Not while they're being switched off one after another, which would put them all there at once.
        if (!activated && SwitchSignal.HoldsMinimum(signalMode, minimumSignal) && !sequence.Pending && !GameSession.IsClientOnly &&
            !(holdToOperate && position > PositionMargin))
        {
            SwitchSignal.HoldMinimum(switchableObjects, minimumSignal, EnabledThing, this);
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

        if (holdToOperate)
        {
            // Taken hold of, and moved for as long as it's held. One character at a time, and not one that has been used for good.
            if ((holder && holder != interacter) || (oneTime && interactedWith))
            {
                return false;
            }

            holder = interacter;
            holder.SetOperating(true);

            if (logHolding)
            {
                Debug.Log($"{name}: {holder.name} took hold of the lever, at {position:0.00} of the way.", this);
            }

            return true;
        }

        if (oneTime)
        {
            interactedWith = true;
        }
        else
        {
            interactedWith = !interactedWith;
        }

        // He's working it until it has swung all the way.
        ReleaseSwinger();
        swinger = interacter;
        swinger.SetOperating(true);

        return true;
    }

    // Whether the lever has been thrown, which is all another machine needs to show it.
    public bool IsThrown
    {
        get { return interactedWith; }
    }

    // How far the lever is from off to on, from 0 to 1, for a lever that has to be held. (Another machine needs it to show where the lever is.)
    public float Position
    {
        get { return position; }
    }

    // For a machine that has joined someone else's game: where the host says the lever is. Only for a lever that has to be held.
    public void SetPositionFromNetwork(float newPosition)
    {
        networkPosition = Mathf.Clamp01(newPosition);
    }

    // For a machine that has joined someone else's game: the lever is shown thrown or not as the host says, and it moves to
    // match. What it switches is the host's to do.
    public void SetThrownFromNetwork(bool thrown)
    {
        interactedWith = thrown;
    }

    // Hold To Operate: moves the lever while it's held, and back to where it was when it isn't. Held, it goes to the other state, and when it
    // gets all the way there that's its state, and it has to be let go of and taken hold of again to go back.
    private void UpdateHeld()
    {
        // In someone else's game the host moves the lever. This only shows where it says it is, and its position only comes a few times a second.
        if (GameSession.IsClientOnly)
        {
            position = Mathf.Lerp(position, networkPosition, 1f - Mathf.Exp(-15f * Time.deltaTime));
            angle = Mathf.Lerp(offAngle, onAngle, position);
            return;
        }

        if (holder)
        {
            var problem = HoldProblem(holder);

            if (problem != null)
            {
                if (logHolding)
                {
                    Debug.Log($"{name}: {holder.name} let go of the lever at {position:0.00} of the way, because {problem}.", this);
                }

                ReleaseHolder();
            }
        }

        var rest = interactedWith ? 1f : 0f;
        var held = holder != null;
        var goal = held ? 1f - rest : rest;
        var seconds = held ? secondsToHold : secondsToReturn;

        position = Mathf.MoveTowards(position, goal, Time.deltaTime / Mathf.Max(0.05f, seconds));
        angle = Mathf.Lerp(offAngle, onAngle, position);

        if (held && position == goal)
        {
            interactedWith = !interactedWith;

            if (logHolding)
            {
                Debug.Log($"{name}: the lever got all the way to {(interactedWith ? "on" : "off")}.", this);
            }

            ReleaseHolder();

            if (interactedWith)
            {
                Activate();
            }
            else
            {
                Deactivate();
            }
        }
        else if (interactedWith && position >= 1f && staysActiveWhenOn)
        {
            Activate();
        }

        PassOnPosition();
    }

    // Whoever was working the lever isn't any more.
    private void ReleaseHolder()
    {
        if (holder)
        {
            holder.SetOperating(false);
        }

        holder = null;
        ReleaseSwinger();
    }

    private void ReleaseSwinger()
    {
        if (swinger)
        {
            swinger.SetOperating(false);
        }

        swinger = null;
    }

    void OnDisable()
    {
        ReleaseHolder();
    }

    // Why a character isn't working the lever any more, or null if they still are: they have to be holding the button, in range, and in
    // the game.
    private string HoldProblem(Character character)
    {
        if (!character.isActiveAndEnabled)
        {
            return "they're switched off";
        }

        if (!character.InteractHeld)
        {
            return "the Interact button isn't held";
        }

        if (!character.InGame || character.InTransit)
        {
            return "they're put away or in a teleporter";
        }

        if (!triggerCollider || !triggerCollider.bounds.Contains(character.transform.position))
        {
            return "they're out of the lever's trigger area";
        }

        return null;
    }

    // Gives the things that can be moved by an amount how far the lever is, and lets them go when it's back where it started.
    private void PassOnPosition()
    {
        var any = position > PositionMargin;

        if (!any && !passingAmount)
        {
            return;
        }

        foreach (var thing in switchableObjects)
        {
            if (!thing || !thing.enabled || !thing.AcceptsAmount)
            {
                continue;
            }

            if (any)
            {
                thing.Signal(this, signalMode, true, SwitchSignal.Pressed(signalMode, position, minimumSignal, maximumSignal));
            }
            else
            {
                thing.Signal(this, signalMode, false, AmountFor(false));
            }
        }

        passingAmount = any;
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

            if (Counts(switchableObject))
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

            if (Counts(switchableObject))
            {
                switchableObject.Signal(this, signalMode, false, AmountFor(false));
            }
        }
    }
}
