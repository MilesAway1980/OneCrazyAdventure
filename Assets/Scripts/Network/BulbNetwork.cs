using Characters;
using Mirror;
using UnityEngine;

// Sends what other machines need to show the Bulb: which of the four states he is in (free, screwing in, in a socket, screwing out),
// and what his light was last told to do. Where he is, as he's carried down into a socket, is sent with the rest of the character.
// Which socket he goes into, and whether it's free, is decided on the host, so the sockets themselves aren't sent. On a machine
// that has joined someone else's game his own code turns his picture and shines his light from these.
//
// Added to the Bulb by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Bulb))]
public class BulbNetwork : NetworkBehaviour
{
    [SyncVar] private byte state;
    [SyncVar(hook = nameof(OnLightChanged))] private BulbLightCall light;

    private Bulb bulb;

    private Bulb TheBulb
    {
        get
        {
            if (!bulb)
            {
                bulb = GetComponent<Bulb>();
            }

            return bulb;
        }
    }

    void Update()
    {
        if (isServer)
        {
            state = TheBulb.StateForNetwork;
            light = TheBulb.LastLightCall;
        }
        else if (isClient)
        {
            TheBulb.ShowState(state);
        }
    }

    public override void OnStartClient()
    {
        if (!isServer)
        {
            // Whatever the light was last told before this machine joined.
            TheBulb.ShowState(state);
            TheBulb.ShowLight(light);
        }
    }

    private void OnLightChanged(BulbLightCall oldValue, BulbLightCall newValue)
    {
        if (!isServer)
        {
            TheBulb.ShowLight(newValue);
        }
    }
}
