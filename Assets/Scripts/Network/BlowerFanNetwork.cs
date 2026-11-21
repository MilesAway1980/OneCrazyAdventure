using Mirror;
using UnityEngine;

// Puts how hard a switched fan blows on the network: its power, which sets how fast its picture turns. What the fan pushes is done on the
// host, which is where everything is simulated, so on a machine that has joined someone else's game this only follows the power it's sent.
//
// Added to the fans that a switch changes by Tools > Network > Make World Networked.
[RequireComponent(typeof(BlowerFan))]
public class BlowerFanNetwork : NetworkBehaviour
{
    [SyncVar] private float power = 1f;

    private BlowerFan fan;

    private BlowerFan Fan
    {
        get
        {
            if (!fan)
            {
                fan = GetComponent<BlowerFan>();
            }

            return fan;
        }
    }

    void Update()
    {
        if (isServer)
        {
            power = Fan.Power;
        }
        else if (isClient)
        {
            Fan.SetPowerFromNetwork(power);
        }
    }
}
