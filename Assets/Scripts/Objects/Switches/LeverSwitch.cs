using Characters;
using UnityEngine;

public class LeverSwitch : InteractableObject
{
    [Range(0, 180f)]
    public float degreesOfMovement;
    [Tooltip("Switch goes from left to right.  Will go from right to left if false.")]
    public bool leftToRight;
    [Tooltip("The game object the lever pivots on when activated")]
    public GameObject pivotObject;
    [Tooltip("How quickly the lever moves from off to on.")]
    public float moveSpeed;
    [Tooltip("Switch can only be used once, and stays used.")]
    public bool oneTime;
    [Tooltip("When the switch is on, it will constantly reapply the switch action.")]
    public bool staysActiveWhenOn;

    public SwitchableObject[] switchableObjects;

    private float angle;
    private bool interactedWith;
    private bool activated;
    private float leftAngle;
    private float rightAngle;

    private BoxCollider2D triggerCollider;
    

    void Start()
    {
        leftAngle = 360 + degreesOfMovement * 0.5f;
        rightAngle = 360 - degreesOfMovement * 0.5f;
        
        angle = leftToRight
                    ? leftAngle
                    : rightAngle;
        
        triggerCollider = GetComponent<BoxCollider2D>();
    }

    
    void Update()
    {
        if (interactedWith)
        {
            if (leftToRight)
            {
                angle -= moveSpeed * Time.deltaTime;
                if (angle < rightAngle)
                {
                    angle = rightAngle;
                    if (activated == false || staysActiveWhenOn)
                    {
                        Activate();
                    }
                }
            }
            else
            {
                angle += moveSpeed * Time.deltaTime;
                if (angle > leftAngle)
                {
                    angle = leftAngle;
                    if (activated == false || staysActiveWhenOn)
                    {
                        Activate();
                    }
                }
            }
        }
        else
        {
            if (leftToRight)
            {
                angle += moveSpeed * Time.deltaTime;
                if (angle > leftAngle)
                {
                    angle = leftAngle;
                    if (activated)
                    {
                        Deactivate();
                    }
                }
            }
            else
            {
                angle -= moveSpeed * Time.deltaTime;
                if (angle < rightAngle)
                {
                    angle = rightAngle;
                    if (activated)
                    {
                        Deactivate();
                    }
                }
            }
        }
        
        pivotObject.transform.eulerAngles = new Vector3(0, 0, angle);
    }

    public override bool Interact(Character interacter)
    {
        if (!triggerCollider)
        {
            Debug.LogError($"{gameObject.name} has no Box Collider 2D to say how close a character has to be. Add one (set to Trigger) to the lever.", this);
            return false;
        }

        if (!triggerCollider.bounds.Contains(interacter.transform.position))
        {
            print($"{this.gameObject} is out of range");
            //Character is not within the range to interact
            return false;
        }
        
        print($"Interacting with: {this.gameObject} ");

        if (oneTime)
        {
            interactedWith = true;
        }
        else
        {
            interactedWith = !interactedWith;
        }

        return true;
    }

    void Activate()
    {
        activated = true;
        foreach (var switchableObject in switchableObjects)
        {
            if (!switchableObject)
            {
                continue;
            }

            if (switchableObject.enabled)
            {
                switchableObject.Activate();
            }
        }
    }

    void Deactivate()
    {
        activated = false;
        foreach (var switchableObject in switchableObjects)
        {
            if (!switchableObject)
            {
                continue;
            }

            if (switchableObject.enabled)
            {
                switchableObject.Deactivate();
            }
        }
    }
}
