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

    public virtual void Deactivate()
    {
        if (!staySwitchedOn && !timeActivated && activated)
        {
            activated = false;
        }
    }
}
