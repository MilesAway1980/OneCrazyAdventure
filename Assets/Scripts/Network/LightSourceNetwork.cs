using Mirror;
using UnityEngine;

// Puts a light on the network: whether it's lit, for a sconce or anything else with a LightSource that isn't on a character.
// What lights it, how long it burns and when it goes out is decided on the host, and the other machines are told. The glow, the
// pulsing and the fade between lit and unlit are the LightSource's own, on every machine.
//
// A character's light, such as the Bulb's, is sent with the character. Added by Tools > Network > Make World Networked.
[RequireComponent(typeof(LightSource))]
public class LightSourceNetwork : NetworkBehaviour
{
    [SyncVar] private bool lit = true;

    // How lit it is, from 0 to 1, if a SwitchLight has set it to a level; below 0 if it's only lit or unlit.
    [SyncVar] private float level = -1f;

    private LightSource source;

    private LightSource Source
    {
        get
        {
            if (!source)
            {
                source = GetComponent<LightSource>();
            }

            return source;
        }
    }

    void Update()
    {
        if (isServer)
        {
            lit = Source.lit;
            level = Source.Level ?? -1f;
        }
        else if (isClient)
        {
            if (level >= 0f)
            {
                if (!Source.Level.HasValue || !Mathf.Approximately(Source.Level.Value, level))
                {
                    Source.SetLevel(level);
                }
            }
            else
            {
                Source.ClearLevel();

                if (Source.lit != lit)
                {
                    Source.SetLit(lit);
                }
            }
        }
    }
}
