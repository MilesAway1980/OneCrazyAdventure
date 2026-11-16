using Mirror;
using UnityEngine;

// Puts a hub on the network: whether it has been reached and whether it's the checkpoint, so every machine shows the flag and knows
// where the party goes back to. Who reaches a hub is decided on the host, and the message about it is sent to everyone.
//
// Added to the hubs by Tools > Network > Make World Networked.
[RequireComponent(typeof(CharacterHub))]
public class HubNetwork : NetworkBehaviour
{
    [SyncVar] private bool reached;
    [SyncVar] private bool current;

    private CharacterHub hub;

    private CharacterHub TheHub
    {
        get
        {
            if (!hub)
            {
                hub = GetComponent<CharacterHub>();
            }

            return hub;
        }
    }

    void Update()
    {
        if (isServer)
        {
            reached = TheHub.Reached;
            current = TheHub.IsCurrent;
        }
        else if (isClient)
        {
            TheHub.ShowReached(reached, current);
        }
    }
}
