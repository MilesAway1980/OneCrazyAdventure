using System;
using System.Collections.Generic;
using Characters;
using UnityEngine;

// A pad on the floor that switches things while something is standing on it.
//
// The pad is moved by this script, not by physics, so it does exactly what it's told and nothing else:
//  - It works out how much weight is on it: the mass of whatever is resting on it, including anything stacked on those and
//    anything a character on it is carrying.
//  - The fraction of Minimum Weight that's on it is how far down it goes. Half the weight, and it stops half way down. Minimum
//    Weight or more, and it goes all the way.
//  - Resistance is how long it takes to go all the way down, however much weight is on it.
//  - When the weight goes it comes back up, at the same gentle speed every time, so it can't throw anyone into the air.
//  - However far it's set to go, it never moves faster than MostSpeed, and it speeds up and slows down gently, so a big Press
//    Distance only makes it slower, never violent.
//
// It switches things on once it has gone down as far as Percent Pressed To Activate, and off again when it comes back above that.
// With Allow Partial Presses, the things it switches that can move by an amount, such as a door, are given how far down the pad is
// instead, so they follow it.
//
// Needs a pad: a child with a Rigidbody2D, set to Kinematic. Tools > Buttons > Upgrade Button Prefab sets the project's Button
// prefab up that way.
public class ButtonSwitch : MonoBehaviour
{
    [Tooltip("The things this button switches. With a delay set below, they are switched in the order of this list.")]
    public SwitchableObject[] switchableObject;

    [Header("Signal")]
    [Tooltip("How this button's signal is put with the signals of the other switches that work the same things. Min/Max: it sends its own signal, from the Minimum Signal when it's off to the Maximum Signal when it's on, and a thing goes by the switch that spoke last. Add: while it's on, it adds the Maximum Signal to what the things get, along with every other switch that adds, so four that are each 25 give 25, 50, 75, 100. Subtract: takes the Maximum Signal off while it's on.")]
    public SignalMode signalMode = SignalMode.MinMax;

    [Range(0, 100)]
    [Tooltip("Min/Max only. How much of its full effect the things get while the button is off, as a percentage. 0 is the ordinary off. Above 0, the things that can be set to a part of their range (a door, a liquid level, a moving platform, a light, a fan) are held at that part while it's off, instead of at nothing. Add and Subtract add or take off nothing while off.")]
    public int minimumSignal = 0;

    [Range(0, 100)]
    [Tooltip("How much of its full effect the things get while it's on, as a percentage. 100 is the ordinary on. Below 100, the things that can be set to a part of their range go that far and no further. With Allow Partial Presses, it's how much they get with the pad all the way down, and less for less. In Add and Subtract this is how much it adds or takes off. Anything that can only be on or off is switched as usual (on once all the Adds reach 100), whatever these are set to.")]
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

    // Switches the things one after another, when there's a delay before them or between them.
    private readonly SwitchSequence sequence = new SwitchSequence();

    private bool Sequenced
    {
        get { return initialDelay > 0f || delayBetweenObjects > 0f; }
    }
    private Func<SwitchableObject, bool> counts;

    // Whether a thing takes part: it's there, it's switched on, and it isn't one that follows how far the pad is pressed.
    private bool Counts(SwitchableObject obj)
    {
        return obj && obj.enabled && !(passOnAmount && obj.AcceptsAmount);
    }

    private Func<SwitchableObject, bool> CountsFunction
    {
        get { return counts ?? (counts = Counts); }
    }

    [Range(0, 100)]
    [Tooltip("How far down the pad has to go, as a percentage of the way, to switch things on. 0 is as soon as it moves at all. " +
             "100 is all the way down. Remember that it only goes as far down as the weight on it allows: with this at 50, " +
             "half of Minimum Weight is enough, and anything less never switches it.")]
    public int percentPressedToActivate = 55;

    [Tooltip("Once it has been pressed far enough, it stays down, and stays on, even when the weight goes.")]
    public bool stayPressed;

    [Tooltip("While it's pressed far enough, it keeps switching things on every frame. Things that switch themselves off, " +
             "after a time or straight away, are held on for as long as it's pressed, and only start to go off once it's let go. " +
             "Without it, things are switched on once, and are left to switch themselves off. Needed for doors and the like " +
             "that should close again when the weight is taken off.")]
    public bool staysActiveWhenOn;

    [Header("The pad")]
    [Tooltip("What goes down. A child of this with a Rigidbody2D, which is made kinematic. Found by the name Pad if it's empty.")]
    public Rigidbody2D pad;

    [Range(0f, 100f)]
    [Tooltip("How far the pad goes down when it's pressed all the way, as a percentage of the way from where it sits to the top of the base " +
             "(the other solid parts of this button, which the pad goes down into). 100 takes it flush with the base, a hair above, so that " +
             "what's standing on it is still on the pad. 50 is half way. 0 doesn't move it at all: it only has to be stood on to count as " +
             "pressed all the way. With no base, the pad's whole height is the way. However far, the pad never moves faster than a gentle " +
             "speed, so a long way just takes longer.")]
    public float pressPercent = 100f;

    [Header("How it feels")]
    [Min(0f)]
    [Tooltip("The weight, as the mass of what's on it, that takes the pad all the way down. Less than this, and it goes down " +
             "by the same fraction. 0 means any weight at all takes it all the way.")]
    public float minimumWeight = 5f;

    [Min(0f)]
    [Tooltip("How much it resists being pressed: the time, in seconds, it takes to go all the way down when there is enough " +
             "weight on it. The higher it is, the longer it takes to switch anything on.")]
    public float resistance = 0.3f;

    [Min(0.05f)]
    [Tooltip("The time, in seconds, it takes to come all the way back up when the weight is taken off.")]
    public float returnTime = 0.5f;

    [Tooltip("Allows for a partial amount of the press to be passed on to things that this switches.  This allows switchable objects, " +
             "such as a door, to move a fraction of the button press.  Example: A door that needs a mass of 50 to open, with a mass of 20 " +
             "on the button, opens 40 percent of the way." +
             "Percent Pressed To Activate doesn't apply to these, as they move as soon as the pad does. " +
             "Everything else it switches is still switched on and off as usual.")]
    [InspectorLabel("Allow Partial Presses")]
    public bool passOnAmount;

    // The quickest the pad is allowed to go down, in seconds for the whole way, however low Resistance is. Faster and whatever is
    // standing on it would be left in the air.
    private const float FastestPress = 0.05f;

    // The fastest the pad ever moves, in units a second, and how quickly it gets up to speed and slows down, in units a second
    // each second. The speed is low enough that nobody is thrown by a pad that stops, however far it's set to go. The acceleration
    // is under what pulls a character down, so one standing on a pad that's going down stays on it.
    private const float MostSpeed = 2f;
    private const float Acceleration = 10f;

    // How far above the top of the base the pad stops when it's pressed all the way down, in units, so that it's still what's
    // being stood on, and not the base.
    private const float FlushGap = 0.02f;

    // Going down less than this, in units, nothing can be thrown by it, so it's only timed and isn't held to a speed and acceleration.
    private const float SmallTravel = 0.05f;

    // How long, in seconds, the weight on the pad is remembered for after it has gone down. Whatever is standing on a pad that is
    // going down can lose touch with it for a moment, and without this the pad would take that for them having left and bounce.
    private const float WeightMemory = 0.12f;

    // How far, in the contacts' normals, a contact has to be along the way the pad faces to be something resting on it and not
    // something pushing against its side.
    private const float FlatEnough = 0.7f;

    // How many bodies deep a stack of things on the pad is looked into.
    private const int StackDepth = 4;
    private const int ContactsPerBody = 24;

    private Vector3 restLocal;
    private float fullTravel;
    private float depth;
    private float speed;
    private float heldDepth;
    private bool latched;
    private bool pressedEnough;
    private bool activated;
    private bool passingAmount;

    private float recentWeight;
    private float recentUntil;

    private ContactFilter2D filter;
    private readonly HashSet<Rigidbody2D> weighed = new HashSet<Rigidbody2D>();
    private readonly ContactPoint2D[][] contactBuffers = new ContactPoint2D[StackDepth + 1][];

    // The pad, for other scripts. It's null if this has none.
    public Rigidbody2D Pad
    {
        get { return pad ? pad : FindPad(); }
    }

    private Rigidbody2D FindPad()
    {
        var child = transform.Find("Pad");
        return child ? child.GetComponent<Rigidbody2D>() : null;
    }

    void Reset()
    {
        pad = FindPad();
    }

    void Start()
    {
        if (!pad)
        {
            pad = FindPad();
        }

        if (!pad)
        {
            Debug.LogError($"{name}: this button has no pad. Give it a child with a Rigidbody2D, and set Pad to it.", this);
            enabled = false;
            return;
        }

        if (GetComponentInChildren<SpringJoint2D>())
        {
            Debug.LogWarning($"{name}: this button still has the Spring Joint 2D it used to be moved by. The pad is moved by the " +
                             "script now. Run Tools > Buttons > Upgrade Button Prefab, or remove the joint.", this);
        }

        // Where the pad sits when nothing is on it, so it can come back to exactly there, and how far it can go down from there.
        restLocal = transform.InverseTransformPoint(pad.transform.position);
        fullTravel = FullTravel(pad);

        filter = new ContactFilter2D { useTriggers = false };
        pad.sleepMode = RigidbodySleepMode2D.NeverSleep;
        pad.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void FixedUpdate()
    {
        // In someone else's game the host works out how far down the pad is. Where the pad is comes from the network.
        if (GameSession.IsClientOnly || !pad)
        {
            return;
        }

        // Moved by this and nothing else. (The network gives a body back the type it had when it stops, which could be dynamic.)
        if (pad.bodyType != RigidbodyType2D.Kinematic)
        {
            pad.bodyType = RigidbodyType2D.Kinematic;
        }

        MoveDepthTowards(DepthWanted(WeightOnPad()));

        var rest = (Vector2)transform.TransformPoint(restLocal);
        pad.MovePosition(rest - (Vector2)transform.up * (Travel * depth));

        // The tiny allowance is for the float sums, so that 100 percent is reachable.
        pressedEnough = depth > 0.0001f && depth >= percentPressedToActivate / 100f - 0.0001f;

        if (pressedEnough && stayPressed)
        {
            latched = true;
        }

        if (latched)
        {
            heldDepth = Mathf.Max(heldDepth, depth);
        }
    }

    void Update()
    {
        if (GameSession.IsClientOnly)
        {
            return;
        }

        // The things that are waiting for their turn.
        if (sequence.Pending)
        {
            sequence.Tick(switchableObject, false, CountsFunction);
        }

        if (passOnAmount)
        {
            PassOnAmount();
        }

        // Not pressed, with a lowest signal to send: the things that don't follow the pad are held at it, all the time. Not while they're
        // being switched off one after another.
        if (!activated && SwitchSignal.HoldsMinimum(signalMode, minimumSignal) && !sequence.Pending)
        {
            SwitchSignal.HoldMinimum(switchableObject, minimumSignal, CountsFunction, this);
        }

        if (pressedEnough)
        {
            if (!activated || staysActiveWhenOn)
            {
                Activate();
            }
        }
        else if (activated)
        {
            Deactivate();
        }
    }

    // How far, in units, the pad goes down when it's pressed all the way.
    private float Travel
    {
        get { return fullTravel * pressPercent / 100f; }
    }

    // The top of a box along a direction, and the bottom.
    private static float TopAlong(Bounds bounds, Vector2 direction)
    {
        return Vector2.Dot(bounds.center, direction) + Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.y) * bounds.extents.y;
    }

    private static float BottomAlong(Bounds bounds, Vector2 direction)
    {
        return Vector2.Dot(bounds.center, direction) - Mathf.Abs(direction.x) * bounds.extents.x - Mathf.Abs(direction.y) * bounds.extents.y;
    }

    // How far the pad can go down from where it is, in units: to a hair above the top of the base, which is whatever else solid on this
    // button is under the top of the pad and overlaps it. With no base it's the pad's whole height. Worked out at rest. (Boxes
    // that are turned are measured by the space they take up, which is close enough for a pad that's meant to be flat.)
    private float FullTravel(Rigidbody2D body)
    {
        var up = (Vector2)transform.up;
        var padColliders = new Collider2D[16];
        var count = body.GetAttachedColliders(padColliders);

        var padTop = float.NegativeInfinity;
        var padBottom = float.PositiveInfinity;
        var padBounds = new Bounds();
        var found = false;

        for (var i = 0; i < count; i++)
        {
            if (padColliders[i].isTrigger || !padColliders[i].enabled)
            {
                continue;
            }

            var bounds = padColliders[i].bounds;
            padTop = Mathf.Max(padTop, TopAlong(bounds, up));
            padBottom = Mathf.Min(padBottom, BottomAlong(bounds, up));

            if (found)
            {
                padBounds.Encapsulate(bounds);
            }
            else
            {
                padBounds = bounds;
                found = true;
            }
        }

        if (!found)
        {
            return 0f;
        }

        var baseTop = float.NegativeInfinity;

        foreach (var other in GetComponentsInChildren<Collider2D>())
        {
            if (other.isTrigger || !other.enabled || other.attachedRigidbody == body ||
                (other.attachedRigidbody && other.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic))
            {
                continue;
            }

            var bounds = other.bounds;
            var top = TopAlong(bounds, up);

            // Under the top of the pad, and under the pad, not just beside it.
            var overlaps = Mathf.Abs(bounds.center.x - padBounds.center.x) <= bounds.extents.x + padBounds.extents.x &&
                           Mathf.Abs(bounds.center.y - padBounds.center.y) <= bounds.extents.y + padBounds.extents.y;

            if (overlaps && top < padTop)
            {
                baseTop = Mathf.Max(baseTop, top);
            }
        }

        return float.IsNegativeInfinity(baseTop) ? padTop - padBottom : Mathf.Max(0f, padTop - baseTop - FlushGap);
    }

    // Takes the pad towards the depth it's wanted at, no faster than the time it's set to take, and never faster than is safe.
    private void MoveDepthTowards(float wanted)
    {
        var travel = Travel;

        // Hardly any way to go, or none: nothing can be thrown by it, so it's only the time that it takes that matters.
        if (travel < SmallTravel)
        {
            var time = Mathf.Max(wanted > depth ? resistance : returnTime, FastestPress);

            depth = Mathf.MoveTowards(depth, wanted, Time.fixedDeltaTime / time);
            speed = 0f;
            return;
        }

        // How far there is to go, in units, which is down if it's more than nothing.
        var left = (wanted - depth) * travel;

        if (Mathf.Abs(left) < 0.0005f)
        {
            depth = wanted;
            speed = 0f;
            return;
        }

        var seconds = left > 0f ? Mathf.Max(resistance, FastestPress) : Mathf.Max(returnTime, FastestPress);
        var cruise = Mathf.Min(travel / seconds, MostSpeed);

        // Slow down in time to stop where it's meant to.
        var stoppable = Mathf.Sqrt(2f * Acceleration * Mathf.Abs(left));
        var wantedSpeed = Mathf.Sign(left) * Mathf.Min(cruise, stoppable);

        speed = Mathf.MoveTowards(speed, wantedSpeed, Acceleration * Time.fixedDeltaTime);

        var step = speed * Time.fixedDeltaTime;

        // Got there this step.
        if (Mathf.Sign(step) == Mathf.Sign(left) && Mathf.Abs(step) >= Mathf.Abs(left))
        {
            depth = wanted;
            speed = 0f;
            return;
        }

        depth = Mathf.Clamp01(depth + step / travel);

        if (depth <= 0f || depth >= 1f)
        {
            speed = 0f;
        }
    }

    // How far down, from 0 to 1, the weight on the pad would take it.
    private float DepthWanted(float weight)
    {
        float wanted;

        if (weight <= 0f)
        {
            wanted = 0f;
        }
        else if (minimumWeight <= 0f)
        {
            wanted = 1f;
        }
        else
        {
            wanted = Mathf.Clamp01(weight / minimumWeight);
        }

        // Once it has been pressed down to stay, it doesn't come back up past where it got to.
        return latched ? Mathf.Max(wanted, heldDepth) : wanted;
    }

    // The weight on the pad, which drops back only after it has stayed lower for a moment.
    private float WeightOnPad()
    {
        var now = Weigh();

        if (now >= recentWeight || Time.time >= recentUntil)
        {
            recentWeight = now;
            recentUntil = Time.time + WeightMemory;
        }

        return recentWeight;
    }

    // The mass of everything resting on the pad, and of what's resting on that.
    private float Weigh()
    {
        weighed.Clear();
        weighed.Add(pad);

        var up = (Vector2)transform.up;
        var total = 0f;
        var count = pad.GetContacts(filter, Buffer(0));

        for (var i = 0; i < count; i++)
        {
            var contact = contactBuffers[0][i];
            var body = contact.rigidbody == pad ? contact.otherRigidbody : contact.rigidbody;

            if (body && IsAbove(contact, pad, up))
            {
                total += WeighBody(body, up, 1);
            }
        }

        return total;
    }

    // The mass of a body that's resting on the pad, or on something that is, with what's resting on it and what it's carrying.
    private float WeighBody(Rigidbody2D body, Vector2 up, int depthInStack)
    {
        // Something that is held still, or moves by itself, isn't weight, and each body only counts once.
        if (body.bodyType != RigidbodyType2D.Dynamic || !weighed.Add(body))
        {
            return 0f;
        }

        var total = body.mass;

        // A crate carried by a character isn't touching the pad, but it's being held up by it all the same.
        if (body.TryGetComponent<Character>(out var character) && character.CarriedObject)
        {
            var carried = character.CarriedObject.GetComponent<Rigidbody2D>();

            if (carried)
            {
                total += WeighBody(carried, up, depthInStack + 1);
            }
        }

        if (depthInStack > StackDepth)
        {
            return total;
        }

        var count = body.GetContacts(filter, Buffer(depthInStack));

        for (var i = 0; i < count; i++)
        {
            var contact = contactBuffers[depthInStack][i];
            var other = contact.rigidbody == body ? contact.otherRigidbody : contact.rigidbody;

            if (other && IsAbove(contact, body, up))
            {
                total += WeighBody(other, up, depthInStack + 1);
            }
        }

        return total;
    }

    // Whether a contact is something resting on the top of a body, rather than against its side or under it.
    private static bool IsAbove(ContactPoint2D contact, Rigidbody2D body, Vector2 up)
    {
        return Mathf.Abs(Vector2.Dot(contact.normal, up)) >= FlatEnough &&
               Vector2.Dot(contact.point - body.worldCenterOfMass, up) > 0f;
    }

    private ContactPoint2D[] Buffer(int level)
    {
        level = Mathf.Min(level, StackDepth);

        if (contactBuffers[level] == null)
        {
            contactBuffers[level] = new ContactPoint2D[ContactsPerBody];
        }

        return contactBuffers[level];
    }

    // Gives the things that can be moved by an amount how far down the pad is, and lets them go when it's back up.
    private void PassOnAmount()
    {
        var any = depth > 0.0001f;

        // With a lowest signal to send, they're held at it while the pad is up, and not only let go of.
        var holding = SwitchSignal.HoldsMinimum(signalMode, minimumSignal);

        if (!any && !passingAmount && !holding)
        {
            return;
        }

        foreach (var obj in switchableObject)
        {
            if (!obj || !obj.enabled || !obj.AcceptsAmount)
            {
                continue;
            }

            if (any)
            {
                obj.Signal(this, signalMode, true, SwitchSignal.Pressed(signalMode, depth, minimumSignal, maximumSignal));
            }
            else
            {
                obj.Signal(this, signalMode, false, AmountFor(false));
            }
        }

        passingAmount = any;
    }

    // The amount, from 0 to 1, to give the things that can take one while it's on or off, or below 0 for no amount: switching them on or
    // off as usual.
    private float AmountFor(bool on)
    {
        return SwitchSignal.Amount(signalMode, on, minimumSignal, maximumSignal);
    }

    private void Activate()
    {
        var starting = !activated;
        activated = true;

        // One after another, if there's a delay before them or between them.
        if (Sequenced)
        {
            if (starting)
            {
                sequence.Begin(switchableObject, true, initialDelay, delayBetweenObjects, false, CountsFunction, AmountFor(true), this, signalMode);
            }

            sequence.Tick(switchableObject, staysActiveWhenOn, CountsFunction);
            return;
        }

        sequence.Cancel();

        foreach (var obj in switchableObject)
        {
            if (!obj || (passOnAmount && obj.AcceptsAmount))
            {
                continue;
            }

            if (obj.enabled)
            {
                obj.Signal(this, signalMode, true, AmountFor(true));
            }
        }
    }

    private void Deactivate()
    {
        activated = false;

        // One after another, if there's a delay and they're to go off that way.
        if (Sequenced && switchOffOrder != SwitchOffOrder.AllAtOnce)
        {
            sequence.Begin(switchableObject, false, initialDelay, delayBetweenObjects, switchOffOrder == SwitchOffOrder.ReverseOrder, CountsFunction, AmountFor(false), this, signalMode);
            sequence.Tick(switchableObject, false, CountsFunction);
            return;
        }

        sequence.Cancel();

        foreach (var obj in switchableObject)
        {
            if (!obj || (passOnAmount && obj.AcceptsAmount))
            {
                continue;
            }

            if (obj.enabled)
            {
                obj.Signal(this, signalMode, false, AmountFor(false));
            }
        }
    }

    // Selected in the scene view: an arrow to each of the things this switches, where the pad sits, and how far down it goes.
    void OnDrawGizmosSelected()
    {
        SwitchLinkGizmo.Draw(transform.position, switchableObject, Sequenced);

        var body = Pad;

        if (!body)
        {
            return;
        }

        var top = Application.isPlaying
            ? (Vector2)transform.TransformPoint(restLocal)
            : (Vector2)body.transform.position;
        var travel = Application.isPlaying ? Travel : FullTravel(body) * pressPercent / 100f;
        var bottom = top - (Vector2)transform.up * travel;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(top, bottom);
        Gizmos.DrawWireSphere(bottom, 0.1f);

        // Where it switches on.
        Gizmos.color = Color.green;
        var activates = top - (Vector2)transform.up * (travel * percentPressedToActivate / 100f);
        Gizmos.DrawLine(activates + Vector2.left * 0.3f, activates + Vector2.right * 0.3f);
    }
}
