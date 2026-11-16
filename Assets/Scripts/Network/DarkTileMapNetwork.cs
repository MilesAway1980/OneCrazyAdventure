using Mirror;
using UnityEngine;

// Puts the scene's darkness on the network: the two colours the ambient light pulses between, and how fast. A switch that brightens
// the scene changes them on the host, and the other machines are told. The pulse itself runs on every machine from those.
//
// Added by Tools > Network > Make World Networked.
[RequireComponent(typeof(DarkTileMap))]
public class DarkTileMapNetwork : NetworkBehaviour
{
    [SyncVar] private Color darkest;
    [SyncVar] private Color lightest;
    [SyncVar] private float changeSeconds;

    private DarkTileMap darkMap;

    private DarkTileMap Dark
    {
        get
        {
            if (!darkMap)
            {
                darkMap = GetComponent<DarkTileMap>();
            }

            return darkMap;
        }
    }

    void Update()
    {
        if (isServer)
        {
            darkest = Dark.darkest;
            lightest = Dark.lightest;
            changeSeconds = Dark.colorChangeSpeed;
        }
        else if (isClient)
        {
            Dark.darkest = darkest;
            Dark.lightest = lightest;
            Dark.colorChangeSpeed = changeSeconds;
        }
    }
}
