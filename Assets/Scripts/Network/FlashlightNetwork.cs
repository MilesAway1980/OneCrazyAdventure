using Characters;
using Mirror;
using UnityEngine;

// Sends what other machines need to show the flashlight: where his head is aimed, which level the light is on, how full the battery
// is, whether it's flat and whether he's in water. On a machine that has joined someone else's game, the flashlight's own code
// turns his head, shines his light, shows the battery meter and reveals hidden tiles from these, as it does on the host.
//
// Added to the flashlight by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Flashlight))]
public class FlashlightNetwork : NetworkBehaviour
{
    [SyncVar] private float aim;
    [SyncVar] private int level;
    [SyncVar] private bool flat;
    [SyncVar] private bool inLiquid;

    // In thousandths, which is as fine as the meter and the flicker show.
    [SyncVar] private ushort battery = 1000;

    private Flashlight flashlight;

    private Flashlight Flash
    {
        get
        {
            if (!flashlight)
            {
                flashlight = GetComponent<Flashlight>();
            }

            return flashlight;
        }
    }

    public override void OnStartServer()
    {
        // The head turns quickly, so what is sent is not held back for long.
        syncInterval = 0.03f;
    }

    void Update()
    {
        if (isServer)
        {
            aim = Flash.AimAngle;
            level = Flash.Level;
            flat = Flash.IsFlat;
            inLiquid = Flash.inLiquid;
            battery = (ushort)Mathf.RoundToInt(Mathf.Clamp01(Flash.BatteryFraction) * 1000f);
        }
        else if (isClient)
        {
            Flash.ShowState(aim, level, flat, inLiquid, battery / 1000f);
        }
    }
}
