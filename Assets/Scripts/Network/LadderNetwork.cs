using Characters;
using Mirror;
using UnityEngine;

// Sends how far out the Ladder is, so that other machines show the ladder that long, with its rungs and its height bar. Climbing it,
// and what is in the way of it growing, are the host's.
//
// Added to the Ladder by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Ladder))]
public class LadderNetwork : NetworkBehaviour
{
    // In hundredths of a unit.
    [SyncVar] private ushort length;

    private Ladder ladder;

    private Ladder TheLadder
    {
        get
        {
            if (!ladder)
            {
                ladder = GetComponent<Ladder>();
            }

            return ladder;
        }
    }

    void Update()
    {
        if (isServer)
        {
            length = TheLadder.LengthForNetwork;
        }
    }

    // After he has been moved this frame, so the ladder is where he is. Not while he's put away, or the ladder would be switched
    // back on with nobody there.
    void LateUpdate()
    {
        if (!isServer && isClient && TheLadder.InGame)
        {
            TheLadder.ShowLength(length / 100f);
        }
    }
}
