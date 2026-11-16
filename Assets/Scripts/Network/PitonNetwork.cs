using System.Collections.Generic;
using Characters;
using Mirror;
using UnityEngine;

// Sends what other machines need to draw the Piton: which state he's in (head on, thrown, stuck, or coming back), how he's aiming, where
// the head is, and the points the rope runs through. The rope is a chain of physics bodies on the host, with all its swinging and the
// characters crossing it, and none of that is sent: a machine that has joined someone else's game draws the head, the rope's links and the
// line through them, and the arrow, from these points, with no physics of its own. The characters who cross the rope are moved by their
// own positions, which are sent as for any character.
//
// Added to the Piton by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Piton))]
public class PitonNetwork : NetworkBehaviour
{
    [SyncVar] private byte state;
    [SyncVar] private bool aimShown;
    [SyncVar] private float aimAngle;
    [SyncVar] private byte aimPower;
    [SyncVar] private bool headOut;
    [SyncVar] private Vector2 headPosition;
    [SyncVar] private float headAngle;
    [SyncVar] private byte ropeMode;
    [SyncVar] private Vector2[] rope = new Vector2[0];

    // How often, in seconds, the points of the rope are worked out and sent.
    private const float RopeSendSeconds = 0.04f;

    private Piton piton;
    private float nextRopeSend;
    private readonly List<Vector2> ropePoints = new List<Vector2>();

    private Piton ThePiton
    {
        get
        {
            if (!piton)
            {
                piton = GetComponent<Piton>();
            }

            return piton;
        }
    }

    public override void OnStartServer()
    {
        // The head flies quickly, so what is sent is not held back for long.
        syncInterval = 0.03f;
    }

    void Update()
    {
        if (!isServer)
        {
            return;
        }

        state = ThePiton.StateForNetwork;
        aimShown = ThePiton.AimShownForNetwork;
        aimAngle = ThePiton.AimAngleForNetwork;
        aimPower = (byte)Mathf.RoundToInt(Mathf.Clamp01(ThePiton.AimPowerForNetwork) * 255f);

        headOut = ThePiton.TryGetHeadForNetwork(out var position, out var angle);
        headPosition = position;
        headAngle = angle;

        if (Time.time >= nextRopeSend)
        {
            nextRopeSend = Time.time + RopeSendSeconds;

            var mode = ThePiton.GetRopeForNetwork(ropePoints);

            if (mode != 0 || ropeMode != 0)
            {
                ropeMode = mode;
                rope = ropePoints.ToArray();
            }
        }
    }

    // After he and the rope have been moved this frame. Not while he's put away, or what's drawn would be left behind.
    void LateUpdate()
    {
        if (isServer || !isClient)
        {
            return;
        }

        if (!ThePiton.InGame)
        {
            ThePiton.ShowHead(false, Vector2.zero, 0f);
            ThePiton.ShowRope(0, null);
            return;
        }

        ThePiton.ShowState(state, aimShown, aimAngle, aimPower / 255f);
        ThePiton.ShowHead(headOut, headPosition, headAngle);
        ThePiton.ShowRope(ropeMode, rope);
    }
}
