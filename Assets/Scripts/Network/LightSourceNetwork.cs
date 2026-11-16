using Mirror;
using UnityEngine;

// Puts a light on the network: whether it's lit, for a sconce or anything else with a LightTileSource that isn't on a character.
// What lights it, how long it burns and when it goes out is decided on the host, and the other machines are told. The glow, the
// pulsing and the fade between lit and unlit are the LightTileSource's own, on every machine.
//
// A character's light, such as the Bulb's, is sent with the character. Added by Tools > Network > Make World Networked.
[RequireComponent(typeof(LightTileSource))]
public class LightSourceNetwork : NetworkBehaviour
{
    [SyncVar] private bool lit = true;

    private LightTileSource source;

    private LightTileSource Source
    {
        get
        {
            if (!source)
            {
                source = GetComponent<LightTileSource>();
            }

            return source;
        }
    }

    void Update()
    {
        if (isServer)
        {
            lit = Source.lit;
        }
        else if (isClient && Source.lit != lit)
        {
            Source.SetLit(lit);
        }
    }
}
