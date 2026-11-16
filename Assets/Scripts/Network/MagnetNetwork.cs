using Characters;
using Mirror;
using UnityEngine;

// Sends whether the Magnet is using his field, so that other machines show it. What it pulls is the host's.
//
// Added to the Magnet by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Magnet))]
public class MagnetNetwork : NetworkBehaviour
{
    [SyncVar] private bool fieldOn;

    private Magnet magnet;

    private Magnet TheMagnet
    {
        get
        {
            if (!magnet)
            {
                magnet = GetComponent<Magnet>();
            }

            return magnet;
        }
    }

    void Update()
    {
        if (isServer)
        {
            fieldOn = TheMagnet.IsPerformingAction();
        }
        else if (isClient)
        {
            TheMagnet.ShowField(fieldOn && TheMagnet.InGame);
        }
    }
}
