using Characters;
using Mirror;
using UnityEngine;

// Sends how much the Torch is burning, so that other machines show him put out in water, and lighting up again: his light and his unlit pictures. Where he
// lights sconces and what his heat does is decided on the host.
//
// Added to the Torch by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Torch))]
public class TorchNetwork : NetworkBehaviour
{
    // How much he's burning, in hundredths: 0 is out, 100 fully lit, and between is the small flame as he lights up again.
    [SyncVar] private byte flame = 100;

    private Torch torch;

    private Torch TheTorch
    {
        get
        {
            if (!torch)
            {
                torch = GetComponent<Torch>();
            }

            return torch;
        }
    }

    void Update()
    {
        if (isServer)
        {
            flame = (byte)Mathf.RoundToInt(Mathf.Clamp01(TheTorch.Flame) * 100f);
        }
    }

    // After the picture has been chosen for how he's facing, since a put-out Torch has pictures of his own.
    void LateUpdate()
    {
        if (!isServer && isClient)
        {
            TheTorch.ShowLit(flame / 100f);
        }
    }
}
