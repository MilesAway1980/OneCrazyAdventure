using Characters;
using Mirror;
using UnityEngine;

// Sends whether the Bowling Ball is rolling, so that other machines roll his picture round as he moves. How fast he's going, and what
// he breaks, is the host's.
//
// Added to the Bowling Ball by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(BowlingBall))]
public class BowlingBallNetwork : NetworkBehaviour
{
    [SyncVar] private bool rolling;

    private BowlingBall ball;

    private BowlingBall TheBall
    {
        get
        {
            if (!ball)
            {
                ball = GetComponent<BowlingBall>();
            }

            return ball;
        }
    }

    void Update()
    {
        if (isServer)
        {
            rolling = TheBall.IsPerformingAction();
        }
    }

    // After he has been moved this frame, so how far he has gone is what turns him.
    void LateUpdate()
    {
        if (!isServer && isClient)
        {
            TheBall.ShowRolling(rolling);
        }
    }
}
