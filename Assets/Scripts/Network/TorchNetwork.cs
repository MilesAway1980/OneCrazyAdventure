using Characters;
using Mirror;
using UnityEngine;

// Sends whether the Torch is burning, so that other machines show him put out in water: his light out and his unlit pictures. Where he
// lights sconces and what his heat does is decided on the host.
//
// Added to the Torch by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Torch))]
public class TorchNetwork : NetworkBehaviour
{
    [SyncVar] private bool burning = true;

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
            burning = TheTorch.IsBurning;
        }
    }

    // After the picture has been chosen for how he's facing, since a put-out Torch has pictures of his own.
    void LateUpdate()
    {
        if (!isServer && isClient)
        {
            TheTorch.ShowLit(burning);
        }
    }
}
