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
| **Movement Profile** | One per character: material, mass, traits, modes and overrides. | `Assets/Movement/Profiles/` |
| **LiquidMap** | A component on a liquid tilemap that gives the liquid's **density** and **viscosity** (thickness), both compared with water (water is 1 and 1). | on the tilemap |
| **Liquid Area** | An area of liquid in any shape (draggable points), with its own density and viscosity and an overlay. See [Liquids.md](Liquids.md). | on any object |

A character points at its profile with the **Movement Profile** field on its `Character` component.

Changes to any of these assets are kept even if you make them while the game is playing, so they can be tuned live.

## Setting it up

Do this once, with the scene open in the editor (it changes the scene through the editor, not by editing the scene file):

1. **Tools > Movement > Create Default Assets.** Makes the tuning, and 12 materials (Balloon, Foam, Cork, Fabric, Wood, Plastic,
   Rubber, Resin, Brick, Glass, Stone, Steel). It never overwrites anything that already exists,
   so it is safe to run again.
2. **Tools > Movement > Create Profiles For Characters.** For each character in the open scenes that has no profile, makes one
   named after the object, seeds it and assigns it. Save the scene afterwards.
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

There's nothing to create. A liquid is just a **Density**, a **Viscosity** and a **Min Kick Height** (the least a character's kick
there lifts it) on the thing that makes the liquid: a **Liquid Area**
(see [Liquids.md](Liquids.md)) or the **LiquidMap** on a liquid tilemap. Water is 1 and 1; oil is about 0.9 and 4; honey is far
thicker.

The profile Inspectors, the Overview and the Movement Lab show what each character does in two liquids, **Water** and **Oil**.
Those two are written in `Fluid.cs` (`Fluid.Presets`), and the Lab also tries any other liquid a tilemap in the scene has. To see
another liquid there, add it to the presets.

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

### How soft it is

A profile's **Softness** (under What it is) is how much the character's picture squashes and stretches as it moves and when it hits
something: 0 is hard (a brick or a bowling ball: not at all) and 1 is as soft as a balloon. Left at -1 it's what the class says (a
Balloon and a Bouncy Ball are 1, a Suction Cup 0.6, an Umbrella 0.3, the plain characters 0.15, the rest 0). It only changes the
picture, not the body, and the numbers for the softest are on the `ShapeFlex` component. See [Animation.md](Animation.md).

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
**Jump Cut** times as much gravity going up, so a short press makes a short jump. A kick or hop in a liquid is cut the same way: let
go early and it's shorter, hold and it goes its full height (so the liquid's Min Kick Height is for a held press).

**Jump Cut and the heavy-fall pull go by weight.** The extra pull going up after a let-go jump and the extra pull falling are on how much
the character weighs in what's round it. Held up by the air, as a balloon in its `float` mode is, it weighs little or nothing, so it
has little or none of them: a balloon isn't dragged down by Jump Cut as it rises. Something that rises because it's lighter than what's
round it goes up no faster than its **Terminal Speed**, the fastest it could fall. A profile can also set its own **Jump Cut** under
Overrides (1 is no cut).

**The Balloon** is set up with **Tools > Movement > Set Up Balloon Floating**: left alone he can't jump and falls at most 1.5 tiles a second
(Overrides: Jump Height 0, Fall Gravity 0.15, Terminal Speed 1.5), and holding the action (the `float` mode, density 0.1 of a balloon's)
he rises, up to 3 tiles a second (the mode's Terminal Speed 2 times that).

**His string and what hangs from it** (`BalloonString.cs`, `Balloon.cs`). The string is a rope worked out by script, not a chain of bodies with
joints. It's a line of points a fixed distance apart, hung from the bottom of him, with a line of a colour drawn through them, so it always
looks joined up however far they're pulled apart (like the Piton's rope). It has no bodies, joints or weight, so it can't stretch or go wild
with something heavy on it, and can't throw him about. It still drapes over the ground and piles up on it. It's made every time he starts, so
the pieces of the old string in the scene aren't needed (if they're there they're taken away). On the Balloon:

- **String Length** is how long it is, in units.
- **Segment Spacing** is how far apart the points are. Fewer, further apart, is a stiffer string; more, closer together, is a more flexible
  one that hangs and piles up like a real string, and costs a little more to work out.
- **String Color** and **String Width** are the line.

Another character takes hold of the end of it with Interact (and lets go with Interact), or he lets go of whatever is hanging from it with his
own Interact. And he can take hold of another character, or a crate (anything with a `CarryableObject`), with it: with Interact, with nothing
hanging and one within reach of the end of it, he hangs it from the string the same way, the nearer of the two if there are both (a character lets
go with Interact too). Only one thing hangs from it at a time: while it's holding a character it can't pick up a crate, and the other way round,
and nobody else can take hold of the string while there's a crate on it. A crate on it isn't carried: it keeps its own weight and its solid
colliders, and the string goes to it, as it does to a character, so it's the string that moves, never the crate. (He can't carry things any other
way.) Nobody else can pick up a crate that's hanging from the string.

What hangs from it is joined by a rope to the Balloon himself. While the string is slack the two don't affect each other at all. When it's pulled
tight they pull on each other, as heavy as each is:

- **Not using his action**, he's light, and is pulled around. A character standing on the ground can drag him along by walking, and one who
  jumps off a ledge and falls pulls him along the ground by their weight, until he's pulled off the ledge as well. How fast what hangs falls is
  slowed by him a little, less the heavier it is (his own slow fall, and **Heavy Fall Speed** for a very heavy load).
- **Using his action** (inflating), he holds it up. Up to his **Lift Capacity**, in mass, he bears what hangs from the string: he takes on that
  much mass, ramped up over a moment, and his lift is made to hold it, so the load is carried as if it weighed nothing to him. (He's a light
  body and the load may be many times heavier, which the physics doesn't manage well when only a force is used: the string would throw him about.)
  He only bears what's really hanging: how much of it counts goes by how tight the string is and how straight down from him it goes, so a load on
  the ground at the end of a string that runs sideways, or one that's tight with the load above him, counts for nothing, and it's taken up smoothly as
  he rises into place above it. What he and the load are going at is taken toward the speed they settle at
  a bit at a time (**Load Acceleration**), nothing instantly:
  - A load that falls and brings the string tight is slowed, its fall getting slower and slower, to nothing.
  - Then, if the load is lighter than his capacity, he lifts it, slowly: at his usual rising speed less the load's share of it, so a load of a
    quarter of his capacity is lifted at three quarters of the speed.
  - At exactly his capacity he has no strength to lift it: he can only hold it where it is, and it doesn't fall any further.
  - Over his capacity he sinks with it, faster the heavier the load, towards Heavy Fall Speed, but slowed by his lift.

As the string comes tight, any sideways speed it has given him, beyond his own walking and how what's hanging is walking, is settled away (**Settle Rate**),
and is never more than his top speed, so he comes to rest above them and doesn't swing about them. His own walking is left alone, so he can still
move to either side, and so is someone hanging who is walking (it's how they're trying to walk that counts, not how fast they're going, which the
string is part of, or he'd be pulled along by his own pulling and go faster and faster). With who's hanging off the ground the two move together, at no more than his
top speed, steered by his walking alone. Between the two, it goes by how high what hangs is, measured as clear space down from the bottom of it to
whatever is under it (not by what a character's feet say, which jump from standing to well off the ground in a step): below **Grounded Height** it's
the first, above **Lifted Height** it's the second, and between them the one gives way to the other, so his speed never changes all at once as
they leave the ground. (The tuning is the `SettleToLoad` method in `Balloon.cs`; **Settle Rate** is how quickly it settles.)

**Staying upright.** The Balloon's Rigidbody can be left free to turn (Freeze Rotation Z off), so the string and what he hits twist him. He's then
turned back upright by a spring (`RightHimself` in `Balloon.cs`): **Righting Strength** is how hard, **Righting Damping** how quickly the swinging
dies away (lower, and he swings back and forth past upright more times), and **Righting Reach** is the furthest from upright it counts as being, in
degrees, so that being upside down turns him over at a sensible speed and, unlike a plain spring, isn't a balance he can sit on. It sets nothing: it only
adds to how fast he's turning, so he never snaps to straight.

A crate hanging from the string counts the same as a character. A load counts for as much as the string is pulled tight, so one lying on the ground on a
slack string weighs nothing on him yet. His own Mass and the string don't change any of this.

Someone standing on the ground walking away with the rope tight drags the Balloon along. This is the rope pulling him, not a special case.

The rope puts itself right if part of it gets into the ground, which it can when it's made very long or he's moved to somewhere new. It's
laid out on the ground to start with, not through it; a piece that is inside the ground is put on top of it; and a piece further from the
top of the rope than the rope is long up to it, by a lot, ignores the ground until it's back in reach. It lies on whatever the Balloon himself is
stopped by, not only the Ground layer, so it goes over crates and platforms as well. A one-way platform stops it from above only.

His own Mass, and the string, don't change any of this.

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
- **Is checked hard when it dives in.** On top of Fluid Damping, the liquid slows something more the faster it's moving through it:
  `viscosity x (Fluid Damping x fractionUnder + Splash Drag x speed)`, and the speed part starts as soon as any of it is under. So a
  dive is slowed from the surface, hard at first and then less as it slows, and it's only a small part of the drag at a slow sinking
  or bobbing speed. Raise **Fluid Splash Drag** (default 0.8) to stop dives sooner, or lower it for a longer plunge. Floating crates
  are the same.
- **Sinks properly.** Something denser than the liquid never weighs less in it than **Min Sink Weight** (default 0.5) of its weight in
  the air. Without that, something only a little denser than water, such as plastic, is held up almost as much as it weighs and
  would sink at a crawl. Lighter things aren't affected, so floaters settle as before.
- **Jumps from something solid, or hops from the surface.** It can jump off the floor, or something standing on the floor (a crate,
  a platform). A liquid isn't a surface, so it can't jump off one in open water: in a liquid it can only jump from the bottom, as
  a **kick** of `Swim Kick x its jump height`, with the liquid's drag, but never less than the liquid's **Min Kick Height**
  (default 1.5 tiles, set on each Liquid Area or LiquidMap, so a deeper or thicker liquid can ask for more), so every character, including a weak jumper in a thick liquid and one that can't jump in the air at all, can at least jump
  off the bottom of a pool and not be stuck there. That minimum is for liquids only: the jump in the air isn't changed. The exception is a **floater at the surface**: something lighter
  than the liquid, with some of it under and the liquid ending at about the top of it (nothing more than 0.3 of a tile over it). It
  can **hop out**, with the same kick. Something that sinks, or a floater that has been pushed deep, can't hop.
  A kick is slower in a thicker liquid: it goes just as high but takes longer, as if gravity were weaker, so it's floaty. Its time
  is multiplied by `(1 + Kick Drag x viscosity) / (1 + Kick Drag)` (default Kick Drag 0.5), so water is as it is and a liquid four
  times as thick takes twice as long. Set Kick Drag to 0 for every liquid to kick at the same pace.
  A kick goes as high as it's worked out to: it's a clean arc up to its top. The liquid's drag is switched off while it's rising,
  its weight is fixed at the weight it had in the liquid when it kicked off (so it doesn't change as it comes out of the water, which
  would shorten the rest of the rise), and its speed is worked out for that weight. If the middle of the character is out of the
  liquid but its feet are in it, such as standing on the bottom of a shallow pool, it gets the kick it would have in the liquid
  (including the minimum) unless its jump in the air is better. When the kick reaches its top, the drag comes back, and it sinks
  as it normally does.
  The extra "snappy" jump and heavy-fall pulls are for the air only, and are off while any of the character is in a liquid, so a
  floater settles where its density puts it.

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
  walking isn't cut back to walking pace. That other speed wears off **on the ground** at one rate for every character, **Ground Friction** in the tuning (default 4 tiles/s
  every second), whatever its weight or traits, as friction does; a character standing with no input slows to a halt the same way.
  In the air it's kept, so jumps carry on. Because it's the same for all, a force such as a fan's, which gives acceleration of
  force / mass, moves a light character further than a heavy one. (Walking, and stopping when the stick is let go, still use each
  character's own acceleration and time to stop.) (Don't rely on friction or air damping for this: Air Damping defaults to 0, and some characters use a
  zero-friction material.) Speed against the walking direction is still
  overridden, which is how a character pushes crates.
- **Brushes.** Sideways speed that appears while a character is touching something at its side (a wall, the corner of a tile, a join
  between tiles) is dropped, not kept: it's the physics throwing the character off the edge, not a shove, and kept through a jump it
  would come back as a random burst of speed on landing. A contact counts if its surface is steeper than flat (see
  `SupportTracker.FlatEnough`), and for 0.1 s after. To catch one that gets through, tick **Log Speed Gains** on the character: it
  writes to the Console whenever speed appears that isn't walking or a push, saying whether it was dropped (a brush) or kept.
- **Walls.** When the wall detectors (or a kinematic wall touching the side) say a wall is ahead, the character's walking stops
  *and so does the speed that walking had given it*. If that speed were left, it would be read next step as other speed, kept
  through the air, and then added to the walking when the character cleared the wall, which doubled its speed. Only speed that
  wasn't walking stays.
- **Pushes** (a fan's wind, a blast) go through `Character.Push`, which keeps them as **momentum** separate from walking: the
  character's sideways speed is `walking + push`, so walking is added to a push and doesn't cancel it. A force adds to the push each
  step by force / mass, and on the ground Ground Friction takes it off at one rate for all (so a push that doesn't beat the friction
  doesn't move anyone). So a strong wind that blows a light character away is hard to walk into: the push keeps building as it
  gets nearer the fan, until the character can't make headway, and a heavy character, which the same force moves less, can walk
  into more of it. And a push in one direction has to be overcome, by it wearing off or by walking faster than it, before the
  character can go the other way. A wall or crate in the way, or a liquid's drag, takes speed off a push. Up and down is left to the
  physics. Anything else that pushes characters with a force should call `Character.Push(body, force, mode)` instead of
  `AddForce`, to get the same (`BlowerFan` and the Dynamite's blast do).
- Something that sets `moveSpeed` itself (a drill coasting, a brick going solid, a teleport) is noticed, and the walking drops what
  it had put in.
- **Riding.** A character standing on a **kinematic** body that moves sideways (a moving platform, a drawbridge, a door) is carried
  by it. This doesn't depend on friction, which some characters and floors don't have: `Character.RideSpeed` works out how fast the
  place under the character's feet is going from how far it moved in the last step, and that is added to the character's speed
  after the walking has been worked out without it. So the character walks about on a platform as it does on the ground, and a
  jump keeps the platform's speed. Stepping off onto other ground drops it; going off the edge into the air keeps it. Only sideways
  movement is carried: a platform going up pushes the character up as usual, and one going down faster than the character falls
  will leave it behind. A platform moved by its transform, with no kinematic Rigidbody2D, carries nobody. **Crates and other
  things the physics moves** are carried the same way by `PlatformRider`, which is added by itself to anything with a
  `CarryableObject` or a `PushableSolid`, or can be added by hand to any dynamic body. It adds the platform's sideways speed to the
  crate's own, so a crate can still be pushed along a platform. The measuring (what's under a body, and how fast it's moving) is
  shared, in `SupportTracker`. A character standing on a crate on a platform isn't carried by the platform itself, only by the
  crate, through friction.
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
- **Brushing something.** Speed he picks up while touching something at his side (a wall, the corner of a tile) isn't a shove, and isn't kept.
  Walking drops it by itself, because it sets his speed from the parts that are his. Standing still, in the air or on the ground, the
  speed that came from the brush is taken off. (Turn on **Log Speed Gains** on the character to see each time it happens.)
- **Corner forgiveness.** Clipping the corner of a ceiling with the edge of his head on the way up is where a character used to get caught
  or thrown sideways: a corner contact turns upward speed into sideways speed, by half of the speed at a 45 degree face (it's
  `speed x sin(2 x angle) / 2`, so every shape has some), and a flat top gives none of that but loses all the upward speed. So,
  each physics step before the move (`Character.ForgiveCeilingCorner`), if he's going up on his own at 1 unit a second or more (not
  just carried up by a platform or a pad, and not a small wobble) and a cast upward finds a ceiling in the way, and he'd be in
  something solid a step on, and would miss it by being moved sideways by at most **Corner Forgiveness** (world units, 0.15 by default, about a quarter of Paperclip's
  width), he's moved over by the smallest distance that clears it, the way he's steering or going first. He goes up at the speed he
  had, with nothing sideways added. More than that is a real ceiling and he bumps it as before. 0 turns it off. One-way platforms don't
  count, and neither do triggers. It's asked by putting him at each place and checking what he overlaps, so the collider's real shape, not
  its bounds, is what's judged. It only runs on the host.
- A piton without its head, or any character that can only walk some of the way, overrides `MaxWalkSpeed`.
- **Freezing** slows speed and jump through `Character.SetMovementScale`, whatever else the character is doing or in.
- `GetMovementSetting(mode, fluid)` fills in the character's current numbers every frame, so changes to a profile or the tuning
  show up straight away.

## Tools

| Menu | What it does |
|---|---|
| **Tools > Movement > Overview** | Every profile side by side: material, mass, traits (editable) and what they come to: speed, time to top speed, stopping distance, jump height and length, fall, and floats or sinks (and how far) in each liquid. Click a heading to sort. The columns grow to fit the window, headings wrap onto more lines rather than being cut off, and hovering over one says what it means. |
| **Tools > Movement > Create Default Assets** | Makes the tuning and materials that don't exist yet. |
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
test and removed at the end: a flat run, a drop, and a tank for each liquid (Water, Oil, and any other a tilemap has). They're tested all at once, each doing the same things
with the same scripted buttons:

| Test | What's measured |
|---|---|
| Run from standing, then let go | Top speed; time to get to it; how long and how far to stop |
| Jump, button held | How high, how long to the top, how long in the air |
| Jump, a tap | How high the shortest jump is |
| Jump at full speed | How far it jumps |
| Fall from 40 tiles | How fast it ends up falling |
| In each liquid: dropped in | Whether it floats or sinks, how much of it is under at rest, how many times it bobs, how long until it's still; for a sinker, how fast it sinks |
| In each liquid: a kick | How far one kick lifts it, from where it ends up. A floater at the surface hops with the same kick, so its figure is how high it hops. It's the height the kick reaches, from where it starts |
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
| A liquid does nothing | Its tilemap has no **LiquidMap**, or there's no **Liquid Area** there. Check there's a `Map` in the scene. |
| A character sinks too slowly | Something only a little denser than the liquid is held to **Min Sink Weight**: raise it. Lower **Fluid Damping** to sink faster. |
| A character sinks that should float | Its density is higher than the liquid's. Check the material, and the mode's **Density** multiple. |
| A floater sits too deep or too high | A material's density sets how far under it floats (about density / liquid density). Max Buoyancy Ratio also limits how high the lightest ones sit. |
| A floater leaps out of the water or jitters | Lower **Max Buoyancy Ratio**; raise **Fluid Damping**. |
| A character can't climb out of a liquid | A floater at the surface hops out with the same kick: raise its Jump trait or **Swim Kick**, or lower the liquid's viscosity. Something that sinks can only jump from the bottom: raise its Jump trait or **Swim Kick**, or lower the liquid's viscosity. The Inspector's liquid line shows what it can do. |
| A character feels slow to start after the change | The old values weren't carried over. Lower its **Time To Top Speed** via a trait or an override. |
| Pressed jump does nothing in the air | By design: it only jumps from something solid, or from the surface of a liquid it's floating in. |

## Where everything is

| File | Role |
|---|---|
| `Assets/Scripts/Player/Characters/Movement/MovementTuning.cs` | The formula's numbers. |
| `Assets/Scripts/Player/Characters/Movement/MovementProfile.cs` | Profile, modes, traits, overrides, and `Resolve` (the formula itself). Also `ResolvedMovement`, the result the movement code reads. |
| `Assets/Scripts/Player/Characters/Movement/CharacterMaterial.cs`, `Fluid.cs` | The material asset, and the liquid (a plain density and viscosity, not an asset). |
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
