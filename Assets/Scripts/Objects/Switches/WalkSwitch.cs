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
    StaysOn
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
public class WalkSwitch : MonoBehaviour
{
    [Tooltip("Whether it goes by someone walking into the area (Enter) or out of it (Exit).")]
    public WalkTrigger activateOn = WalkTrigger.Enter;

    [Tooltip("What it does when that happens. Hold: Enter is on while anyone is in the area; Exit is on from when someone leaves until someone walks back in. Toggle: flips each time. Stays On: switches on once and stays on.")]
    public WalkMode mode = WalkMode.Hold;

    [Tooltip("While it's on, it keeps switching things on every frame. Things that switch themselves off, after a time or straight away, are held on for as long as it's on, and only start to go off when it is. Without it, things are switched on once, and are left to switch themselves off. Needed for doors and the like that should close again.")]
    public bool staysActiveWhenOn = true;

    [Tooltip("The things this switches. With a delay set below, they are switched in the order of this list.")]
    public SwitchableObject[] switchableObjects;

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

    // Switches the things one after another, when there's a delay before them or between them.
    private readonly SwitchSequence sequence = new SwitchSequence();
    private static readonly Func<SwitchableObject, bool> usable = thing => thing.enabled;

    private bool Sequenced
    {
        get { return initialDelay > 0f || delayBetweenObjects > 0f; }
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

    void Start()
    {
        MakeTriggers();

        if (!GetComponent<Collider2D>())
        {
            Debug.LogWarning($"{name}: the Walk Switch has no collider, so nothing can walk into it. Add a Box Collider 2D to its object.", this);
        }
    }

    void OnDisable()
    {
        inside.Clear();
        sequence.Cancel();
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
                sequence.Begin(switchableObjects, true, initialDelay, delayBetweenObjects, false, usable);
            }

            sequence.Tick(switchableObjects, staysActiveWhenOn, usable);
            return;
        }

        sequence.Cancel();

        foreach (var thing in switchableObjects)
        {
            if (thing && thing.enabled)
            {
                thing.Activate();
            }
        }
    }

    private void Deactivate()
    {
        activated = false;

        // One after another, if there's a delay and they're to go off that way.
        if (Sequenced && switchOffOrder != SwitchOffOrder.AllAtOnce)
        {
            sequence.Begin(switchableObjects, false, initialDelay, delayBetweenObjects, switchOffOrder == SwitchOffOrder.ReverseOrder, usable);
            sequence.Tick(switchableObjects, false, usable);
            return;
        }

        sequence.Cancel();

        foreach (var thing in switchableObjects)
        {
            if (thing && thing.enabled)
            {
                thing.Deactivate();
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
