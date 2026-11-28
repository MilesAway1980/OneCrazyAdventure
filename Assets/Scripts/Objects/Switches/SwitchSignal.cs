using System;
using System.Collections.Generic;
using UnityEngine;

// How a switch's signal is combined with the signals of the other switches that work the same thing.
public enum SignalMode
{
    // Sends its own signal, from the Minimum Signal when it's off to the Maximum Signal when it's on, and the thing goes by whichever switch
    // spoke last. With the defaults (0 and 100) this is the ordinary on and off.
    MinMax,
    // Adds the Maximum Signal to what the thing gets while it's on, and nothing while it's off. The things that work the thing add up: four
    // switches that are each 25 give it 25, 50, 75 and 100 as they come on.
    Add,
    // Takes the Maximum Signal off what the thing gets while it's on, and nothing while it's off.
    Subtract
}

// What the switches share about signals: what to send in each mode, and how to hold a lowest signal. Used by LeverSwitch, ButtonSwitch and
// WalkSwitch. (What a thing does with the signals it's sent is in SwitchableObject.Signal.)
public static class SwitchSignal
{
    // What an on or off switch sends, as an amount from 0 to 1, or below 0 for no amount, which is the ordinary on and off, so that things
    // such as a platform that go about their path when they're on do so. In Add and Subtract the switch sends its Maximum Signal when on, and
    // nothing when off.
    public static float Amount(SignalMode mode, bool on, int minimum, int maximum)
    {
        if (mode != SignalMode.MinMax)
        {
            return on ? Mathf.Clamp01(maximum / 100f) : 0f;
        }

        if (on)
        {
            return maximum < 100 ? Mathf.Clamp01(maximum / 100f) : -1f;
        }

        return minimum > 0 ? Mathf.Clamp01(minimum / 100f) : -1f;
    }

    // What a switch that follows how far a pad is pressed sends, from 0 (not pressed) to 1 (all the way down). In Min/Max it goes from the
    // Minimum Signal to the Maximum, which is just how far down the pad is when they are 0 and 100. In Add and Subtract it's that fraction of
    // the Maximum Signal.
    public static float Pressed(SignalMode mode, float depth, int minimum, int maximum)
    {
        if (mode != SignalMode.MinMax)
        {
            return Mathf.Clamp01(depth * maximum / 100f);
        }

        return Mathf.Clamp01(Mathf.Lerp(minimum, maximum, depth) / 100f);
    }

    // Whether a switch has to keep sending its lowest signal while it's off: only in Min/Max, and only if that isn't 0. (In Add and Subtract, off
    // adds nothing, and the things remember what the other switches added.)
    public static bool HoldsMinimum(SignalMode mode, int minimum)
    {
        return mode == SignalMode.MinMax && minimum > 0;
    }

    // Tells the things a switch works that it does, so that one that needs all its switches knows which they are. Done as the switch is switched on, and
    // undone as it's switched off. (A switch that is off is no longer one it waits for.)
    public static void Register(UnityEngine.Object source, SignalMode mode, IList<SwitchableObject> things)
    {
        if (things == null)
        {
            return;
        }

        for (var i = 0; i < things.Count; i++)
        {
            if (things[i])
            {
                things[i].AddSource(source, mode);
            }
        }
    }

    public static void Unregister(UnityEngine.Object source, IList<SwitchableObject> things)
    {
        if (things == null)
        {
            return;
        }

        for (var i = 0; i < things.Count; i++)
        {
            if (things[i])
            {
                things[i].RemoveSource(source);
            }
        }
    }

    // Tells the things that can take an amount the lowest signal. Those that can only be on or off are left as they are.
    public static void HoldMinimum(IList<SwitchableObject> things, int minimum, Func<SwitchableObject, bool> counts, UnityEngine.Object source)
    {
        var amount = Mathf.Clamp01(minimum / 100f);

        for (var i = 0; i < things.Count; i++)
        {
            var thing = things[i];

            if (thing && counts(thing) && thing.AcceptsAmount)
            {
                thing.Signal(source, SignalMode.MinMax, false, amount);
            }
        }
    }
}
