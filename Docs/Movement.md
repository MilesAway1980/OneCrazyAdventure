# Character movement

How a character runs, jumps, falls and floats is worked out from **what it is**: what it's made of, and how much it weighs. A few
shared assets say how that turns into numbers, so changing one value can change every character at once, and adding a character
or a liquid is a matter of making an asset rather than tuning a dozen fields by hand in every scene.

This replaced the old `MovementSettings` components, which were copied onto every character in every scene. Nothing reads those
any more.

## Contents

1. [The pieces](#the-pieces)
2. [Setting it up](#setting-it-up)
3. [Everyday jobs](#everyday-jobs) (add a character, a liquid or a material; tune)
4. [How the numbers are worked out](#how-the-numbers-are-worked-out)
5. [Modes](#modes)
6. [Liquids and buoyancy](#liquids-and-buoyancy)
7. [Walking and jumping in the code](#walking-and-jumping-in-the-code)
8. [Tools](#tools)
9. [The Movement Lab](#the-movement-lab)
10. [Troubleshooting](#troubleshooting)
11. [Where everything is](#where-everything-is)
12. [Not built yet](#not-built-yet)

## The pieces

| Asset | What it is | Where it lives |
|---|---|---|
| **Movement Tuning** | The formula: reference speeds, jump height and times, how weight changes them, and how liquids behave. There is only one. | `Assets/Resources/MovementTuning.asset` |
| **Character Material** | What a character is made of. Its only setting is **density**, compared with water (water is 1). | `Assets/Movement/Materials/` |
| **Fluid** | A liquid: **density** and **viscosity** (thickness), both compared with water (water is 1). | `Assets/Resources/Fluids/` (Water must stay here) |
| **Movement Profile** | One per character: material, mass, traits, modes and overrides. | `Assets/Movement/Profiles/` |
| **LiquidMap** | A component on a liquid tilemap that says which Fluid it is. Empty means Water. | on the tilemap |

A character points at its profile with the **Movement Profile** field on its `Character` component.

Changes to any of these assets are kept even if you make them while the game is playing, so they can be tuned live.

## Setting it up

Do this once, with the scene open in the editor (it changes the scene through the editor, not by editing the scene file):

1. **Tools > Movement > Create Default Assets.** Makes the tuning, 12 materials (Balloon, Foam, Cork, Fabric, Wood, Plastic,
   Rubber, Resin, Brick, Glass, Stone, Steel) and two fluids (Water and Oil). It never overwrites anything that already exists,
   so it is safe to run again.
2. **Tools > Movement > Create Profiles For Characters.** For each character in the open scenes that has no profile, makes one
   named after the object, seeds it and assigns it. It also sets any LiquidMap with no Fluid to Water. Save the scene afterwards.
3. Optional: **Tools > Movement > Remove Old Movement Settings** takes the old per-character components off.

The seeds are starting points, not decisions: mass is the character's current Rigidbody mass, and the material and modes are
guessed from its class (Balloon, BouncyBall, BowlingBall, Umbrella, Brick, Drill, Magnet, Piton, Pebble, Bulb, Ladder, Torch and
SuctionCup have particular guesses; anything else is Plastic, or Wood if its name contains "pencil" or "wood").

## Everyday jobs

### Add a new character

The Overview lists every Movement Profile in the project, so a character appears once it has one. Either:

- **Automatically:** open the scene and run **Tools > Movement > Create Profiles For Characters**. It only touches characters
  that have no profile, so it won't disturb the others.
- **By hand:** **Assets > Create > Movement > Profile**, set the material and mass, then drag it onto the character's
  **Movement Profile** field.

If the new character moves differently while it does something (floats, rolls, inflates), give its class a mode:

1. In the class, override `GetMovementMode()` to return the mode's name (see `Balloon.cs`, `BowlingBall.cs`).
2. In the profile, add a mode with that name under **Modes** and set its multipliers (see [Modes](#modes)).

A character with no profile logs an error and is given a plain one, so it still plays. Click **Refresh** in the Overview if a new
profile isn't listed.

### Add a liquid

1. **Assets > Create > Movement > Fluid.** Set **Density** and **Viscosity** (water is 1 and 1; the seeded Oil is 0.9 and 4).
2. Select the liquid tilemap, and drag the Fluid into the **Fluid** field of its **LiquidMap** component.

Every profile Inspector and the Overview find all Fluids automatically, so the new liquid gets its own line and column showing
what it does to each character. The file can go anywhere, except that **Water must stay in `Assets/Resources/Fluids`**, because
that's where the default is loaded from.

### Add or change a material

- **Change one:** select it in `Assets/Movement/Materials` and change **Density**. Every character made of it changes.
- **Add one:** **Assets > Create > Movement > Material**, set the density. It shows up in a profile's **Material** field and, after
  a refresh, in the Overview's dropdown.

Density is the material's only effect. It decides floating and sinking: lighter than the liquid floats, heavier sinks. It does
not change mass or speed (those come from the profile's **Mass**), so a heavy foam is possible. Rough guide, water being 1:

| Material | Density | | Material | Density |
|---|---|---|---|---|
| Balloon | 0.004 | | Rubber | 1.15 |
| Foam | 0.1 | | Resin | 1.3 |
| Cork | 0.25 | | Brick | 1.9 |
| Fabric | 0.4 | | Glass | 2.5 |
| Wood | 0.6 | | Stone | 2.7 |
| Plastic | 1.05 | | Steel | 7.8 |

### Tune how a character feels

Work from the general to the particular:

1. **The whole game:** change **Movement Tuning**. One value moves every character that goes by the formula.
2. **One character, a little:** use the **traits** (Run Speed, Acceleration, Jump, Floatiness), each a multiple of what its weight
   says. The Overview lets you edit these along with mass and material, in a table, which is the fastest way to compare.
3. **One character, exactly:** use **Overrides** in its profile. Anything left at **-1** is worked out; anything else is used as
   given.
4. **While it does something:** use a **mode** (see below).

The profile Inspector shows what the numbers come to in words (top speed, how long to get going and stop, how high and far it
jumps, how it falls) and draws the jump arc, so you can judge a change without playing. Tune a character in the Overview, with
the game running, and the change is live.

## How the numbers are worked out

For a character of mass *m*, with `heavy = m / Reference Mass`:

| Result | Formula (before modes and liquids) |
|---|---|
| Top speed (tiles/s) | `Top Speed x heavy^-speedExponent x Run Speed trait` |
| Time to top speed (s) | `Time To Top Speed x heavy^accelerationExponent / Acceleration trait` |
| Time to stop (s) | `Time To Stop x heavy^brakingExponent / Acceleration trait` |
| Jump height (tiles) | `Jump Height x heavy^-jumpExponent x Jump trait` |
| Fall gravity (x normal) | `Fall Gravity x heavy^fallExponent / Floatiness trait` |
| Terminal speed (tiles/s) | `Terminal Speed x heavy^terminalExponent` |
| Climb speed | `Climb Speed x Run Speed trait` |

The exponents (Tuning > "How weight changes it") say how much weight matters: 0 means none, higher means more. Heavy characters
run slower, get going and stop slower, jump lower, and fall harder. The defaults are 0.15 for speed, 0.4 for getting going and
stopping, 0.25 for jumping, 0.15 for falling and 0.2 for terminal speed.

**Jumping is a height, not a force.** The take-off speed is worked out from the height and the pull of gravity going up:
`speed = sqrt(2 x gravity x riseGravity x height)`. With the button held the jump reaches the height; letting go early applies
**Jump Cut** times as much gravity going up, so a short press makes a short jump.

**Gravity** on a character is the world's gravity times **Character Gravity** (default 2). **Air Damping** defaults to 0, so a jump
goes as high as the numbers say.

The speed-up and slow-down values the movement code uses are the top speed divided by the times above, per 0.02 s physics step.

## Modes

A **mode** is a set of multiples applied while a character is doing something. All default to 1 (no change):

| Field | Effect |
|---|---|
| Speed | Top speed |
| Acceleration | Over 1 gets going faster |
| Braking | Over 1 stops faster |
| Jump Height | Jump height |
| Rise Gravity | Pull going up. A big number kills an upward speed almost at once |
| Fall Gravity | Pull falling. A small number is a slow fall |
| Terminal Speed | The fastest it can fall |
| Density | How dense it is while this is going on. A small number is something filled with air, which floats |

The built-in characters use these mode names (a class picks one in `GetMovementMode()`; anything else is `base`):

| Character | Modes |
|---|---|
| Balloon | `float` (while the action button is held) |
| BouncyBall | `bouncy` (inflated), `charging` (squashed, charging a jump) |
| BowlingBall | `rolling` |
| Umbrella | `float` (open, in the air), `updraft` (open and being lifted by wind) |

There are no separate liquid versions of a mode: a mode in a liquid just gets the liquid's effects on top.

## Liquids and buoyancy

Whether something floats is its **density against the liquid's**. A character in a liquid:

- **Is held up** by as much of it as is under. Something lighter rises until only as much is under as it weighs, then settles at
  the surface, bobbing. Something heavier sinks. This is done by changing the character's gravity every physics step:
  `gravity x (1 - lift)`, where `lift = (fluidDensity x fractionUnder + airDensity x fractionAbove) / characterDensity`.
- **Is slowed** by the liquid's viscosity: walking and swimming speed drops by `1 / (1 + Swim Drag x viscosity)`, and the liquid
  damps its movement, which also settles the bobbing. A higher **Fluid Damping** means fewer bobs.
- **Jumps as a kick** of `Swim Kick x its jump height`, with the liquid's drag. It can kick from nothing while 30% or more of it is
  under, which is how a sinker swims up and a floater climbs out. The extra "snappy" jump and heavy-fall pulls are for the air
  only, and are off while any of the character is in a liquid, so a floater settles where its density puts it.

**Max Buoyancy Ratio** (default 3) is the most a liquid can lift something, as a multiple of its weight. A lighter thing than that
would rise too hard and leap out, so it floats a little deeper instead (with the default, at least a third of it is under).

Air is treated the same way with its own density (**Air Density**), which is why a Balloon in its `float` mode, being lighter than
air, rises.

How much of a character is under is sampled at five points up its solid colliders. `inLiquid` (used by the Torch, the Flashlight
and the Umbrella) is still whether its **middle** is in a liquid.

## Walking and jumping in the code

- **Walking** is `Character.ApplyWalking`, once per physics step. `Move()` only records which way the player is steering. Speeding
  up and slowing down are per 0.02 s step, scaled to the real step length, so they don't change with the frame rate.
- Walking is **added** to the speed a character already has, so a shove, a force switch or a moving platform in the direction it's
  walking isn't cut back to walking pace. That other speed wears off **on the ground**, at the rate the character can stop itself
  (its time to stop), as friction would; a character standing with no input slows to a halt the same way. In the air it's kept,
  so jumps carry on. (Don't rely on friction or air damping for this: Air Damping defaults to 0, and some characters use a
  zero-friction material.) Speed against the walking direction is still
  overridden, which is how a character pushes crates.
- Something that sets `moveSpeed` itself (a drill coasting, a brick going solid, a teleport) is noticed, and the walking drops what
  it had put in.
- **Riding.** A character standing on a **kinematic** body that moves sideways (a moving platform, a drawbridge, a door) is carried
  by it. This doesn't depend on friction, which some characters and floors don't have: `Character.RideSpeed` works out how fast the
  place under the character's feet is going from how far it moved in the last step, and that is added to the character's speed
  after the walking has been worked out without it. So the character walks about on a platform as it does on the ground, and a
  jump keeps the platform's speed. Stepping off onto other ground drops it; going off the edge into the air keeps it. Only sideways
  movement is carried: a platform going up pushes the character up as usual, and one going down faster than the character falls
  will leave it behind. A platform moved by its transform, with no kinematic Rigidbody2D, carries nobody. Things that aren't
  characters, such as a crate, aren't carried this way; they rely on friction.
- **Standing on things that aren't "Ground".** The floor detectors only look for the **Ground** layer, so a platform, crate or
  bridge on another layer would look like thin air: no jumping, and the jump picture. Instead `Character.FindSupport` also counts
  touching anything solid on a flat enough surface (about 45 degrees or flatter) under the character as standing on the ground, on
  any layer. That's what decides whether it can jump. On something that's going up, the character can jump when it isn't rising
  faster than the thing is. Platforms don't need to be on the Ground layer, which has other effects (enemies treat things on it
  as walls).
- **Walls.** A character stops pushing into a wall so it can't cling to it (pressing into one hard enough, friction holds a character
  there against gravity). The detectors find walls on the **Ground** layer ahead of time; a wall that's a **kinematic body** on
  another layer, such as a drawbridge standing up, a platform's side or a door, is found when the character touches it
  (`FindSupport`). Tile walls aren't judged by touch, as the joins between tiles make contacts that aren't walls.
- A piton without its head, or any character that can only walk some of the way, overrides `MaxWalkSpeed`.
- **Freezing** slows speed and jump through `Character.SetMovementScale`, whatever else the character is doing or in.
- `GetMovementSetting(mode, fluid)` fills in the character's current numbers every frame, so changes to a profile or the tuning
  show up straight away.

## Tools

| Menu | What it does |
|---|---|
| **Tools > Movement > Overview** | Every profile side by side: material, mass, traits (editable) and what they come to: speed, time to top speed, stopping distance, jump height and length, fall, and floats or sinks (and how far) in each liquid. Click a heading to sort. |
| **Tools > Movement > Create Default Assets** | Makes the tuning, materials and fluids that don't exist yet. |
| **Tools > Movement > Create Profiles For Characters** | Makes and assigns a profile for each character in the open scenes that has none. |
| **Tools > Movement > Remove Old Movement Settings** | Takes the old `MovementSettings` components off. Ones that are part of a prefab are left and reported. |
| **Tools > Movement > Movement Lab** | Runs every character through the same tests with the real physics and shows what it measured next to what the formula said. See below. |
| A profile's Inspector | The derived numbers in words, the jump arc, each mode's summary, and what each liquid does. |

## The Movement Lab

The Overview's numbers are worked out from the formula. The Lab **measures**: it runs the game's own physics on the real
characters, so it shows where the formula and the real thing disagree (a character's shape, its friction, a script of its own that
changes how it moves) and what a liquid really does to each one.

**To run it:** open the scene whose characters you want tested (it needs a `Map`, and every character it finds with a profile is
tested), then **Tools > Movement > Movement Lab > Run the Lab in the open scene**. It starts the game, runs by itself for a couple
of minutes, stops the game and puts everything back as it was. Don't join with a keyboard or gamepad while it runs; if a player has
joined, it stops and says so. The results are in the window, and in `Logs/MovementLab.json` and `Logs/MovementLab.csv` (one row for
each thing measured, for a spreadsheet).

**What it does.** Each character is taken to a lane of its own, high above the level at (2000, 2000) and up, which is made for the
test and removed at the end: a flat run, a drop, and a tank for each Fluid. They're tested all at once, each doing the same things
with the same scripted buttons:

| Test | What's measured |
|---|---|
| Run from standing, then let go | Top speed; time to get to it; how long and how far to stop |
| Jump, button held | How high, how long to the top, how long in the air |
| Jump, a tap | How high the shortest jump is |
| Jump at full speed | How far it jumps |
| Fall from 40 tiles | How fast it ends up falling |
| In each liquid: dropped in | Whether it floats or sinks, how much of it is under at rest, how many times it bobs, how long until it's still; for a sinker, how fast it sinks |
| In each liquid: a kick | How far one kick lifts it |
| In each liquid: getting out | Steering toward a bank and kicking from the bottom, whether it gets out onto the bank (default 1 tile above the water) and how long it takes |

A character whose action button changes how it moves (the Balloon, Umbrella and BowlingBall: the `actionPassTypes` list on the
`MovementLab` component) is tested a second time holding it, and shown with its mode's name.

**Reading the table.** Each number is what was measured, with what the formula said in brackets. Green is within 15%, orange within
30%, red further. A red cell is a prompt to look, not necessarily a fault: the formula ignores a character's collision shape, floor
friction and any scripts of its own. "didn't run" means it never moved sideways (a character that can't walk, or one that something
held still). "stuck" in the liquids table means it never got out onto the bank: tune its Jump trait, **Swim Kick**, the liquid's
viscosity, or the bank height.

**Settings** (in the window; also on the `MovementLab` component if you add it to a scene yourself, with **Run On Start** ticked):
the speed it runs at (keep it at 1 for the most exact results) and the height of the bank.

**Notes.** The jump cooldown now uses game time, not real time, so the Lab gives the same results at any speed. The Lab
touches the scene only while it runs: it moves the characters, brings the ones that were put away into the game, and sets them back.

## Troubleshooting

| Seen | Likely cause |
|---|---|
| "has no Movement Profile" in the Console | The character has no profile. Run **Create Profiles For Characters** or assign one. |
| "no MovementTuning in a Resources folder" | Run **Create Default Assets**. |
| A liquid does nothing | Its tilemap has no **LiquidMap**, or the Fluid is empty (that means Water; check the Overview shows the liquid). |
| A character sinks that should float | Its density is higher than the liquid's. Check the material, and the mode's **Density** multiple. |
| A floater sits too deep or too high | A material's density sets how far under it floats (about density / liquid density). Max Buoyancy Ratio also limits how high the lightest ones sit. |
| A floater leaps out of the water or jitters | Lower **Max Buoyancy Ratio**; raise **Fluid Damping**. |
| A character can't climb out of a liquid | Raise its Jump trait, **Swim Kick**, or lower the liquid's viscosity. The Overview's "kick" column and the Inspector's liquid line show what it can do. |
| A character feels slow to start after the change | The old values weren't carried over. Lower its **Time To Top Speed** via a trait or an override. |
| Pressed jump does nothing in the air | By design: it only jumps from the ground or when 30% or more is in a liquid. |

## Where everything is

| File | Role |
|---|---|
| `Assets/Scripts/Player/Characters/Movement/MovementTuning.cs` | The formula's numbers. |
| `Assets/Scripts/Player/Characters/Movement/MovementProfile.cs` | Profile, modes, traits, overrides, and `Resolve` (the formula itself). Also `ResolvedMovement`, the result the movement code reads. |
| `Assets/Scripts/Player/Characters/Movement/CharacterMaterial.cs`, `Fluid.cs` | The material and liquid assets. |
| `Assets/Scripts/Player/Characters/Assets/Character.cs` | Walking (`ApplyWalking`), buoyancy (`ApplyBuoyancy`, `SampleSubmersion`), jump, `GetMovementSetting`, `GetMovementMode`. |
| `Assets/Scripts/World/Map/LiquidMap.cs`, `Map.cs` (`FluidAt`) | Which liquid is where. |
| `Assets/Scripts/Editor/MovementSetupMenu.cs`, `MovementOverviewWindow.cs`, `MovementProfileEditor.cs` | The menu, the Overview table and the profile Inspector. The seeds for each character class are in `Seed` in the menu file. |
| `Assets/Scripts/Player/Characters/Movement/Lab/MovementLab.cs` | The Lab: builds the lanes, runs the tests (one `Lane` per character, a step at a time), restores the scene. |
| `Assets/Scripts/Player/Characters/Movement/Lab/MovementLabReport.cs` | What it found: the JSON and CSV, and the data classes. |
| `Assets/Scripts/Editor/MovementLabWindow.cs` | The window that starts it and shows the results. |
| `Assets/Scripts/Player/Characters/Assets/MovementSettings.cs` | The old component, kept only so old scenes don't show missing scripts. |

## Not built yet

Ideas that were suggested and not made: a tuning panel inside the game; a spreadsheet import (the Lab already exports a CSV); a
view that overlays several characters' jump arcs on one graph. The Overview's numbers are worked out from the formula, not
measured from the physics (the Lab is the measured version), so they ignore things like a character's collision shape or
friction. The Lab doesn't yet test a character's special abilities, climbing, or carrying.
