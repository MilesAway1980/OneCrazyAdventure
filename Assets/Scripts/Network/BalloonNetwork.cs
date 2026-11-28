using System.Collections.Generic;
using Characters;
using Mirror;
using UnityEngine;

// Sends where the points of the Balloon's string are. The string is a rope worked out by script on the host (BalloonString). On a machine that has joined
// someone else's game the Balloon is only a picture too, so it works nothing out; the points of its rope are moved to where the host has them, and the
// line along them is drawn there. The points are sent from here, as a list in order from the top of the string down.
//
// Added to the Balloon by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Balloon))]
public class BalloonNetwork : NetworkBehaviour
{
    // For each point of the rope after the top of it: where it is (x, y).
    [SyncVar] private Vector3[] pieces = new Vector3[0];

    // How often, in seconds, the points are looked at, and how far one has to move to be worth sending.
    private const float SendSeconds = 0.04f;
    private const float MovedEnough = 0.002f;

    // How quickly a point catches up with where it was said to be, so it doesn't jump between what is sent.
    private const float CatchUpSpeed = 20f;

    private BalloonString rope;
    private Vector3[] lastSent = new Vector3[0];
    private float nextSend;

    private bool shownOnly;
    private bool snapped;

    // His string is made on every machine, the same, as soon as there's a Balloon, so that the points are there to be moved.
    void Awake()
    {
        rope = GetComponent<Balloon>().EnsureString();
    }

    public override void OnStartClient()
    {
        if (isServer)
        {
            return;
        }

        shownOnly = true;
        snapped = false;
    }

    public override void OnStopClient()
    {
        shownOnly = false;
    }

    // After the physics for the frame, so what's sent is where the points have got to.
    void LateUpdate()
    {
        if (isServer)
        {
            Send();
        }
        else if (isClient)
        {
            Show();
        }
    }

    private void Send()
    {
        if (Time.time < nextSend || !rope || rope.PointCount < 2)
        {
            return;
        }

        nextSend = Time.time + SendSeconds;

        var now = new Vector3[rope.PointCount - 1];
        rope.CopyPoints(now);

        var changed = lastSent.Length != now.Length;

        for (var i = 0; i < now.Length && !changed; i++)
        {
            if (Vector2.Distance(now[i], lastSent[i]) > MovedEnough)
            {
                changed = true;
            }
        }

        // Only when something has moved: a string hanging still costs nothing.
        if (changed)
        {
            lastSent = now;
            pieces = now;
        }
    }

    private void Show()
    {
        if (!rope || pieces == null || pieces.Length == 0 || pieces.Length != rope.PointCount - 1)
        {
            return;
        }

        var catchUp = snapped ? 1f - Mathf.Exp(-Time.deltaTime * CatchUpSpeed) : 1f;
        snapped = true;

        rope.ShowPoints(pieces, catchUp);
    }
}
