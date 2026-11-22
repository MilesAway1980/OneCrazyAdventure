using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DamageableTile : DamageableTileOptions
{
    [Tooltip("Where this tile is in its tilemap, as a cell. Set by the section that made it.")]
    public Vector2Int position;

    // The section this tile is a part of, which is where what shows when it breaks is taken from.
    [Tooltip("The section this tile is a part of, which is where what shows when it breaks is taken from.")]
    public DamageableTileOptions section;

    private List<(Tilemap, TileBase)> tiles;

    // Where this tile is on each tilemap it is on, worked out once when it starts. It's the same tile on the same cells
    // wherever its tilemap goes, so the tilemap's cells are used from then on, not worked out again from the world.
    private readonly Dictionary<Tilemap, Vector3Int> cellOn = new Dictionary<Tilemap, Vector3Int>();

    private float destroyedTime;
    private bool destroyed;

    // How fast something can be going up and still count as standing on the tile, rather than as jumping off it.
    private const float RisingSpeedLimit = 0.5f;

    // How big a gap, in units, there can be between the tile and something for it to count as touching the tile. It's the
    // smallest the physics leaves between them, and a little more.
    private const float TouchTolerance = 0.05f;

    // How long, in seconds, something goes on counting as standing on, or touching, the tile after it was last seen on it,
    // as touching can stop for an instant.
    private const float ContactGraceSeconds = 0.15f;

    // What is standing on the tile now, and when each was last seen on it, and for how long there has been enough of it.
    private readonly Dictionary<CanTriggerFloorCollapse, float> standers = new Dictionary<CanTriggerFloorCollapse, float>();
    private float standTime;

    // The same for what is touching the tile, from the sides that are ticked.
    private readonly Dictionary<CanTriggerFloorCollapse, float> touchers = new Dictionary<CanTriggerFloorCollapse, float>();
    private float touchTime;

    private readonly List<CanTriggerFloorCollapse> departed = new List<CanTriggerFloorCollapse>();

    // What is touching the tile that gives off heat, and when each was last seen, and for how long there has been some.
    private readonly Dictionary<HeatSource, float> heaters = new Dictionary<HeatSource, float>();
    private readonly List<HeatSource> cooled = new List<HeatSource>();
    private float heatTime;

    // Whether the tile is alight, for how long it has been, whether it has set fire to the tiles next to it yet, and the
    // flames put on it, if there are any.
    private bool burning;
    private float burnTime;
    private bool spread;
    private GameObject flames;

    // Every tile, by where it is, so that fire can find the ones next to the tile it is on.
    private static readonly Dictionary<Vector2Int, DamageableTile> tilesByPosition = new Dictionary<Vector2Int, DamageableTile>();

    // The tile's picture laid over it while it shows that it is about to give way, and how dark it is made.
    private List<TileBreakEffects.TileOverlay> warning;
    private Color warningTint = Color.white;

    private Collider2D triggerCollider;

    // The space of every tile that can be destroyed, whether or not it is at the moment. Somewhere a tile may come back
    // to isn't a place to leave anyone.
    private static readonly HashSet<Vector2Int> destroyableTileSpaces = new HashSet<Vector2Int>();

    void Start()
    {
        tiles = Map.GetTiles(position, true);

        foreach (var (tilemap, _) in tiles)
        {
            cellOn[tilemap] = Map.ToCell(tilemap, position);
        }

        destroyed = false;
        destroyableTileSpaces.Add(position);
        tilesByPosition[position] = this;

        triggerCollider = GetComponent<Collider2D>();
    }

    void OnDestroy()
    {
        destroyableTileSpaces.Remove(position);

        if (tilesByPosition.TryGetValue(position, out var registered) && registered == this)
        {
            tilesByPosition.Remove(position);
        }

        ClearWarning();
        PutOutFire();
    }

    public static bool IsDestroyableTileSpace(Vector2Int cell)
    {
        return destroyableTileSpaces.Contains(cell);
    }

    // The tile in a space, or null if there is none.
    public static DamageableTile At(Vector2Int cell)
    {
        tilesByPosition.TryGetValue(cell, out var tile);
        return tile;
    }

    // How far, from 0 to 1, a digger is through the tile, and when it last said so. A tile is being dug while it is
    // still being told.
    private float digProgress;
    private float lastDigTime = float.NegativeInfinity;

    private bool BeingDug
    {
        get { return Time.time - lastDigTime < DigSignalSeconds; }
    }

    // How long, in seconds, a tile goes on counting as being dug after the digger last told it.
    private const float DigSignalSeconds = 0.15f;

    // How fast, in units per second, the debris is thrown the way a tile is dug through.
    private const float DigDebrisSpeed = 6f;

    // How hard the tile is to dig through. It is never nothing, so a digger can't get through it in no time.
    public float Hardness
    {
        get { return Mathf.Max(Options.digHardness, 0.01f); }
    }

    // Whether it can be dug through the way a digger is going: one going up is at the bottom of the tile, so it is the
    // bottom side of it that has to be ticked, and one going down is at the top.
    public bool CanBeDugFrom(Vector2 direction)
    {
        if (destroyed || !Options.dig)
        {
            return false;
        }

        if (direction.y != 0f)
        {
            return IsDamagedFrom(direction.y > 0f ? Side.Down : Side.Up);
        }

        return IsDamagedFrom(direction.x > 0f ? Side.Left : Side.Right);
    }

    // A tile with a dig radius is dug through as one bigger block: this tile is the middle of the face the digger meets, and
    // the block goes out the digger's way from it, and is as wide as the radius reaches either side. A radius of 1 is 3
    // tiles wide and 3 long, a radius of 2 is 5 by 5, and 0 is just the tile. Only the cells of the block that are
    // tiles that can be dug are in it, and it ends at the first tile in the middle line that isn't.

    // How many tiles long the block is going the digger's way, counting from this one.
    public int DigBlockLength(Vector2 direction)
    {
        var forward = DigStep(direction);
        var most = Mathf.Max(Options.digRadius, 0) * 2 + 1;
        var rows = 1;

        while (rows < most)
        {
            var next = At(position + forward * rows);

            if (!next || next.destroyed || !next.Options.dig)
            {
                break;
            }

            rows++;
        }

        return rows;
    }

    private static Vector2Int DigStep(Vector2 direction)
    {
        return new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));
    }

    private static readonly List<(DamageableTile tile, Vector2 outward)> digBlock = new List<(DamageableTile, Vector2)>();

    // Fills digBlock with the tiles that are in the block, each with the way its debris goes: the digger's way for those in
    // the middle line, and for the rest, angled out to the side they are on.
    private void FindDigBlock(Vector2 direction, int rows)
    {
        digBlock.Clear();

        var forward = DigStep(direction);
        var across = new Vector2Int(Mathf.Abs(forward.y), Mathf.Abs(forward.x));
        var radius = Mathf.Max(Options.digRadius, 0);

        for (int row = 0; row < rows; row++)
        {
            for (int side = -radius; side <= radius; side++)
            {
                var tile = At(position + forward * row + across * side);

                if (tile && !tile.destroyed && tile.Options.dig)
                {
                    var outward = ((Vector2)forward + (Vector2)across * (side * 0.5f)).normalized;

                    digBlock.Add((tile, outward));
                }
            }
        }
    }

    // A digger says how far through the block it is, which is shown on all of it if it is set to show crumbling.
    public void ShowDig(float progress, Vector2 direction, int rows)
    {
        FindDigBlock(direction, rows);

        foreach (var (tile, _) in digBlock)
        {
            tile.digProgress = progress;
            tile.lastDigTime = Time.time;
        }
    }

    // The digger is all the way through the block, and all of it breaks, outright. They don't dig out more around themselves.
    public void BreakByDig(Vector2 direction, int rows)
    {
        FindDigBlock(direction, rows);

        foreach (var (tile, outward) in digBlock)
        {
            tile.Break(outward * DigDebrisSpeed);
        }
    }

    private static readonly Collider2D[] occupants = new Collider2D[4];

    // Whether anything is in the space of the tile: a character, or an enemy. Characters are judged by the same test the
    // tile uses to make room for them when it comes back, so it never waits for less than it would otherwise push out. A
    // character's tail sticking only a little way into the space counts.
    private bool IsOccupied()
    {
        if (Characters.Character.AnyCharacterInCell(position))
        {
            return true;
        }

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(LayerMask.GetMask("Enemy"));

        // A little smaller than the space, so that something standing against it isn't in it.
        var centre = new Vector2(position.x + 0.5f, position.y + 0.5f);

        return Physics2D.OverlapBox(centre, Vector2.one * 0.96f, 0f, filter, occupants) > 0;
    }

    // The settings of the section this tile is a part of, as they are now, so they can be changed while playing. A tile that
    // isn't a part of one goes by its own.
    private DamageableTileOptions Options
    {
        get { return section ? section : this; }
    }

    // ----- Over the network -----
    //
    // The host decides everything that happens to a tile. A machine that has joined someone else's game does none of it, and is told what
    // the tile is doing (see TileNetwork).

    // A tile is known to the other machines by where it is in the world.
    public static uint KeyOf(Vector2Int place)
    {
        return (uint)((place.x + 32768) & 0xFFFF) << 16 | (uint)((place.y + 32768) & 0xFFFF);
    }

    public static Vector2Int PositionOf(uint key)
    {
        return new Vector2Int((int)(key >> 16) - 32768, (int)(key & 0xFFFF) - 32768);
    }

    // Whether the tile has been taken off here, on a machine that has joined.
    public bool IsGoneHere
    {
        get { return destroyed; }
    }

    // The host says the tile broke: it goes, and, if it has only just broken, it shows how it broke.
    public void ApplyBroken(TileBreakInfo info, bool showBreakEffects)
    {
        if (destroyed)
        {
            return;
        }

        ClearWarning();

        if (showBreakEffects)
        {
            Color tint = info.tint;

            if (info.hasPush)
            {
                ShowBlock(info.push, tint);
            }
            else
            {
                ShowBreak(info.impact, info.drop, tint);
            }
        }

        PutOutFire();
        destroyedTime = Time.time;
        destroyed = true;
        RemoveTiles();
    }

    // The host says the tile has come back.
    public void ApplyRestored()
    {
        if (!destroyed)
        {
            return;
        }

        PlaceTiles();
        destroyed = false;
    }

    // The host says what the tile is showing: nothing, a glow, a warning that it will crumble, or burning, and how far it has got.
    public void ApplyShowing(ushort value)
    {
        if (destroyed)
        {
            return;
        }

        var kind = value >> 8;
        var progress = (value & 63) / 63f;

        switch (kind)
        {
            case 1:
                ShowGlow(Options, progress);
                break;

            case 2:
                ShowWarning(Options, progress);
                break;

            case 3:
                ShowBurning(Options, progress);
                ShowFlames();
                break;

            default:
                ClearWarning();
                PutOutFire();
                break;
        }
    }

    // What was last said to the other machines about what the tile is showing.
    private ushort lastReportedShowing;

    // Tells the other machines what the tile is showing, if that has changed in a way they would notice.
    private void ReportShowing(int kind, float progress)
    {
        var value = kind == 0 ? (ushort)0 : (ushort)((kind << 8) | Mathf.RoundToInt(Mathf.Clamp01(progress) * 63f));

        if (value != lastReportedShowing)
        {
            lastReportedShowing = value;
            TileNetwork.Showing(this, value);
        }
    }

    // The flames on the tile, if it burns with any.
    private void ShowFlames()
    {
        var options = Options;

        if (flames || !options.fireFlames)
        {
            return;
        }

        flames = Instantiate(options.fireFlames, new Vector3(position.x + 0.5f, position.y + 0.5f, 0f), Quaternion.identity);
        TileBreakEffects.SortInFront(flames, tiles.Select(tile => tile.Item1));
    }

    void Update()
    {
        // In someone else's game the host decides what happens to the tile, and says so.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        var options = Options;

        if (destroyed)
        {
            if (options.timeStayDestroyed == 0)
            {
                //Do nothing, remains destroyed for duration of gameplay.
            }
            else if (options.timeStayDestroyed < 0)
            {
                //TODO stay destroyed forever.  Save to save file.
            }
            else
            {
                var elapsed = Time.time - destroyedTime;

                // A tile that was dug doesn't come back into a space that something is in. It waits until it's clear.
                if (elapsed >= options.timeStayDestroyed && !(options.dig && IsOccupied()))
                {

                    PlaceTiles();
                    TileNetwork.Restored(this);
                    destroyed = false;
                    ResetContacts();
                }
            }
        }
        else if (options.standOn || options.touch || options.fire || burning || BeingDug || warning != null)
        {
            UpdateContacts();
        }

        // Nothing can damage a tile that's gone, so while it is, it isn't listening for anything touching it.
        if (triggerCollider)
        {
            var listening = !destroyed;

            if (triggerCollider.enabled != listening)
            {
                triggerCollider.enabled = listening;
            }
        }
    }

    //private void OnTriggerStay2D(Collider2D other)
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!GameSession.IsClientOnly)
        {
            HandleCollision(collider);
        }
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        if (!GameSession.IsClientOnly)
        {
            HandleCollision(collider);
        }
    }

    private void ResetContacts()
    {
        standers.Clear();
        standTime = 0f;
        touchers.Clear();
        touchTime = 0f;
        heaters.Clear();
        heatTime = 0f;
        PutOutFire();
    }

    // Runs the time for standing on the tile and touching it, for whichever the tile is set to. Each only counts while it's
    // going on, and starts over when it isn't. Whichever is further on is what the warning shows, if the tile is set to.
    private void UpdateContacts()
    {
        var options = Options;

        var progress = 0f;

        if (options.standOn)
        {
            progress = Mathf.Max(progress, UpdateStanding(options));
        }

        if (!destroyed && options.touch)
        {
            progress = Mathf.Max(progress, UpdateTouching(options));
        }

        // How far a digger is through it shows in the same way.
        if (BeingDug)
        {
            progress = Mathf.Max(progress, digProgress);
        }

        var heating = 0f;

        if (!destroyed && (options.fire || burning))
        {
            heating = UpdateFire(options);
        }

        if (destroyed)
        {
            return;
        }

        // A tile that is burning shows that, and not the warning for crumbling.
        if (burning)
        {
            return;
        }

        // One that is heating up glows, if it is set to, ahead of the warning for crumbling.
        if (heating > 0f && options.fireWarning)
        {
            ShowGlow(options, heating);
        }
        else if (progress > 0f)
        {
            ShowWarning(options, progress);
        }
        else
        {
            ClearWarning();
        }
    }

    // The fire on the tile. Something giving off heat has to touch it for long enough to set it alight, and then it burns
    // by itself, whether or not that goes on touching it. A moment after it catches, it sets fire to the tiles next to it
    // that burn, and after it has burned for long enough it is destroyed. While it is being heated, it says how far it is
    // from catching, from 0 to 1.
    private float UpdateFire(DamageableTileOptions options)
    {
        if (!burning)
        {
            if (options.fireReach > 0f)
            {
                FindHeatWithinReach(options);
            }

            ForgetCooled(options);

            if (heaters.Count == 0)
            {
                // With the heat gone it cools off, at the speed it would take to cool from nearly alight in the cooling
                // time, rather than going straight back to cold.
                if (heatTime > 0f)
                {
                    heatTime = options.fireCoolSeconds > 0f
                        ? Mathf.Max(0f, heatTime - Time.deltaTime * options.fireIgniteSeconds / options.fireCoolSeconds)
                        : 0f;
                }

                return options.fireIgniteSeconds > 0f ? heatTime / options.fireIgniteSeconds : 0f;
            }

            heatTime += Time.deltaTime;

            if (heatTime >= options.fireIgniteSeconds)
            {
                CatchFire(options);
                return 0f;
            }

            return heatTime / options.fireIgniteSeconds;
        }

        burnTime += Time.deltaTime;

        if (!spread && burnTime >= options.fireSpreadSeconds)
        {
            spread = true;
            SpreadFire();
        }

        if (burnTime >= options.fireBurnSeconds)
        {
            // What's left of it just falls, as dark as it had become.
            Break(Vector2.zero, true);
            return 0f;
        }

        ShowBurning(options, burnTime / options.fireBurnSeconds);

        return 0f;
    }

    // How red a tile glows when it is by that much of the way to catching fire.
    private static Color FireGlow(DamageableTileOptions options, float progress)
    {
        return Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f, 1f), options.fireGlowAmount * progress);
    }

    // Glows red, more as the heat goes on, by laying its picture over it.
    private void ShowGlow(DamageableTileOptions options, float progress)
    {
        ReportShowing(1, progress);
        EnsureOverlays();

        warningTint = FireGlow(options, progress);

        foreach (var overlay in warning)
        {
            overlay.renderer.color = overlay.color * warningTint;
            overlay.transform.position = overlay.home;
        }
    }

    // Whether a heat source can heat the tile: it has to be giving off heat, and be intense enough to set fire to it. One
    // that isn't intense enough does nothing to it, however long it is there.
    private static bool CanHeat(HeatSource source, DamageableTileOptions options)
    {
        return source && source.GetLevel() > 0f && source.GetFireIntensity() >= options.fireIntensityNeeded;
    }

    // Notes everything that is giving off heat, and intense enough, and is near enough: no further than the reach from the
    // tile to the edge of it.
    private void FindHeatWithinReach(DamageableTileOptions options)
    {
        var reach = options.fireReach;
        var tileMin = new Vector2(position.x, position.y);
        var tileMax = tileMin + Vector2.one;

        foreach (var source in HeatSource.Active)
        {
            if (!CanHeat(source, options))
            {
                continue;
            }

            // How far apart the two boxes are across and up and down, which is nothing where they overlap.
            var bounds = source.GetBounds();
            var across = Mathf.Max(Mathf.Max(0f, tileMin.x - bounds.max.x), bounds.min.x - tileMax.x);
            var up = Mathf.Max(Mathf.Max(0f, tileMin.y - bounds.max.y), bounds.min.y - tileMax.y);

            if (across * across + up * up <= reach * reach)
            {
                heaters[source] = Time.time;
            }
        }
    }

    // Anything that has stopped giving off heat enough, or hasn't been seen on the tile lately, is forgotten.
    private void ForgetCooled(DamageableTileOptions options)
    {
        cooled.Clear();

        foreach (var heater in heaters)
        {
            if (!CanHeat(heater.Key, options) || Time.time - heater.Value > ContactGraceSeconds)
            {
                cooled.Add(heater.Key);
            }
        }

        foreach (var gone in cooled)
        {
            heaters.Remove(gone);
        }
    }

    // Sets the tile alight, if it can be and isn't already. It starts with the flames, if there are any.
    private void CatchFire(DamageableTileOptions options)
    {
        if (destroyed || burning || !options.fire)
        {
            return;
        }

        burning = true;
        burnTime = 0f;
        spread = false;
        heaters.Clear();
        heatTime = 0f;

        if (options.fireFlames)
        {
            flames = Instantiate(options.fireFlames, new Vector3(position.x + 0.5f, position.y + 0.5f, 0f), Quaternion.identity);
            TileBreakEffects.SortInFront(flames, tiles.Select(tile => tile.Item1));
        }
    }

    // Sets fire to the tiles next to this one, above, below and either side, that are damaged by fire and need no more
    // intensity than this fire has. Ones that are already alight, or gone, aren't touched, so the fire goes on to wherever
    // there are tiles left to burn, and stops where there are not.
    private void SpreadFire()
    {
        var next = new[]
        {
            position + Vector2Int.up,
            position + Vector2Int.down,
            position + Vector2Int.left,
            position + Vector2Int.right
        };

        // How intense this fire is: as much as the tile was set to, and never less than it took to set it alight.
        var options = Options;
        var intensity = Mathf.Max(options.fireBurnIntensity, options.fireIntensityNeeded);

        foreach (var cell in next)
        {
            // It only spreads to a tile that needs no more than that. One that needs more is left for a hotter fire.
            if (tilesByPosition.TryGetValue(cell, out var neighbour) && intensity >= neighbour.Options.fireIntensityNeeded)
            {
                neighbour.CatchFire(neighbour.Options);
            }
        }
    }

    // Burnt blacker the longer it has burned, by laying its picture over it, as when it is about to crumble.
    private void ShowBurning(DamageableTileOptions options, float progress)
    {
        ReportShowing(3, progress);
        EnsureOverlays();

        // It goes on from as red as it glowed, if it was glowing, and burns to black from there.
        var start = options.fireWarning ? FireGlow(options, 1f) : Color.white;
        var shade = 1f - options.fireCharAmount;
        warningTint = Color.Lerp(start, new Color(shade, shade, shade, 1f), progress);

        foreach (var overlay in warning)
        {
            overlay.renderer.color = overlay.color * warningTint;
            overlay.transform.position = overlay.home;
        }
    }

    // Takes the fire off the tile, along with the flames.
    private void PutOutFire()
    {
        burning = false;
        burnTime = 0f;
        spread = false;

        if (flames)
        {
            Destroy(flames);
            flames = null;
        }
    }

    // Anything that hasn't been seen on the tile lately has gone, and what's left is added up. The heaviest is found too.
    private void ForgetDeparted(Dictionary<CanTriggerFloorCollapse, float> contacts, out float weight, out float heaviest)
    {
        weight = 0f;
        heaviest = 0f;
        departed.Clear();

        foreach (var contact in contacts)
        {
            if (!contact.Key || !contact.Key.triggersCollapse || Time.time - contact.Value > ContactGraceSeconds)
            {
                departed.Add(contact.Key);
            }
            else
            {
                weight += contact.Key.Weight;
                heaviest = Mathf.Max(heaviest, contact.Key.Weight);
            }
        }

        foreach (var gone in departed)
        {
            contacts.Remove(gone);
        }
    }

    // How far on the time is for standing on the tile, from 0 to 1, and it falls if it gets there. It runs while enough
    // is standing on it.
    private float UpdateStanding(DamageableTileOptions options)
    {
        ForgetDeparted(standers, out var weight, out var heaviest);

        // It's either everything on it together that has to be enough, or one thing alone.
        var weighing = options.standOnWeightsAdd ? weight : heaviest;

        if (standers.Count == 0 || weighing < options.standOnWeightNeeded)
        {
            standTime = 0f;
            return 0f;
        }

        standTime += Time.deltaTime;

        if (standTime >= options.standOnTimeNeeded)
        {
            // Whatever is on it just falls, and so does the debris.
            Break(Vector2.zero, true);
            return 1f;
        }

        return standTime / options.standOnTimeNeeded;
    }

    // The same for touching the tile, which has no weight to it: it runs while anything that counts is touching it.
    private float UpdateTouching(DamageableTileOptions options)
    {
        ForgetDeparted(touchers, out _, out _);

        if (touchers.Count == 0)
        {
            touchTime = 0f;
            return 0f;
        }

        touchTime += Time.deltaTime;

        if (touchTime >= options.touchTimeNeeded)
        {
            Break(Vector2.zero, true);
            return 1f;
        }

        return touchTime / options.touchTimeNeeded;
    }

    // The tile's picture laid over it, for showing that it is about to crumble or is burning, if it isn't already.
    private void EnsureOverlays()
    {
        if (warning != null)
        {
            return;
        }

        warning = new List<TileBreakEffects.TileOverlay>();

        foreach (var (tilemap, _) in tiles)
        {
            var overlay = TileBreakEffects.CreateOverlay(tilemap, cellOn[tilemap]);

            if (overlay != null)
            {
                warning.Add(overlay);
            }
        }
    }

    // Lays the tile's picture over it, darker, shaking, or both, by how far the time has run. The tile itself is left
    // alone, as its solid part has to stay where it is.
    private void ShowWarning(DamageableTileOptions options, float progress)
    {
        if (!options.crumbleWarning)
        {
            ClearWarning();
            return;
        }

        ReportShowing(2, progress);
        EnsureOverlays();

        var darken = options.crumbleWarningType != WarningType.Shake;
        var shake = options.crumbleWarningType != WarningType.Darken;

        var shade = darken ? 1f - options.warningDarkness * progress : 1f;
        warningTint = new Color(shade, shade, shade, 1f);

        // It shakes a little at first and more as it goes.
        var reach = options.warningShake * Mathf.Lerp(0.3f, 1f, progress);

        foreach (var overlay in warning)
        {
            overlay.renderer.color = overlay.color * warningTint;
            overlay.transform.position = overlay.home + (shake ? (Vector3)(UnityEngine.Random.insideUnitCircle * reach) : Vector3.zero);
        }
    }

    private void ClearWarning()
    {
        ReportShowing(0, 0f);

        warningTint = Color.white;

        if (warning == null)
        {
            return;
        }

        foreach (var overlay in warning)
        {
            if (overlay.transform)
            {
                Destroy(overlay.transform.gameObject);
            }
        }

        warning = null;
    }

    // Destroys the tile, with whatever shows that it has been. The velocity is that of whatever hit it, so that the debris
    // goes the way it was hit, or none if nothing did. If it drops, the debris just falls.
    // If a blast is knocking it loose as a block, that is the push it is thrown with, and the debris isn't made.
    private void Break(Vector2 impact, bool drop = false, Vector2? blockPush = null)
    {
        // The debris is as dark as the tile was, if it was showing that it was going.
        var tint = warningTint;
        TileNetwork.Broken(this, impact, drop, tint, blockPush);
        ClearWarning();

        // Before the tile is taken away, as the debris is made from its picture.
        if (blockPush.HasValue)
        {
            ShowBlock(blockPush.Value, tint);
        }
        else
        {
            ShowBreak(impact, drop, tint);
        }

        destroyedTime = Time.time;
        destroyed = true;
        ResetContacts();
        RemoveTiles();
    }

    // Takes the tile off every tilemap it is on.
    private void RemoveTiles()
    {
        foreach (var (tilemap, _) in tiles)
        {
            tilemap.SetTile(cellOn[tilemap], null);
        }
    }

    // Puts the tile back on every tilemap it was on, after letting whatever is in its space know it is coming.
    private void PlaceTiles()
    {
        Map.AnnounceSolidTile(position);

        foreach (var (tilemap, tile) in tiles)
        {
            tilemap.SetTile(cellOn[tilemap], tile);
        }
    }

    private void ShowBreak(Vector2 impact, bool drop, Color tint)
    {
        var centre = new Vector3(position.x + 0.5f, position.y + 0.5f, 0f);

        var options = Options;

        TileBreakEffects.Sound(options.breakSound, centre);

        if (options.breakEffect == BreakEffect.Particles)
        {
            TileBreakEffects.Particles(options.breakParticles, centre, tiles.Select(tile => tile.Item1));
        }
        else if (options.breakEffect == BreakEffect.Debris)
        {
            if (tiles.Count == 0)
            {
                Debug.LogWarning($"The tile at {position} has no tiles on tilemaps that can be destroyed, so it has no debris.", this);
            }

            // The pictures are taken now, while the tile is still there, but the pieces aren't made until the physics has
            // caught up with the tile being taken away.
            var plans = new List<TileBreakEffects.DebrisPlan>();

            foreach (var (tilemap, _) in tiles)
            {
                if (TileBreakEffects.PlanDebris(tilemap, cellOn[tilemap], out var plan))
                {
                    plan.color *= tint;
                    plans.Add(plan);
                }
            }

            StartCoroutine(ThrowDebrisWhenClear(plans, options, impact, drop));
        }
    }

    // The solid part of a tile is only taken away by the physics a step after the tile is. Pieces made before then would be
    // inside it, and pushed out of it, in the same direction, instead of flying off the way they are thrown.
    private IEnumerator ThrowDebrisWhenClear(
        List<TileBreakEffects.DebrisPlan> plans, DamageableTileOptions options, Vector2 impact, bool drop)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        foreach (var plan in plans)
        {
            TileBreakEffects.ThrowDebris(plan, options, impact, drop);
        }
    }

    private enum Side
    {
        Up,
        Down,
        Left,
        Right
    }

    // The side of the tile that something is touching. The two are boxes that touch or just overlap, and they touch along
    // the axis they overlap least on: something beside the tile overlaps it a lot up and down and hardly at all across, and
    // something on top of it is the other way round. Which way is then which side of the tile it is on. That isn't the
    // same as which way its middle is from the tile's: a tall character beside a wall has its middle level with other
    // tiles, so from the middle it would be above or below the one it is touching. A tie, which is a corner, goes to
    // up or down.
    //
    // It also says whether the two are touching, which is when there is no more than a tiny gap along either axis. The
    // trigger can be a long way from that, as it is as wide as the buffer the section is given, so something going past the
    // corner of a tile, with the corner a little way off along both axes, is in the trigger without touching the tile.
    private Side SideTouched(Collider2D other, out bool touching)
    {
        var bounds = other.bounds;

        // How far they overlap along each axis, which is negative if there is a small gap between them.
        var overlapX = Mathf.Min(bounds.max.x, position.x + 1) - Mathf.Max(bounds.min.x, position.x);
        var overlapY = Mathf.Min(bounds.max.y, position.y + 1) - Mathf.Max(bounds.min.y, position.y);

        touching = overlapX >= -TouchTolerance && overlapY >= -TouchTolerance;

        if (overlapY <= overlapX)
        {
            return bounds.center.y > position.y + 0.5f ? Side.Up : Side.Down;
        }

        return bounds.center.x > position.x + 0.5f ? Side.Right : Side.Left;
    }

    // Whether the side is ticked, with all of them counting when that is.
    private bool IsDamagedFrom(Side side)
    {
        var options = Options;

        if (options.All)
        {
            return true;
        }

        switch (side)
        {
            case Side.Up:
                return options.Up;

            case Side.Down:
                return options.Down;

            case Side.Left:
                return options.Left;

            default:
                return options.Right;
        }
    }

    private void HandleCollision(Collider2D other)
    {
        if (destroyed)
        {
            return;
        }
        
        var options = Options;
        var side = SideTouched(other, out var touching);

        // Rolling into it counts from the sides that are ticked, from as far away as the trigger reaches, so that it can be
        // seen coming before it has been slowed by the hit.
        if (options.roll && IsDamagedFrom(side))
        {
            if (other.TryGetComponent<RollDamage>(out var rd) && rd.CanBreak(options.rollSpeedNeeded))
            {
                Break(rd.GetImpactVelocity());
            }
        }

        // Standing on it and touching it are touching it, and only count while it is.
        if (!touching)
        {
            return;
        }

        // Fire doesn't care which side the heat is on. Heat that has to be touching is noticed here. Heat with a reach is
        // looked for by distance instead, as it can be further away than the trigger reaches.
        if (options.fire && !burning && options.fireReach <= 0f)
        {
            NoteHeat(other, options);
        }

        // Standing on it is on the top of it, whichever sides are ticked. It isn't standing if it's on its way up.
        if (options.standOn && side == Side.Up)
        {
            NoteContact(other, standers, true);
        }

        // Touching it counts from the sides that are ticked.
        if (options.touch && IsDamagedFrom(side))
        {
            NoteContact(other, touchers, false);
        }
    }

    // Something is touching the tile, and it's noted if it is giving off heat, enough of it, at the moment.
    private void NoteHeat(Collider2D other, DamageableTileOptions options)
    {
        if (other.isTrigger)
        {
            return;
        }

        var source = other.GetComponentInParent<HeatSource>();
        if (CanHeat(source, options))
        {
            heaters[source] = Time.time;
        }
    }

    // Something is touching the tile. It's noted if it is the kind of thing that counts. Something going up past the speed
    // limit isn't standing on it, but it can be touching it.
    private void NoteContact(Collider2D other, Dictionary<CanTriggerFloorCollapse, float> contacts, bool notRising)
    {
        if (other.isTrigger)
        {
            return;
        }

        var counter = other.GetComponentInParent<CanTriggerFloorCollapse>();
        if (!counter || !counter.triggersCollapse)
        {
            return;
        }

        var body = other.attachedRigidbody;
        if (notRising && body && body.linearVelocity.y > RisingSpeedLimit)
        {
            return;
        }

        contacts[counter] = Time.time;
    }

    // The strongest push, in units per second, that a blast gives the debris of a tile it breaks, so a huge explosion doesn't
    // throw it out of the level.
    private const float BlastDebrisSpeedLimit = 15f;

    // An explosion, felt by every tile it reaches that can be blown apart: those within its radius of its centre, measured
    // to the middle of the tile. The force is the strongest at the centre and fades to nothing at the edge of the radius,
    // and a tile that needs more than it feels there is left alone. It is the same push that bodies in range get, so
    // anything that explodes can call this alongside pushing them.
    public static void Blast(Vector2 centre, float radius, float force)
    {
        if (radius <= 0f)
        {
            return;
        }

        var minX = Mathf.FloorToInt(centre.x - radius);
        var maxX = Mathf.FloorToInt(centre.x + radius);
        var minY = Mathf.FloorToInt(centre.y - radius);
        var maxY = Mathf.FloorToInt(centre.y + radius);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var tile = At(new Vector2Int(x, y));

                if (!tile)
                {
                    continue;
                }

                var offset = new Vector2(x + 0.5f, y + 0.5f) - centre;
                var distance = offset.magnitude;

                if (distance <= radius)
                {
                    tile.BlastedBy(centre, (radius - distance) * force);
                }
            }
        }
    }

    // Blown apart by a blast, if it is set to be and the blast is strong enough where it is. It either flies as a block or
    // breaks as it would from anything else, with the debris thrown away from the blast.
    private void BlastedBy(Vector2 centre, float strength)
    {
        var options = Options;

        if (destroyed || !options.dynamite || strength < options.dynamiteForceNeeded)
        {
            return;
        }

        var away = new Vector2(position.x + 0.5f, position.y + 0.5f) - centre;
        var direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;

        if (options.dynamiteResult == DynamiteResult.Fly)
        {
            Break(Vector2.zero, false, direction * strength);
        }
        else
        {
            Break(direction * Mathf.Min(strength, BlastDebrisSpeedLimit));
        }
    }

    // What shows when a blast knocks the tile loose as a block: the sound and effect, and the whole tile thrown as one
    // solid piece, rather than the four pieces of its debris.
    private void ShowBlock(Vector2 push, Color tint)
    {
        var centre = new Vector3(position.x + 0.5f, position.y + 0.5f, 0f);

        var options = Options;

        TileBreakEffects.Sound(options.breakSound, centre);

        if (options.breakEffect == BreakEffect.Particles)
        {
            TileBreakEffects.Particles(options.breakParticles, centre, tiles.Select(tile => tile.Item1));
        }

        var plans = new List<TileBreakEffects.DebrisPlan>();

        foreach (var (tilemap, _) in tiles)
        {
            if (TileBreakEffects.PlanDebris(tilemap, cellOn[tilemap], out var plan))
            {
                plan.color *= tint;
                plans.Add(plan);
            }
        }

        StartCoroutine(ThrowBlocksWhenClear(plans, options, push));
    }

    // As with debris, the block can't be made until the physics has taken the tile's solid part away.
    private IEnumerator ThrowBlocksWhenClear(List<TileBreakEffects.DebrisPlan> plans, DamageableTileOptions options, Vector2 push)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        foreach (var plan in plans)
        {
            TileBreakEffects.ThrowBlock(plan, options, push);
        }
    }
}