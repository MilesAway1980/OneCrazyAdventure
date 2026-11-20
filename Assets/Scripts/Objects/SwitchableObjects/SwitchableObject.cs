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

    // Where the arrows from the levers and buttons that switch this point to in the scene view: this object, unless what it moves or
    // changes is somewhere else.
    public virtual Vector3 GizmoPoint
    {
        get { return transform.position; }
    }
}
