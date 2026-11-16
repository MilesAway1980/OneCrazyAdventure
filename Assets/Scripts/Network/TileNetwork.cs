using System.Collections.Generic;
using Mirror;
using UnityEngine;

// How a tile was broken, for the other machines to break it the same way: the speed of whatever hit it, whether it just drops, the
// colour it had gone, and the push of a blast that knocks it loose as a block, if one did.
public struct TileBreakInfo
{
    public Vector2 impact;
    public bool drop;
    public Color32 tint;
    public bool hasPush;
    public Vector2 push;
}

// Puts the tiles that can be destroyed on the network. The host decides when each is hurt, burns, breaks and comes back, and sends:
//
//  - which tiles are gone now, and how each was broken. The machines that are only showing the game take them off the tilemaps, and
//    show the break (the debris, the sound) for one that has just broken. A tile that was already gone when a machine joined is
//    just gone.
//  - which tiles are showing something, and how far it has got: a warning that it is about to crumble, a glow as it heats, or
//    burning. Each machine shows it itself.
//
// Tiles are known by where they are in the world. There is one of these in a scene that has damageable tiles, made by Tools >
// Network > Make World Networked. Tiles on a tilemap that moves aren't supported.
public class TileNetwork : NetworkBehaviour
{
    public static TileNetwork Instance { get; private set; }

    public readonly SyncDictionary<uint, TileBreakInfo> gone = new SyncDictionary<uint, TileBreakInfo>();

    // What a tile that is showing something is showing: the kind in the high byte (1 glowing, 2 warning, 3 burning) and how far it has
    // got in the low six bits. A tile that isn't showing anything isn't in it.
    public readonly SyncDictionary<uint, ushort> showing = new SyncDictionary<uint, ushort>();

    // On a machine that has joined: which tiles were already gone when it joined, which it has taken off, and which are showing something.
    private readonly HashSet<uint> goneAtStart = new HashSet<uint>();
    private readonly HashSet<uint> takenOff = new HashSet<uint>();
    private readonly HashSet<uint> shown = new HashSet<uint>();
    private readonly List<uint> scratch = new List<uint>();

    public override void OnStartServer()
    {
        Instance = this;
    }

    public override void OnStartClient()
    {
        Instance = this;

        goneAtStart.Clear();
        foreach (var pair in gone)
        {
            goneAtStart.Add(pair.Key);
        }
    }

    public override void OnStopServer()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void OnStopClient()
    {
        if (Instance == this && !isServer)
        {
            Instance = null;
        }
    }

    // ----- On the host: said by the tiles -----

    public static void Broken(DamageableTile tile, Vector2 impact, bool drop, Color tint, Vector2? blockPush)
    {
        var net = Instance;

        if (!net || !net.isServer)
        {
            return;
        }

        var key = DamageableTile.KeyOf(tile.position);

        net.showing.Remove(key);
        net.gone[key] = new TileBreakInfo
        {
            impact = impact,
            drop = drop,
            tint = tint,
            hasPush = blockPush.HasValue,
            push = blockPush ?? Vector2.zero
        };
    }

    public static void Restored(DamageableTile tile)
    {
        var net = Instance;

        if (net && net.isServer)
        {
            net.gone.Remove(DamageableTile.KeyOf(tile.position));
        }
    }

    // What the tile is showing, or 0 for nothing.
    public static void Showing(DamageableTile tile, ushort value)
    {
        var net = Instance;

        if (!net || !net.isServer)
        {
            return;
        }

        var key = DamageableTile.KeyOf(tile.position);

        if (value == 0)
        {
            net.showing.Remove(key);
        }
        else
        {
            net.showing[key] = value;
        }
    }

    // ----- On a machine that has joined: what the host says is done here -----

    void Update()
    {
        if (isServer || !isClient)
        {
            return;
        }

        // Tiles that are gone. The tile might not be there to be taken off yet, as the sections make theirs when the map is ready, so
        // it's tried each frame until it is.
        foreach (var pair in gone)
        {
            var tile = DamageableTile.At(DamageableTile.PositionOf(pair.Key));

            if (tile && !tile.IsGoneHere)
            {
                tile.ApplyBroken(pair.Value, !goneAtStart.Contains(pair.Key));
                takenOff.Add(pair.Key);
            }
        }

        // Tiles that have come back.
        if (takenOff.Count > 0)
        {
            scratch.Clear();

            foreach (var key in takenOff)
            {
                if (!gone.ContainsKey(key))
                {
                    scratch.Add(key);
                }
            }

            foreach (var key in scratch)
            {
                var tile = DamageableTile.At(DamageableTile.PositionOf(key));

                if (tile)
                {
                    tile.ApplyRestored();
                }

                takenOff.Remove(key);
                goneAtStart.Remove(key);
            }
        }

        // What tiles are showing, every frame, so that a shaking tile goes on shaking between what the host sends.
        foreach (var pair in showing)
        {
            var tile = DamageableTile.At(DamageableTile.PositionOf(pair.Key));

            if (tile)
            {
                tile.ApplyShowing(pair.Value);
                shown.Add(pair.Key);
            }
        }

        if (shown.Count > 0)
        {
            scratch.Clear();

            foreach (var key in shown)
            {
                if (!showing.ContainsKey(key))
                {
                    scratch.Add(key);
                }
            }

            foreach (var key in scratch)
            {
                var tile = DamageableTile.At(DamageableTile.PositionOf(key));

                if (tile)
                {
                    tile.ApplyShowing(0);
                }

                shown.Remove(key);
            }
        }
    }
}
