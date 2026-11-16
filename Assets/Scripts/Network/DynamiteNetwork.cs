using Characters;
using Mirror;
using UnityEngine;

// Sends when the Dynamite goes off and how far his fuse is burned, so that other machines show him exploded, the pieces flying out, and
// his fuse bar. What the blast pushes and breaks is the host's: the tiles it breaks are sent by the tiles, and what it pushes moves with
// the host.
//
// Added to the Dynamite by Tools > Network > Make Characters Networked.
[RequireComponent(typeof(Dynamite))]
public class DynamiteNetwork : NetworkBehaviour
{
    [SyncVar] private bool exploded;
    [SyncVar] private byte charge;

    private Dynamite dynamite;
    private bool explodedSeen;

    private Dynamite TheDynamite
    {
        get
        {
            if (!dynamite)
            {
                dynamite = GetComponent<Dynamite>();
            }

            return dynamite;
        }
    }

    public override void OnStartClient()
    {
        // He had already gone off before this machine joined: there's no blast to show.
        explodedSeen = exploded;
    }

    void Update()
    {
        if (isServer)
        {
            exploded = TheDynamite.exploded;
            charge = (byte)Mathf.RoundToInt(TheDynamite.ChargeFraction * 255f);
        }
    }

    void LateUpdate()
    {
        if (isServer || !isClient || !TheDynamite.InGame)
        {
            return;
        }

        if (exploded && !explodedSeen)
        {
            TheDynamite.ShowExplosion();
        }

        explodedSeen = exploded;
        TheDynamite.ShowState(exploded, charge / 255f);
    }
}
