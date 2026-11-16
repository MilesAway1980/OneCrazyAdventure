using System.Collections.Generic;
using UnityEngine;
using Mirror;

// Puts an enemy on the network. The host does all the thinking: where the enemy goes, how it gets there, when it jumps. Where it is is sent
// by the network rigidbody next to this, which turns it as well, and this sends which way its picture faces. A machine that has joined
// someone else's game switches the enemy's scripts off and only shows it, so it can't go off by itself and drift away from the host's.
//
// Added to enemies by Tools > Network > Make World Networked.
public class EnemyNetwork : NetworkBehaviour
{
    [SyncVar] private bool facingLeft;

    private SpriteRenderer picture;

    // What was switched off on this machine, to put back if it goes back to playing by itself.
    private readonly List<MonoBehaviour> switchedOff = new List<MonoBehaviour>();

    private SpriteRenderer Picture
    {
        get
        {
            if (!picture)
            {
                picture = GetComponent<SpriteRenderer>();

                if (!picture)
                {
                    picture = GetComponentInChildren<SpriteRenderer>();
                }
            }

            return picture;
        }
    }

    // The scripts that make an enemy move and decide.
    private static bool IsEnemyThinking(MonoBehaviour script)
    {
        return script is Enemy || script is EnemyMovement || script is EnemyGroundPath || script is EnemyPathFollow;
    }

    public override void OnStartClient()
    {
        if (isServer)
        {
            return;
        }

        foreach (var script in GetComponents<MonoBehaviour>())
        {
            if (script && script.enabled && IsEnemyThinking(script))
            {
                script.enabled = false;
                switchedOff.Add(script);
            }
        }
    }

    public override void OnStopClient()
    {
        if (isServer)
        {
            return;
        }

        foreach (var script in switchedOff)
        {
            if (script)
            {
                script.enabled = true;
            }
        }

        switchedOff.Clear();
    }

    void Update()
    {
        if (isServer && Picture)
        {
            facingLeft = Picture.flipX;
        }
    }

    // After the enemy's own scripts, so what the host says has the last word.
    void LateUpdate()
    {
        if (!isServer && isClient && Picture)
        {
            Picture.flipX = facingLeft;
        }
    }
}
