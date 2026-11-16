using Characters;
using Mirror;
using UnityEngine;

// Sends what the Suction Cup is stuck to, and which way he faces on a wall, so that other machines show him holding on. How he sticks,
// slides and climbs is the host's.
//
// Added to the Suction Cup by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(SuctionCup))]
public class SuctionCupNetwork : NetworkBehaviour
{
    [SyncVar] private byte holding;
    [SyncVar] private bool facesRight;

    private SuctionCup cup;

    private SuctionCup TheCup
    {
        get
        {
            if (!cup)
            {
                cup = GetComponent<SuctionCup>();
            }

            return cup;
        }
    }

    void Update()
    {
        if (isServer)
        {
            holding = TheCup.HoldForNetwork;
            facesRight = TheCup.FacesRightOnWall;
        }
    }

    // After his picture has been chosen for walking and jumping, which what he holds on with replaces.
    void LateUpdate()
    {
        if (!isServer && isClient)
        {
            TheCup.ShowHold(holding, facesRight);
        }
    }
}
