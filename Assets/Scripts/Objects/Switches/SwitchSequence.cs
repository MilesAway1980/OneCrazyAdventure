using System;
using System.Collections.Generic;
using UnityEngine;

// What order a switch takes when it switches its things off, if it spaces them out.
public enum SwitchOffOrder
{
    // All at the same moment, whatever the delay between them when switching on.
    AllAtOnce,
    // One after another, in the same order as they were switched on, with the same delay.
    SameOrder,
    // One after another, the last one first, so a row of lights that came on from the left goes out from the right.
    ReverseOrder
}

// Switches a list of switchable objects on or off one after another, with a delay between each, for a lever or a button that works
// several things: a row of sconces that light one by one instead of all together. It's used by LeverSwitch and ButtonSwitch.
//
// The objects go in the order of the list. The first is switched after the initial delay (none, if it's 0), the second one delay after that,
// the third two delays after, and so on, so the first can come on quickly and the rest more slowly. Any in
// the list that are missing, switched off, or left out by the switch (a button's things that follow how far it's pressed) don't take a turn.
// Switching the other way part way through stops the ones that haven't had their turn, and starts again the other way.
//
// It runs where the switch does: on the host, in a network game.
public class SwitchSequence
{
    private bool running;
    private bool pending;
    private bool turningOn;
    private float started;
    private float[] dueAfter = new float[0];
    private bool[] done = new bool[0];

    // Whether some of the objects are still waiting for their turn.
    public bool Pending
    {
        get { return pending; }
    }

    // Starts switching the objects on or off, one by one: the first after firstDelay seconds, then each one delay seconds after the one
    // before. The ones that aren't included are skipped, and take no time.
    public void Begin(IList<SwitchableObject> objects, bool on, float firstDelay, float delay, bool reversed, Func<SwitchableObject, bool> included)
    {
        var count = objects.Count;

        if (dueAfter.Length != count)
        {
            dueAfter = new float[count];
            done = new bool[count];
        }

        started = Time.time;
        turningOn = on;
        running = true;
        pending = false;

        // Each one's turn, counted along the ones that are going to take it, from the front, or from the back if reversed.
        var slot = 0;

        for (var n = 0; n < count; n++)
        {
            var i = reversed ? count - 1 - n : n;
            var object1 = objects[i];

            if (!object1 || !included(object1))
            {
                done[i] = true;
                continue;
            }

            dueAfter[i] = firstDelay + slot * delay;
            done[i] = false;
            slot++;
            pending = true;
        }
    }

    // Stops any that haven't had their turn yet.
    public void Cancel()
    {
        running = false;
        pending = false;
    }

    // Gives each object its turn if it has come. Call it every frame while it's Pending. With keepApplying, the ones that have had their
    // turn are told again, every frame, as a switch that stays active does while it's on.
    public void Tick(IList<SwitchableObject> objects, bool keepApplying, Func<SwitchableObject, bool> included)
    {
        if (!running || objects.Count != done.Length)
        {
            pending = false;
            return;
        }

        var waiting = false;

        for (var i = 0; i < objects.Count; i++)
        {
            var thing = objects[i];

            if (!thing || !included(thing))
            {
                continue;
            }

            if (done[i])
            {
                if (keepApplying && turningOn)
                {
                    Apply(thing);
                }

                continue;
            }

            if (Time.time - started >= dueAfter[i])
            {
                Apply(thing);
                done[i] = true;
            }
            else
            {
                waiting = true;
            }
        }

        pending = waiting;
    }

    private void Apply(SwitchableObject thing)
    {
        if (turningOn)
        {
            thing.Activate();
        }
        else
        {
            thing.Deactivate();
        }
    }
}
