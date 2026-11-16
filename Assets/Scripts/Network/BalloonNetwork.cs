using System.Collections.Generic;
using Characters;
using Mirror;
using UnityEngine;

// Sends where the pieces of the Balloon's string are. The string is a chain of small physics bodies hanging from him, and it swings with
// him and anything that bumps it. On the host that is all ordinary physics. On a machine that has joined someone else's game the Balloon is
// only a picture (his joints are switched off with the rest of him), so the string would simply fall away; instead its pieces are made
// into pictures too and moved to where the host has them. The pieces can't have a Network Identity each, because they are inside the
// Balloon, so they are sent from here, as a list in the order they are in the Balloon.
//
// Added to the Balloon by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Balloon))]
public class BalloonNetwork : NetworkBehaviour
{
    // For each piece: where it is (x, y) and how it's turned (z).
    [SyncVar] private Vector3[] pieces = new Vector3[0];

    // How often, in seconds, the pieces are looked at, and how far one has to move or turn to be worth sending.
    private const float SendSeconds = 0.04f;
    private const float MovedEnough = 0.002f;
    private const float TurnedEnough = 0.2f;

    // How quickly a piece catches up with where it was said to be, so it doesn't jump between what is sent.
    private const float CatchUpSpeed = 20f;

    private List<Rigidbody2D> segments;
    private Vector3[] lastSent = new Vector3[0];
    private float nextSend;

    // What the bodies were on this machine, to put back if it goes back to playing by itself.
    private readonly List<RigidbodyType2D> bodyTypes = new List<RigidbodyType2D>();
    private bool shownOnly;
    private bool snapped;

    // The bodies in the string: every body inside the Balloon except his own.
    private List<Rigidbody2D> Segments
    {
        get
        {
            if (segments == null)
            {
                segments = new List<Rigidbody2D>();
                var own = GetComponent<Rigidbody2D>();

                foreach (var body in GetComponentsInChildren<Rigidbody2D>(true))
                {
                    if (body != own)
                    {
                        segments.Add(body);
                    }
                }
            }

            return segments;
        }
    }

    public override void OnStartClient()
    {
        if (isServer)
        {
            return;
        }

        shownOnly = true;
        snapped = false;
        bodyTypes.Clear();

        // Nothing here works them out: they go where the host says. Switched off they'd be left to fall.
        foreach (var body in Segments)
        {
            bodyTypes.Add(body.bodyType);
            body.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    public override void OnStopClient()
    {
        if (!shownOnly)
        {
            return;
        }

        shownOnly = false;

        for (var i = 0; i < Segments.Count && i < bodyTypes.Count; i++)
        {
            if (Segments[i])
            {
                Segments[i].bodyType = bodyTypes[i];
            }
        }

        bodyTypes.Clear();
    }

    // After the physics for the frame, so what's sent is where they've got to.
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
        if (Time.time < nextSend || Segments.Count == 0)
        {
            return;
        }

        nextSend = Time.time + SendSeconds;

        var now = new Vector3[Segments.Count];
        var changed = lastSent.Length != now.Length;

        for (var i = 0; i < now.Length; i++)
        {
            var piece = Segments[i].transform;
            now[i] = new Vector3(piece.position.x, piece.position.y, piece.eulerAngles.z);

            if (!changed && (Vector2.Distance(now[i], lastSent[i]) > MovedEnough ||
                             Mathf.Abs(Mathf.DeltaAngle(now[i].z, lastSent[i].z)) > TurnedEnough))
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
        if (pieces == null || pieces.Length == 0 || pieces.Length != Segments.Count)
        {
            return;
        }

        var catchUp = snapped ? 1f - Mathf.Exp(-Time.deltaTime * CatchUpSpeed) : 1f;
        snapped = true;

        for (var i = 0; i < pieces.Length; i++)
        {
            var piece = Segments[i].transform;
            var target = new Vector3(pieces[i].x, pieces[i].y, piece.position.z);

            piece.position = Vector3.Lerp(piece.position, target, catchUp);
            piece.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(piece.eulerAngles.z, pieces[i].z, catchUp));
        }
    }
}
