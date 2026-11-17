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
// With Pass On Amount, the things it switches that can move by an amount, such as a door, are given how far down the pad is
// instead, so they follow it.
//
// Needs a pad: a child with a Rigidbody2D, set to Kinematic. Tools > Buttons > Upgrade Button Prefab sets the project's Button
// prefab up that way.
public class ButtonSwitch : MonoBehaviour
{
    public SwitchableObject[] switchableObject;

    [Range(0, 100)]
    [Tooltip("How far down the pad has to go, as a percentage of the way, to switch things on. 0 is as soon as it moves at all. " +
             "100 is all the way down. Remember that it only goes as far down as the weight on it allows: with this at 50, " +
             "half of Minimum Weight is enough, and anything less never switches it.")]
    public int percentPressedToActivate = 55;

    [Tooltip("Once it has been pressed far enough, it stays down, and stays on, even when the weight goes.")]
    public bool stayPressed;

    [Tooltip("While it's pressed far enough, it keeps switching things on, every frame. Things that switch themselves off, " +
             "after a time or straight away, are held on for as long as it's pressed, and only start to go off once it's let go. " +
             "Without it, things are switched on once, and are left to switch themselves off. Needed for doors and the like " +
             "that should close again when the weight is taken off.")]
    public bool staysActiveWhenOn;

    [Header("The pad")]
    [Tooltip("What goes down. A child of this with a Rigidbody2D, which is made kinematic. Found by the name Pad if it's empty.")]
    public Rigidbody2D pad;

    [Range(0.01f, 2f)]
    [Tooltip("How far, in world units, the pad goes down when it's pressed all the way. It can't be more than 2: the pad is only " +
             "one unit thick. However it's set, the pad never moves faster than a gentle speed, so a long way just takes longer.")]
    public float pressDistance = 0.5f;

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

    [Tooltip("Things this switches that can be moved by an amount, such as a door, are given how far down the pad is, from none " +
             "to all, and follow it. A door that needs a mass of 50 to open, with a mass of 20 on the button, opens 40 percent of " +
             "the way. Percent Pressed To Activate doesn't apply to those, as they move as soon as the pad does. " +
             "Everything else it switches is still switched on and off as usual.")]
    public bool passOnAmount;

    // The quickest the pad is allowed to go down, in seconds for the whole way, however low Resistance is. Faster and whatever is
    // standing on it would be left in the air.
    private const float FastestPress = 0.05f;

    // The fastest the pad ever moves, in units a second, and how quickly it gets up to speed and slows down, in units a second
    // each second. The speed is low enough that nobody is thrown by a pad that stops, however far it's set to go. The acceleration
    // is under what pulls a character down, so one standing on a pad that's going down stays on it.
    private const float MostSpeed = 2f;
    private const float Acceleration = 10f;

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

        // Where the pad sits when nothing is on it, so it can come back to exactly there.
        restLocal = transform.InverseTransformPoint(pad.transform.position);

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
        pad.MovePosition(rest - (Vector2)transform.up * (pressDistance * depth));

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

        if (passOnAmount)
        {
            PassOnAmount();
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

    // Takes the pad towards the depth it's wanted at, no faster than the time it's set to take, and never faster than is safe.
    private void MoveDepthTowards(float wanted)
    {
        // How far there is to go, in units, which is down if it's more than nothing.
        var left = (wanted - depth) * pressDistance;

        if (Mathf.Abs(left) < 0.0005f)
        {
            depth = wanted;
            speed = 0f;
            return;
        }

        var seconds = left > 0f ? Mathf.Max(resistance, FastestPress) : Mathf.Max(returnTime, FastestPress);
        var cruise = Mathf.Min(pressDistance / seconds, MostSpeed);

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

        depth = Mathf.Clamp01(depth + step / pressDistance);

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

        if (!any && !passingAmount)
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
                obj.SetAmount(depth);
            }
            else
            {
                obj.Deactivate();
            }
        }

        passingAmount = any;
    }

    private void Activate()
    {
        activated = true;
        foreach (var obj in switchableObject)
        {
            if (!obj || (passOnAmount && obj.AcceptsAmount))
            {
                continue;
            }

            if (obj.enabled)
            {
                obj.Activate();
            }
        }
    }

    private void Deactivate()
    {
        activated = false;
        foreach (var obj in switchableObject)
        {
            if (!obj || (passOnAmount && obj.AcceptsAmount))
            {
                continue;
            }

            if (obj.enabled)
            {
                obj.Deactivate();
            }
        }
    }

    // Selected in the scene view: where the pad sits, and how far down it goes.
    void OnDrawGizmosSelected()
    {
        var body = Pad;

        if (!body)
        {
            return;
        }

        var top = Application.isPlaying
            ? (Vector2)transform.TransformPoint(restLocal)
            : (Vector2)body.transform.position;
        var bottom = top - (Vector2)transform.up * pressDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(top, bottom);
        Gizmos.DrawWireSphere(bottom, 0.1f);

        // Where it switches on.
        Gizmos.color = Color.green;
        var activates = top - (Vector2)transform.up * (pressDistance * percentPressedToActivate / 100f);
        Gizmos.DrawLine(activates + Vector2.left * 0.3f, activates + Vector2.right * 0.3f);
    }
}
