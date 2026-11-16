using System.Collections.Generic;
using Mirror;
using UnityEngine;

// Sends where the links of a rope bridge are. The builder makes them one at a time as it's switched on, as physics bodies joined in a
// chain, and all that swinging and the characters crossing it is worked out on the host. A machine that has joined someone else's game
// makes a picture for each link, as the host makes it, and moves them to where the host says. The links are made while playing, so they
// can't each have a Network Identity: they are sent from here, as a list in the order they were built.
//
// Added to a rope bridge builder by Tools > Network > Make World Networked.
[RequireComponent(typeof(SwitchRopeBridgeBuilder))]
public class BridgeNetwork : NetworkBehaviour
{
    // For each link: where it is (x, y) and how it's turned (z).
    [SyncVar] private Vector3[] links = new Vector3[0];

    // How often, in seconds, the links are looked at, and how far one has to move or turn to be worth sending.
    private const float SendSeconds = 0.05f;
    private const float MovedEnough = 0.002f;
    private const float TurnedEnough = 0.2f;

    private SwitchRopeBridgeBuilder builder;
    private readonly List<Vector3> now = new List<Vector3>();
    private Vector3[] lastSent = new Vector3[0];
    private float nextSend;
    private bool showing;

    private SwitchRopeBridgeBuilder Builder
    {
        get
        {
            if (!builder)
            {
                builder = GetComponent<SwitchRopeBridgeBuilder>();
            }

            return builder;
        }
    }

    public override void OnStartClient()
    {
        showing = !isServer;
    }

    public override void OnStopClient()
    {
        // Back to playing alone: whatever was drawn goes, and the bridge is built here if it's switched on.
        if (showing)
        {
            showing = false;
            Builder.ClearShownLinks();
        }
    }

    // After the physics for the frame, so what's sent is where the links have got to.
    void LateUpdate()
    {
        if (isServer)
        {
            Send();
        }
        else if (showing)
        {
            Builder.ShowLinks(links);
        }
    }

    private void Send()
    {
        if (Time.time < nextSend)
        {
            return;
        }

        nextSend = Time.time + SendSeconds;

        Builder.GetLinksForNetwork(now);

        var changed = lastSent.Length != now.Count;

        for (var i = 0; !changed && i < now.Count; i++)
        {
            changed = Vector2.Distance(now[i], lastSent[i]) > MovedEnough ||
                      Mathf.Abs(Mathf.DeltaAngle(now[i].z, lastSent[i].z)) > TurnedEnough;
        }

        // Only when something has changed: a bridge that has come to rest costs nothing.
        if (changed)
        {
            lastSent = now.ToArray();
            links = lastSent;
        }
    }
}
