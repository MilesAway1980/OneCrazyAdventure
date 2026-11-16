using Mirror;
using UnityEngine;

// Puts a lever on the network: whether it has been thrown, which is all another machine needs to show it. What the lever
// switches is done on the host, so on a machine that has joined someone else's game it only moves, to match.
//
// Added to the levers by Tools > Network > Make World Networked.
[RequireComponent(typeof(LeverSwitch))]
public class LeverNetwork : NetworkBehaviour
{
    [SyncVar] private bool thrown;

    private LeverSwitch lever;

    private LeverSwitch Lever
    {
        get
        {
            if (!lever)
            {
                lever = GetComponent<LeverSwitch>();
            }

            return lever;
        }
    }

    void Update()
    {
        if (isServer)
        {
            thrown = Lever.IsThrown;
        }
        else if (isClient)
        {
            Lever.SetThrownFromNetwork(thrown);
        }
    }
}
