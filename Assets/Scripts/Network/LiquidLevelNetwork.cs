using Mirror;
using UnityEngine;

// Puts the level of a switched liquid on the network: how full it is, which is all another machine needs to draw it. What switches it, and
// what's in it, is done on the host, so on a machine that has joined someone else's game it only follows the level it's sent.
//
// Added to the objects with a Switch Liquid by Tools > Network > Make World Networked.
[RequireComponent(typeof(SwitchLiquid))]
public class LiquidLevelNetwork : NetworkBehaviour
{
    [SyncVar] private float level = 1f;

    private SwitchLiquid switchLiquid;

    private SwitchLiquid Switch
    {
        get
        {
            if (!switchLiquid)
            {
                switchLiquid = GetComponent<SwitchLiquid>();
            }

            return switchLiquid;
        }
    }

    void Update()
    {
        if (isServer)
        {
            level = Switch.Level;
        }
        else if (isClient)
        {
            Switch.SetLevelFromNetwork(level);
        }
    }
}
