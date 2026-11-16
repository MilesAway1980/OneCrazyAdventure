using Mirror;
using UnityEngine;

// Puts a character pickup on the network, so that when a character is found by someone on any machine, it's gone from all of
// them, and everyone is told. Who finds a character is decided on the host. Which characters have been found is sent separately,
// by the ConnectionPlayer.
//
// Added to the pickups by Tools > Network > Make World Networked.
[RequireComponent(typeof(CharacterPickup))]
public class PickupNetwork : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCollectedChanged))] private bool collected;

    private CharacterPickup pickup;

    private CharacterPickup Pickup
    {
        get
        {
            if (!pickup)
            {
                pickup = GetComponent<CharacterPickup>();
            }

            return pickup;
        }
    }

    void Update()
    {
        if (isServer && !collected && Pickup.Collected)
        {
            collected = true;
        }
    }

    public override void OnStartClient()
    {
        if (!isServer && collected)
        {
            Pickup.Disappear();
        }
    }

    // The message that it was found is the host's, sent to everyone, so only the pickup itself goes here.
    private void OnCollectedChanged(bool oldValue, bool newValue)
    {
        if (!isServer && newValue)
        {
            Pickup.Disappear();
        }
    }
}
