using Characters;
using Mirror;
using UnityEngine;

// Sends when the Brick shudders because he can't turn solid, so that other machines show it too. Whether he's solid already shows in his
// picture. Everything about being solid is the host's.
//
// Added to the Brick by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Brick))]
public class BrickNetwork : NetworkBehaviour
{
    [SyncVar] private int shudders;

    private Brick brick;
    private int shudderSeen;

    private Brick TheBrick
    {
        get
        {
            if (!brick)
            {
                brick = GetComponent<Brick>();
            }

            return brick;
        }
    }

    public override void OnStartClient()
    {
        // One from before this machine joined isn't shown.
        shudderSeen = shudders;
    }

    void Update()
    {
        if (isServer)
        {
            shudders = TheBrick.BlockedCount;
        }
    }

    // After his picture has been chosen, so the shudder is drawn over it.
    void LateUpdate()
    {
        if (isServer || !isClient)
        {
            return;
        }

        if (shudders != shudderSeen)
        {
            shudderSeen = shudders;
            TheBrick.StartShownFlash();
        }

        TheBrick.UpdateShownFlash();
    }
}
