using System;
using System.Collections.Generic;
using Characters;
using UnityEngine;

// What a WalkSwitch goes by: someone walking into the area, or someone walking out of it.
public enum WalkTrigger
{
    // Switched when someone walks in. With several characters, it's the first to come in, into an empty area.
    Enter,
    // Switched when someone walks out. With several characters, it's the last to leave, which empties the area.
    Exit
}

// What a WalkSwitch does when its trigger happens.
public enum WalkMode
{
    // Enter: on while anyone is in the area, off when the last has left. Exit: on from when someone leaves until someone walks back in.
    // So a door that's open while someone stands in front of it, or that opens behind you and closes if you come back.
    Hold,
    // Each time the trigger happens, it flips: on, then off, then on again.
    Toggle,
    // Switched on the first time the trigger happens, and stays on from then.
    StaysOn,
    // Goes by how long someone is in the area, not by their coming or going (so Activate On doesn't apply): it fills up while anyone is in it, and is on
    // once it's full. Things that go exactly with the switch follow how full it is.
    Timer
}

// A switch that is just an area on the ground (a Box Collider 2D): it switches the things in its list when a character walks into the
// area, or when one walks out of it.
//
// To make one: make a GameObject, give it a Box Collider 2D for the area, and add this, or use GameObject > Walk Switch, which does
// both. The collider is made a trigger for you, so characters walk through it. It needs to be on a layer that collides with the
// characters, which the default layer does. Then give it the switchable objects it works.
//
// Activate On says whether it goes by someone entering or someone exiting. Mode says what it does then. Characters that are put away, or
// are being sent somewhere by a teleporter, don't count, and one that's put away while in the area counts as having left it. Like the
// other switches it runs on the host, in a network game, and the things it switches are the ones that are sent to the other machines.
//
// Initial Delay, Delay Between Objects and Switch Off Order work as they do on a lever or a button (see Docs/Switches.md).
public class WalkSwitch : MonoBehaviour, ILatchedSwitch
{
    [Tooltip("Whether it goes by someone walking into the area (Enter) or out of it (Exit).")]
    public WalkTrigger activateOn = WalkTrigger.Enter;

    [Tooltip("What it does when that happens. Hold: Enter is on while anyone is in the area; Exit is on from when someone leaves until someone walks back in. Toggle: flips each time. Stays On: switches on once and stays on. Timer: goes by how long anyone is in the area, and is on once they've been in it for Seconds To Complete.")]
    public WalkMode mode = WalkMode.Hold;

    [Min(0.05f)]
    [Tooltip("Timer only. How many seconds someone has to be in the area, without leaving, for it to be complete and switch its things on. Things that can be set to a part of their range and go exactly with the switch (a door that doesn't use its own speed) follow how far along it is.")]
    public float secondsToComplete = 3f;

    [Min(0f)]
    [Tooltip("Timer only. How many seconds it takes to go all the way back to the start once the area is empty, if it hadn't finished or isn't kept. Leaving and coming back soon carries on from where it had got to. 0 goes back at once.")]
    public float secondsToReset = 1.5f;

    [Tooltip("Timer only. Once it's complete, it stays complete: the things stay switched on, whoever leaves, and it doesn't go back (a door that stays open, lights that stay on).")]
    public bool staysCompleted;

    [Tooltip("While it's on, it keeps switching things on every frame. Things that switch themselves off, after a time or straight away, are held on for as long as it's on, and only start to go off when it is. Without it, things are switched on once, and are left to switch themselves off. Needed for doors and the like that should close again.")]
    public bool staysActiveWhenOn = true;

    [Tooltip("The things this switches. With a delay set below, they are switched in the order of this list.")]
    public SwitchableObject[] switchableObjects;

    [Header("Signal")]
    [Tooltip("How this switch's signal is put with the signals of the other switches that work the same things. Min/Max: it sends its own signal, from the Minimum Signal when it's off to the Maximum Signal when it's on, and a thing goes by the switch that spoke last. Add: while it's on, it adds the Maximum Signal to what the things get, along with every other switch that adds, so four that are each 25 give 25, 50, 75, 100. Subtract: takes the Maximum Signal off while it's on.")]
    public SignalMode signalMode = SignalMode.MinMax;

    [Range(0, 100)]
    [Tooltip("Min/Max only. How much of its full effect the things get while the switch is off, as a percentage. 0 is the ordinary off. Above 0, the things that can be set to a part of their range (a door, a liquid level, a moving platform, a light, a fan) are held at that part while it's off, instead of at nothing. Add and Subtract add or take off nothing while off.")]
    public int minimumSignal = 0;

    [Range(0, 100)]
    [Tooltip("How much of its full effect the things get while it's on, as a percentage. 100 is the ordinary on. Below 100, the things that can be set to a part of their range go that far and no further. In Add and Subtract this is how much it adds or takes off. Anything that can only be on or off is switched as usual (on once all the Adds reach 100), whatever these are set to.")]
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

    // Who is in the area, and with how many of their colliders, as a character has more than one and each tells when it goes in and out.
    private readonly Dictionary<Character, int> inside = new Dictionary<Character, int>();
    private readonly List<Character> gone = new List<Character>();

    private bool wasOccupied;
    private bool toggledOn;
    private bool latched;
    private bool leftBehind;
    private bool activated;

    // Timer. How far along it is, from 0 to 1, and whether the things that follow it were being given it.
    private const float PositionMargin = 0.0001f;
    private float progress;
    private bool passingAmount;

    // Switches the things one after another, when there's a delay before them or between them.
    private readonly SwitchSequence sequence = new SwitchSequence();
    private Func<SwitchableObject, bool> usable;

    private bool Sequenced
    {
        get { return initialDelay > 0f || delayBetweenObjects > 0f; }
    }

    // Stays On, or a Timer that Stays Completed, once it has gone on: it's on for good, and the things it has switched on are held on, whatever other
    // switches that work them do afterward.
    public bool IsLatched
    {
        get { return latched && isActiveAndEnabled; }
    }

    public float LatchedAmount
    {
        get { return AmountFor(true); }
    }

    // How far along the timer is, from 0 (the start) to 1 (complete). Only changes in Timer mode.
    public float Progress
    {
        get { return progress; }
    }

    // Whether a thing is switched on and off by this. With a Timer, those that go exactly with the switch aren't: they follow how far along it is.
    private bool Usable(SwitchableObject thing)
    {
        return thing.enabled && !(mode == WalkMode.Timer && thing.FollowsSwitchPosition);
    }

    // Whether anyone is in the area at the moment.
    public bool Occupied
    {
        get { return inside.Count > 0; }
    }

    void Reset()
    {
        // An area to walk into, if there isn't anything yet.
        if (!GetComponent<Collider2D>())
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(3f, 2f);
        }

        MakeTriggers();
    }

    void OnValidate()
    {
        MakeTriggers();
    }

    void Awake()
    {
        usable = Usable;
    }

    void Start()
    {
        MakeTriggers();

        if (!GetComponent<Collider2D>())
        {
            Debug.LogWarning($"{name}: the Walk Switch has no collider, so nothing can walk into it. Add a Box Collider 2D to its object.", this);
        }
    }

    void OnEnable()
    {
        SwitchSignal.Register(this, signalMode, switchableObjects);
    }

    void OnDisable()
    {
        SwitchSignal.Unregister(this, switchableObjects);
        inside.Clear();
        sequence.Cancel();
        progress = 0f;
        passingAmount = false;
    }

    // Any collider on the object is only for telling who is in the area, so it's made a trigger: characters walk through it. This is done
    // as the switch is added, when anything changes, and in the Inspector, so it doesn't matter which was added first.
    public void MakeTriggers()
    {
        foreach (var area in GetComponents<Collider2D>())
        {
            if (!area.isTrigger)
            {
                area.isTrigger = true;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(area);
#endif
            }
        }
    }

    // Whether a character counts: they're in the game, and aren't part of the way through being sent somewhere.
    private static bool Counts(Character character)
    {
        return character && character.isActiveAndEnabled && character.InGame && !character.InTransit;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // In someone else's game nothing touches anything: the host works out who is in the area.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        var character = other.GetComponentInParent<Character>();

        if (!Counts(character))
        {
            return;
        }

        inside.TryGetValue(character, out var colliders);
        inside[character] = colliders + 1;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var character = other.GetComponentInParent<Character>();

        if (!character || !inside.TryGetValue(character, out var colliders))
        {
            return;
        }

        if (colliders <= 1)
        {
            inside.Remove(character);
        }
        else
        {
            inside[character] = colliders - 1;
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
            sequence.Tick(switchableObjects, false, usable);
        }

        // Someone put away, or taken out of the game, in the area never leaves it.
        if (inside.Count > 0)
        {
            gone.Clear();

            foreach (var pair in inside)
            {
                if (!Counts(pair.Key))
                {
                    gone.Add(pair.Key);
                }
            }

            foreach (var character in gone)
            {
                inside.Remove(character);
            }
        }

        var occupied = inside.Count > 0;

        // Someone came into an empty area, or the last one left it.
        var entered = occupied && !wasOccupied;
        var exited = !occupied && wasOccupied;
        var happened = activateOn == WalkTrigger.Enter ? entered : exited;

        wasOccupied = occupied;

        bool wantOn;

        switch (mode)
        {
            case WalkMode.Toggle:
                if (happened)
                {
                    toggledOn = !toggledOn;
                }

                wantOn = toggledOn;
                break;

            case WalkMode.StaysOn:
                latched |= happened;
                wantOn = latched;
                break;

            case WalkMode.Timer:
                wantOn = UpdateTimer(occupied);
                break;

            default:
                if (activateOn == WalkTrigger.Enter)
                {
                    wantOn = occupied;
                }
                else
                {
                    // On from the moment someone leaves, until someone walks in again.
                    if (exited)
                    {
                        leftBehind = true;
                    }
                    else if (entered)
                    {
                        leftBehind = false;
                    }

                    wantOn = leftBehind;
                }

                break;
        }

        if (wantOn)
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

        if (mode == WalkMode.Timer)
        {
            PassOnProgress();
        }
        else if (progress != 0f)
        {
            // Changed from a Timer to something else: it isn't part of the way there any more.
            progress = 0f;
            PassOnProgress();
        }

        // Off, with a lowest signal to send: the things are held at it, all the time. Not while they're being switched off one after another, or
        // while a timer is giving the things that follow it how far along it is.
        if (!activated && SwitchSignal.HoldsMinimum(signalMode, minimumSignal) && !sequence.Pending && !(progress > PositionMargin))
        {
            SwitchSignal.HoldMinimum(switchableObjects, minimumSignal, usable, this);
        }
    }

    // Timer: fills while anyone is in the area, and empties when no one is (unless it's complete and is to stay that way). Returns whether it's
    // complete, which is when it's on.
    private bool UpdateTimer(bool occupied)
    {
        if (!latched)
        {
            if (occupied)
            {
                progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / Mathf.Max(0.05f, secondsToComplete));
            }
            else
            {
                progress = secondsToReset <= 0f ? 0f : Mathf.MoveTowards(progress, 0f, Time.deltaTime / secondsToReset);
            }

            if (progress >= 1f && staysCompleted)
            {
                latched = true;
            }
        }

        return progress >= 1f;
    }

    // Gives the things that go exactly with the switch how far along the timer is, and lets them go when it's back at the start.
    private void PassOnProgress()
    {
        var any = progress > PositionMargin;

        if (!any && !passingAmount)
        {
            return;
        }

        foreach (var thing in switchableObjects)
        {
            if (!thing || !thing.enabled || !thing.AcceptsAmount || !thing.FollowsSwitchPosition)
            {
                continue;
            }

            if (any)
            {
                thing.Signal(this, signalMode, true, SwitchSignal.Pressed(signalMode, progress, minimumSignal, maximumSignal));
            }
            else
            {
                thing.Signal(this, signalMode, false, AmountFor(false));
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
                sequence.Begin(switchableObjects, true, initialDelay, delayBetweenObjects, false, usable, AmountFor(true), this, signalMode);
            }

            sequence.Tick(switchableObjects, staysActiveWhenOn, usable);
            return;
        }

        sequence.Cancel();

        foreach (var thing in switchableObjects)
        {
            if (thing && Usable(thing))
            {
                thing.Signal(this, signalMode, true, AmountFor(true));
            }
        }
    }

    private void Deactivate()
    {
        activated = false;

        // One after another, if there's a delay and they're to go off that way.
        if (Sequenced && switchOffOrder != SwitchOffOrder.AllAtOnce)
        {
            sequence.Begin(switchableObjects, false, initialDelay, delayBetweenObjects, switchOffOrder == SwitchOffOrder.ReverseOrder, usable, AmountFor(false), this, signalMode);
            sequence.Tick(switchableObjects, false, usable);
            return;
        }

        sequence.Cancel();

        foreach (var thing in switchableObjects)
        {
            if (thing && Usable(thing))
            {
                thing.Signal(this, signalMode, false, AmountFor(false));
            }
        }
    }

    // In the scene view while it's selected: an arrow from the middle of the area to each of the things it switches.
    void OnDrawGizmosSelected()
    {
        var area = GetComponent<Collider2D>();

        SwitchLinkGizmo.Draw(area ? area.bounds.center : transform.position, switchableObjects, Sequenced);
    }
}
