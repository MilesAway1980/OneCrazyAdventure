using System.Collections.Generic;
using UnityEngine;

// A switch that, once it has switched on, never switches off: a Walk Switch that Stays On, or a Timer that Stays Completed. A thing that one of these has
// switched on is held on by it for good, whatever the other switches that work it then do (see SwitchableObject.Signal).
public interface ILatchedSwitch
{
    // Whether it has switched on for good.
    bool IsLatched { get; }

    // How much of its full effect the things get, from 0 to 1, or below 0 for the ordinary on (all of it).
    float LatchedAmount { get; }
}

public class SwitchableObject : MonoBehaviour
{
    [Tooltip("Object switch action will stay on even if switch is turned off.  Ignores the staySwitchedOn timer.")]
    public bool staySwitchedOn;
    [Tooltip("Time in seconds before switching off.  If set to 0, the timer will be ignored.")]
    public float switchOffTime;

    [Tooltip("When more than one switch works this, every one of them has to be on before it does anything: with four switches, all four. Let one go off and it " +
             "goes off too. Switches that subtract don't count, and a switch is on from the moment it's sending anything (a button pressed any of the way, " +
             "a lever that has left its start). With one switch, it makes no difference.")]
    public bool needsAllSwitches;

    private float switchOnTime;
    protected bool activated;
    private bool timeActivated;

    protected virtual void Start()
    {
        
    }

    protected virtual void Update()
    {
        if (staySwitchedOn || Latched)
        {
            return;
        }

        if (switchOffTime == 0)
        {
            Deactivate();
        }
        else
        {
            if (Time.time - switchOnTime >= switchOffTime && activated)
            {
                timeActivated = false;
                Deactivate();
            }
            else
            {
                timeActivated = true;
            }
        }
    }
    
    public virtual void Activate()
    {
        activated = true;
        switchOnTime = Time.time;
    }

    // Whether this can be switched by an amount, from 0 to 1, as well as on and off. A button set to pass on how far it's pressed
    // gives the amount to the ones that can, and switches the rest on and off as usual.
    public virtual bool AcceptsAmount
    {
        get { return false; }
    }

    // Whether this goes exactly with how far the switch is, one to one, rather than being switched on and off and moving in its own time. A lever,
    // even one that swings by itself, gives such a thing how far along its swing it is, from the moment it's pressed, and not just at the end.
    public virtual bool FollowsSwitchPosition
    {
        get { return false; }
    }

    // Switches this on by an amount from 0 to 1, called again each frame while it's held there. Only for those that accept one.
    public virtual void SetAmount(float amount)
    {
        if (amount > 0f)
        {
            Activate();
        }
        else
        {
            Deactivate();
        }
    }

    public virtual void Deactivate()
    {
        if (!staySwitchedOn && !timeActivated && activated)
        {
            activated = false;
        }
    }

    // ----- Signals from the switches -----
    //
    // Every switch that works this tells it through Signal, which is how it can keep track of them all. A switch that is Min/Max sends its own
    // amount, and this goes by the last that did, as it always has. A switch that is Add or Subtract is remembered, with how much it gives, and
    // this goes by all of them added up (less the ones taken off): four that give 25 each take it to 25, 50, 75, then 100. The total is an
    // amount, from 0 to 1, and a thing that takes an amount (a door's height, how full a liquid is, how far along a path) goes to it. One that
    // can only be on or off is on once the total gets to the whole (100), so it can need all of its switches.

    // ----- Needs All Switches -----
    //
    // The switches that work this tell it so as they come on (AddSource), with how they combine, and what each last said it is kept as it comes (Signal).
    // With Needs All Switches, this is only switched by what they say when every one of them (but the ones that subtract) is on.
    private readonly Dictionary<Object, SignalMode> sources = new Dictionary<Object, SignalMode>();
    private readonly Dictionary<Object, bool> sourceOn = new Dictionary<Object, bool>();
    private readonly List<Object> forgotten = new List<Object>();

    // A switch that works this is there, and says how it combines with the others.
    public void AddSource(Object source, SignalMode mode)
    {
        if (source)
        {
            sources[source] = mode;
        }
    }

    // A switch that worked this isn't any more (it was switched off, or taken away). What's left decides.
    public void RemoveSource(Object source)
    {
        if (!source)
        {
            return;
        }

        sources.Remove(source);
        sourceOn.Remove(source);

        if (needsAllSwitches && this && isActiveAndEnabled)
        {
            Resolve();
        }
    }

    // Whether every switch that has to be on is. With none to go by, nothing is held back.
    private bool AllSwitchesOn()
    {
        forgotten.Clear();

        foreach (var pair in sources)
        {
            if (!pair.Key)
            {
                forgotten.Add(pair.Key);
                continue;
            }

            if (pair.Value == SignalMode.Subtract)
            {
                continue;
            }

            if (!sourceOn.TryGetValue(pair.Key, out var on) || !on)
            {
                return false;
            }
        }

        foreach (var key in forgotten)
        {
            sources.Remove(key);
        }

        return true;
    }

    // The switches that have switched this on for good.
    private readonly HashSet<Object> latches = new HashSet<Object>();
    private readonly List<Object> unlatched = new List<Object>();

    // Whether a switch has switched this on for good, and so it can't be switched off.
    protected bool Latched
    {
        get { return latches.Count > 0; }
    }

    // What each Add or Subtract switch is giving, as an amount that's negative if it takes away.
    private readonly Dictionary<Object, float> shares = new Dictionary<Object, float>();
    private readonly List<Object> gone = new List<Object>();

    // What the last Min/Max switch said, which is what this is at before the others are counted.
    private bool hasMinMax;
    private bool minMaxOn;
    private float minMaxAmount = -1f;

    // How close to the whole the total has to be for something that can only be on or off to be on, for the float sums.
    private const float WholeEnough = 0.999f;

    // A switch tells this what it is sending: on or off, and, if it sends an amount (from 0 to 1), how much. Below 0 is no amount, which is
    // the ordinary on and off. In Add and Subtract the amount is always sent, and 0 takes the switch's share away.
    public void Signal(Object source, SignalMode mode, bool on, float amount)
    {
        // A switch that has switched on for good holds this on from now: it's remembered, and nothing else that works this changes it again.
        if (on && source is ILatchedSwitch latchedSwitch && latchedSwitch.IsLatched)
        {
            latches.Add(source);
        }

        // What it last said: on if it's sending something. (An amount of 0 is nothing.)
        if (source)
        {
            sourceOn[source] = on && !Mathf.Approximately(amount, 0f);
        }

        if (mode == SignalMode.MinMax)
        {
            hasMinMax = true;
            minMaxOn = on;
            minMaxAmount = amount >= 0f && AcceptsAmount ? amount : -1f;
        }
        else
        {
            if (!source)
            {
                return;
            }

            var share = Mathf.Max(0f, amount) * (mode == SignalMode.Subtract ? -1f : 1f);

            if (Mathf.Approximately(share, 0f))
            {
                // Giving nothing, and never was: nothing has changed.
                if (!shares.Remove(source))
                {
                    return;
                }
            }
            else
            {
                shares[source] = share;
            }
        }

        Resolve();
    }

    // Works out what this is switched to by everything that works it, and switches it to that.
    private void Resolve()
    {
        // Every switch has to be on, and one isn't: nothing is done, and what was done is undone.
        if (needsAllSwitches && !AllSwitchesOn())
        {
            Deactivate();
            return;
        }

        // On for good: all that's left to do is what the switches that did it say, and the rest are ignored, however much they send or when.
        if (latches.Count > 0)
        {
            var latchedAmount = float.NegativeInfinity;
            unlatched.Clear();

            foreach (var source in latches)
            {
                if (source is ILatchedSwitch latchedSwitch && source && latchedSwitch.IsLatched)
                {
                    var share = latchedSwitch.LatchedAmount;
                    latchedAmount = Mathf.Max(latchedAmount, share >= 0f ? share : 1f);
                }
                else
                {
                    unlatched.Add(source);
                }
            }

            foreach (var source in unlatched)
            {
                latches.Remove(source);
            }

            if (latchedAmount > float.NegativeInfinity)
            {
                if (AcceptsAmount && latchedAmount < 1f)
                {
                    SetAmount(latchedAmount);
                }
                else
                {
                    Activate();
                }

                return;
            }
        }

        // Switches that have gone, such as one that was destroyed, don't count.
        if (shares.Count > 0)
        {
            gone.Clear();

            foreach (var share in shares)
            {
                if (!share.Key)
                {
                    gone.Add(share.Key);
                }
            }

            foreach (var key in gone)
            {
                shares.Remove(key);
            }
        }

        // Only Min/Max switches: as it has always been, this does what the last of them said.
        if (shares.Count == 0)
        {
            if (!hasMinMax)
            {
                Deactivate();
            }
            else if (minMaxAmount >= 0f)
            {
                SetAmount(minMaxAmount);
            }
            else if (minMaxOn)
            {
                Activate();
            }
            else
            {
                Deactivate();
            }

            return;
        }

        var total = hasMinMax ? (minMaxAmount >= 0f ? minMaxAmount : (minMaxOn ? 1f : 0f)) : 0f;

        foreach (var share in shares.Values)
        {
            total += share;
        }

        total = Mathf.Clamp01(total);

        if (AcceptsAmount)
        {
            if (total > 0f)
            {
                SetAmount(total);
            }
            else
            {
                Deactivate();
            }
        }
        else if (total >= WholeEnough)
        {
            Activate();
        }
        else
        {
            Deactivate();
        }
    }

    // Where the arrows from the levers and buttons that switch this point to in the scene view: this object, unless what it moves or
    // changes is somewhere else.
    public virtual Vector3 GizmoPoint
    {
        get { return transform.position; }
    }
}
