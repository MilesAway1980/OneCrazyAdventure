using Characters;
using Mirror;
using UnityEngine;

// Sends which way the Drill's drill points, so that other machines turn him upside down, or on his side, as he does. Where he is as he
// goes through tiles is sent with the character, and the tiles breaking is sent by the tiles.
//
// Added to the Drill by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Drill))]
public class DrillNetwork : NetworkBehaviour
{
    [SyncVar] private byte pointing;

    private Drill drill;

    private Drill TheDrill
    {
        get
        {
            if (!drill)
            {
                drill = GetComponent<Drill>();
            }

            return drill;
        }
    }

    void Update()
    {
        if (isServer)
        {
            pointing = TheDrill.PointingForNetwork;
        }
        else if (isClient)
        {
            TheDrill.ShowPointing(pointing);
        }
    }
}
