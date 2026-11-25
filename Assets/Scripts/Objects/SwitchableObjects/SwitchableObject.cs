using System.Collections.Generic;
using UnityEngine;

public class SwitchableObject : MonoBehaviour
{
    [Tooltip("Object switch action will stay on even if switch is turned off.  Ignores the staySwitchedOn timer.")]
    public bool staySwitchedOn;
    [Tooltip("Time in seconds before switching off.  If set to 0, the timer will be ignored.")]
    public float switchOffTime;

    private float switchOnTime;
    protected bool activated;
    private bool timeActivated;

    protected virtual void Start()
    {
        
    }

    protected virtual void Update()
    {
        if (staySwitchedOn)
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
