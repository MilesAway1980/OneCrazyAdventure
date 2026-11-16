using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ButtonSwitch : MonoBehaviour
{
    public SwitchableObject[] switchableObject;

    [Range(0, 1)]
    public float percentagePressedToActivate;
    public bool stayPressed;
    public bool staysActiveWhenOn;

    public SpringJoint2D joint;
    
    private float pressedHeight;
    private bool activated;
    private GameObject pad;
    private bool passedActivatedHeight;
    
    void Start()
    {
        pad = joint.connectedBody.gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        // In someone else's game the host works out what is pressed and what it switches. Where the pad is comes from the network.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        var pressed = Vector2.Distance(
            (Vector2)joint.attachedRigidbody.transform.position,
            (Vector2)joint.connectedBody.transform.position
        );

        var percentage = pressed / joint.distance;

        if (percentage <= percentagePressedToActivate)
        {
            passedActivatedHeight = true;
            if (!activated || (staysActiveWhenOn && passedActivatedHeight))
            {
                Activate();
            }
            
            if (stayPressed)
            {
                pressedHeight = pad.transform.localPosition.y;
            }
        }
        else
        {
            if (!stayPressed)
            {
                if (activated)
                {
                    Deactivate();
                }
            }
        }

        if (stayPressed && passedActivatedHeight)
        {
            pad.transform.localPosition = new Vector3(
                pad.transform.localPosition.x,
                pressedHeight,
                pad.transform.localPosition.z
            );
        }
    }

    private void Activate()
    {
        activated = true;
        foreach (var obj in switchableObject)
        {
            if (!obj)
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
            if (!obj)
            {
                continue;
            }

            if (obj.enabled)
            {
                obj.Deactivate();
            }
        }
    }
}
