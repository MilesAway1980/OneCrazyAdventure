using Characters;
using Mirror;
using UnityEngine;

// Sends which form the Bouncy Ball is in (normal, charging a jump, or inflated) and how far his jump is charged, so that other machines
// show him squashed or blown up with his shell, and his charge bar. How he bounces and jumps is the host's.
//
// Added to the Bouncy Ball by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(BouncyBall))]
public class BouncyBallNetwork : NetworkBehaviour
{
    [SyncVar] private byte form;
    [SyncVar] private byte charge;

    private BouncyBall ball;

    private BouncyBall TheBall
    {
        get
        {
            if (!ball)
            {
                ball = GetComponent<BouncyBall>();
            }

            return ball;
        }
    }

    void Update()
    {
        if (isServer)
        {
            form = TheBall.FormForNetwork;
            charge = (byte)Mathf.RoundToInt(TheBall.ChargeFraction * 255f);
        }
    }

    // After his picture has been chosen for walking and jumping, which being squashed replaces. Not while he's put away, or his
    // shell would be switched on with nobody there.
    void LateUpdate()
    {
        if (!isServer && isClient && TheBall.InGame)
        {
            TheBall.ShowForm(form, charge / 255f);
        }
    }
}
