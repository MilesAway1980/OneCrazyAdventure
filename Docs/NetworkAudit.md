# Network audit: what each script needs

For playing over a network the host runs the game and every other machine only shows it. This lists every script outside the
characters, the cameras and the networking itself, and what it needs for that. See [WorldMap.md](WorldMap.md) for the game's design.

Read from the code on 2026-11-16. Where a script wasn't read in full, it's marked "(skimmed)". Nothing here is tested.

## The kinds

| Kind | Means | What it takes |
|---|---|---|
| **A. Moves** | Its position changes during play | A Network Identity and a position sync (the same two components for every one), and its movement logic running on the host only. |
| **B. Shows state** | Something about it can be seen, other than where it is | A small script that sends that state, about 30 lines (like the lever and the pickup). |
| **C. Logic only** | It decides things on the host and nothing needs to be seen | Nothing, as long as it can't also run on the joined machine and do something different there. |
| **D. Local only** | Screen, controls or decoration, one per machine | Nothing, unless the host has to tell it something (an event). |
| **E. Events or spawning** | Things appear, break or are made while playing | Needs spawning or a message from the host, and a record for anyone who joins late. |

A script can be more than one kind. "Guard" below means it also needs the host-only check (`GameSession.IsClientOnly`), or the
global fix described in the last section.

## Done

| Script | Kind | How |
|---|---|---|
| Characters (position, facing, in the game) | A, B | `CharacterNetwork`, position sync |
| Flashlight, Bulb | B | `FlashlightNetwork`, `BulbNetwork` |
| `SwitchDoor` | A | position sync, guard |
| `ButtonSwitch` | A (the pad) | position sync of the pad, guard |
| `LeverSwitch` | B | `LeverNetwork`, guard |
| `CharacterPickup` | B | `PickupNetwork`; the roster of found characters rides on `ConnectionPlayer` |
| `LightSocket` | C | host only; the Bulb sitting in it is already shown |
| `HiddenTileSection` | D | runs on every machine from the flashlight beams, which are sent |
| `DamageableTile`, `DamageableTileSection` | (see E) | now independent of where the tilemap is; nothing is networked yet |

## Moving things (A)

Each is a generic position sync, plus a guard for the logic.

| Script | Notes |
|---|---|
| `CarryableObject` (crates) | Position sync. The joint to the carrier exists only on the host, which is fine. Needs the carrier's seat to be correct on every machine so it's shown being carried. |
| `PushableSolid` (a marker on pushable bodies such as the Brick) | Position sync on the body it marks. |
| `FallingTileSection` | The platform moves by a spring on the host; position sync. Its tiles move with it. Hidden tiles on it already work. |
| `RotatingCenterPlatform` | Rotation as well as position. Its Update looks odd (it sets the platforms' rotation from the centre's `rotation.z`); worth checking before networking. |
| `FollowPath`, `FollowPathTarget` | Moves by its transform. Guard. `FollowPath` prints to the Console every frame; remove that. |
| `MagnetBox` | Moves by a fixed step each frame, so it depends on frame rate. Guard, and better made to use time. |
| `StickToPlatform` | Carries riders by the platform's change in position. Host only. |
| `SwitchForce` | Pushes its own body while switched on. Guard and position sync. |
| `GrabPoint` | Joins a character to it on the host, so everyone sees only the positions. Host only. |
| `ChainLinker` (skimmed) | Pulls links back that stretch too far. Each link is a body, so a chain is many identities; decide whether chains are decoration (local, not synced) or matter. |
| Enemies: `Enemy`, `EnemyMovement`, `EnemySideToSide`, `EnemyPathFollow`, `EnemyGroundPath`, `EnemyCrawler`, and the path-finders (skimmed) | All the thinking stays on the host and each enemy is position-synced. Also B for the sprite's facing (`Enemy` flips it by speed). About 4,000 lines, but none of it changes: it only needs a guard so that it doesn't run on the joined machine. |

## Shows state (B)

| Script | Notes |
|---|---|
| `Sconce` and any `LightTileSource` that's not on a character | Lit or unlit, and the boost. One sender covers them all. |
| `SwitchBrighten`, `DarkTileMap` | The scene's ambient colours change when a switch is on. Send the two colours. The slow pulsing is decoration that can run locally. |
| `CharacterHub` | Which hubs have been reached, which is the checkpoint, and the flag (`reachedVisual`). |
| Health: `DamageableEntity` (skimmed) | Hit points and death. A character who dies is returned by the host, so the movement is seen; send hit points only if something shows them. |
| `CharacterFreeze`, `IceZone` (skimmed) | Freezing slows a character on the host. Check whether anything visible changes. |

## Logic only (C)

`KillZone` (host teleports, which is already shown), `HeatSource`, `IceZone`'s effect, `RollDamage`, `CanTriggerFloorCollapse`, the
`BlowerFan` forces (its fan picture animation is local decoration, D), `Map` and its lookups, `LiquidMap`, `Detector`s on
characters, `WindCatcher`.

## Local only (D)

`GameMessages`, `HubMenu` (see below), `TutorialPrompt` (see below), `PlayerPrompts`, `ScreenMeter`, `ParallaxBackground`,
`GlobalLightSettings`, `GameText`, the camera code, the map editor and every editor tool.

## Needs events or spawning (E)

| Script | Notes |
|---|---|
| `DamageableTile`, `DamageableTileSection`, `TileBreakEffects`, `TileDebris` | Which tiles are broken, and when, as events from the host, plus a list for anyone who joins late. The break effects and debris are made by each machine from the event. Only fixed tiles are supported (see "moving tiles" below). |
| `SwitchRopeBridgeBuilder` | Makes the links of a bridge one at a time while playing, each a body with joints. Hard: either the host spawns each link as a networked object, or each machine builds the bridge itself and only the result is checked. Decide when we get here. |
| Effects made by a character's ability (flames, dynamite, thrown things) | Per character, as we go. |

## Things that are neither of those, and need a decision

- **`HubMenu` and joined players.** The menu belongs to a `Player` on the host's own machine, so a joined player can't open it. It
  needs the controls routed to the host (or a menu run on the joined machine that sends choices), and the roster and party on the
  joined machine, which is already copied.
- **`TutorialPrompt`.** It shows hints when a player's character is inside its area, found by a trigger. A joined machine's
  characters have no colliders, so nothing is ever found there. It should test the position of each local player's character
  against the area.
- **`GameMessages` from the host.** Anything the host announces ("found a character", "sent back to the hub") shows on the host's
  screen only. Each is done by hand now (the pickup). A single "show this on every machine" message from the host would cover
  them all.
- **Late joiners.** Anything that has changed since the game began must be sent to someone who joins afterwards: tiles broken,
  hubs reached, characters found (done), bridges built.

## One fix that could remove most of the guards

On a joined machine nothing is simulated by physics, only moved where the host says. So the joined machine could switch its
physics off altogether, with `Physics2D.simulationMode = SimulationMode2D.Script`. Then triggers don't fire, bodies don't fall,
forces don't push and joints don't pull. Almost every "guard" above is only there to stop that. What's left to guard is logic that
runs on timers and by transform, such as `FollowPath`, `MagnetBox` and the enemies' thinking.

To test before relying on it: that the positions sent by the host still move everything (they set transforms, so they should),
that the joined machine's own queries (raycasts) are still right where used (the code that needs them is on the characters, which
are only shown there), and that nothing visual relies on physics running. If it works, the audit's "guard" column shrinks to a
handful.

## Moving tiles

Breaking tiles on a tilemap that moves isn't supported and isn't planned (see the notes on `FallingTileSection`). Hidden tiles on one
are.
