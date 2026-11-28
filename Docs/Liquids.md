# Liquids

A body of liquid is either a **Liquid Area** or a liquid **tilemap**. Both give the liquid's **Density** and **Viscosity**, and anything
inside them responds to it. How a liquid affects a character (floating, sinking, drag, kicking off the bottom) is in [Movement.md](Movement.md); this is about
making the water.

## Liquid Areas

An area of liquid in any shape, drawn as an outline of points, the way a Polygon Collider 2D is.

### Making one

1. **GameObject > Liquid > Liquid Area** (or add the `LiquidArea` component to any object). If something is selected, it's made under
   it. Otherwise it's made at the middle of the scene view. It starts as a box.
2. Shape it in the scene view:
   - **Drag a point** to move it. Hold **Ctrl** to snap.
   - **Click the small dot in the middle of an edge** to add a point there, then drag it where you want it.
   - **Shift-click a point** to delete it. (There always have to be at least three.)
   - The points are also a list in the Inspector, if you'd rather type them, and **Reset To Box** puts it back to a box.
3. Set the liquid: **Density** and **Viscosity**, both compared with water, which is 1 and 1 (oil is about 0.9 and 4, honey
   is far thicker), and **Min Kick Height**: the least a kick or hop in the liquid lifts any character, in tiles (default 1.5).
   That's what guarantees a character can jump off the bottom and out, so a deeper or thicker liquid can ask for more. Then, if
   you want it drawn, an **Overlay**.

The outline can be any shape, bends inwards included, but it shouldn't cross itself. If it does, the Inspector says so, and the overlay
can't be drawn.

Moving, turning or scaling the object moves, turns or scales the area, so a body of water can ride a boat, a platform or anything else
it's a child of.

### More than one area

Each area is a body of its own, with its own density, viscosity and look. Where two overlap:

- The one with the higher **Priority** is in force.
- If the priorities are the same, the **smaller** area is. So a pool of oil drawn inside a lake needs nothing set: it's smaller, so it wins.
- An area over a liquid **tilemap** wins over the tilemap.

### The overlay

A picture drawn over the area, to show the water.

| Setting | What it does |
|---|---|
| **Overlay** | The sprite. Nothing is drawn if it's empty. |
| **Overlay Style** | **Stretch** fits the picture to the area's box. **Tile** repeats it across the whole area at its own size, so the surface is covered in copies of the sprite. It works with any sprite, a whole texture or one cut from a sprite sheet, and doesn't need the texture's Wrap Mode set to Repeat (it has a shader of its own, `Assets/Resources/LiquidSprite.shader`). |
| **Tile Scale** | Tile only: how big each copy is, compared with the sprite's own size. 1 is as big as the sprite, 2 is twice as big, 0.5 is half. |
| **Tint** | A colour the picture is tinted. White leaves it as it is. |
| **Transparency** | How see-through it is, as a percentage: **0 is solid, 100 is invisible**. |
| **Light Response** | How much the scene's 2D lights matter to the overlay, from 0 to 1. **1 (the default)** lights it as everything else is lit: it goes dark where it's dark (under the Global Light 2D that `DarkTileMap` sets) and is brightened by a light near it. **0** ignores the lights, so the water is always fully bright, as it was before. In between is a mix of the two: 0.5 is water that is dimmed in the dark, but not all the way. |
| **Sorting Layer Name** | The sorting layer the overlay is drawn on, "Default" unless you say. A light only lights things on its **Target Sorting Layers**, so the overlay has to be on one of them. Type the layer's name exactly. |
| **Sorting Order** | Which things it's drawn in front of. The default, 5, is in front of ordinary things, so a character in the water looks under it. |

The overlay's shader (`LiquidSprite.shader`) is made from URP's own 2D lit sprite shader, so it follows the 2D renderer's lights and
light blend styles, and the shadows from Shadow Caster 2Ds fall on it. It has no normal map, so it's lit flat. If the shader is missing, or has
a compile error, the Console says so once, and the overlay is drawn without light instead of turning pink.

Areas that overlap each draw their own overlay, so where they overlap the picture is less see-through. The overlay is made while
playing and in the editor; it's never saved in the scene, and doesn't show in the hierarchy.

### What responds

Everything asks `Map.FluidAt(point)` where a liquid is, and areas answer it as tilemaps do:

- **Characters**: float, sink and are slowed, and can kick off the bottom, as set by their movement profile. A liquid isn't something
  solid, so a character can't jump off it, with one exception: one that is **floating at the surface** can hop out. See [Movement.md](Movement.md).
- **Things the physics moves**, with a `FloatsInFluid` component, are held up by as much of them as is under the surface and slowed by
  the liquid's thickness, so they float and sink as characters do. It's added by itself to a **carryable crate**, as wood (density
  0.6), which floats. To change it, or to make anything else float, add `FloatsInFluid` by hand and set its Material or Density. A
  crate being carried isn't affected while it's held. It also **levels itself with the surface**: the
  liquid holds it up through the middle of what's under it, which is off to the side of its own middle when it's tipped (the low end is the wet one), so
  it turns it back toward level, as it would a raft, and rocking dies away by the liquid's thickness. So an end that's pushed down comes back up, and a
  light platform doesn't hang one end in the air. **Leveling** (1 is how a raft sits) sets how strongly; 0 leaves it at whatever angle it's pushed to. It needs
  a collider, and is ignored with Freeze Rotation Z. **A character standing on it weighs his mass.** Characters have
  gravity much heavier than the world's, so that they fall and jump well, and all of it presses on what they stand on (a character of 30 might press like 250), which
  would sink a raft far deeper than he weighs. So in a liquid, all of that but his mass is taken back off it, as an upward force where he stands
  (**Character Weighs Mass**, on by default). The same is done for platforms held up by floating objects ([Floating.md](Floating.md)).

## Tide: a surface that rises and falls by itself

A Liquid Area can slowly rise and fall on its own, by a small percentage, so the water is never perfectly still. It uses the same
level as the switch (below), and works with or without one.

| Setting | What it does |
|---|---|
| **Tide Range** | How far the surface drops below the top, as a percentage of how full the area is. 5 means it rises and falls within the top 5 percent. 0 (the default) keeps it still. The area as drawn is the top, so it never goes above it. |
| **Tide Period** | How long one whole rise and fall takes, in seconds (default 20). |
| **Tide Randomness** | 0 is a smooth, regular rise and fall; 1 drifts about with no pattern. |
| **Tide Phase** | Where in its cycle it starts, 0 to 1, so areas next to each other aren't in step. |

- The tide takes a percentage **of the current level**, so a drained area (level 0) stays empty, and a half-full one moves by half as much.
- It works out from the time, with no per-frame work, and in a network game from the time every machine shares, so the host and the others agree
  on where the surface is. Everything in the liquid responds as it moves, as it does for a switch.
- In the scene view, with the area selected, a faint line across it marks the lowest the tide takes the surface.

## Waves: a surface that isn't a straight line

A Liquid Area can have a **wavy or choppy surface** instead of a straight line. The waves travel along it, and the overlay's top edge
follows them.

| Setting | What it does |
|---|---|
| **Wave Height** | How high the waves are, from trough to crest, in units. 0 (the default) keeps the surface flat. |
| **Wave Length** | The distance from one crest to the next, in units. |
| **Wave Speed** | How fast the waves travel along the surface, in units a second. Positive goes right, negative goes left, 0 holds them still. |
| **Choppiness** | 0 is one smooth wave. Up to 1 adds smaller, faster waves going both ways on top of it, so it's rough and never the same twice. |
| **Waves Move Things** | On (the default): what's liquid follows the waves, so a floating crate or a character at the surface rides them. Off: the waves are only for show, and the liquid is as if the surface were flat, half way down them. |

- **The crests reach the level of the water**, and the troughs are the Wave Height below it. So with the area full, the crests touch the
  top of the outline, as with the tide: the area as drawn is the highest the surface goes.
- The waves go by **where they are in the world**, not in the area, so areas side by side have waves that carry on across the join.
- They're worked out from the shared time (the network's, in a network game), so every machine sees the same wave in the same place.
- Waves go with the **tide** and a **switch**: they ride the level, whatever it is.
- In the scene view, with the area selected, the waves are drawn as they are now across the area. Press play to see them travel.
- A very choppy surface with big waves moves things around a lot. For something calm, keep the Wave Height small (a quarter of a tile or
  less) and the Wave Length long.

## Raising and lowering the liquid

A **Switch Liquid** (a switchable object, so a lever or button works it, as they do a door) raises and lowers the liquid in a Liquid Area.
Put it on the Liquid Area's object, or give it the area in the **Area** field.

The liquid is the part of the area **below a level surface**, and the surface is a fraction of the way up the area's height, from the
lowest point of its outline to the highest, in the world, so it's level however the area is turned. The area as drawn is **100%**.

| Setting | What it does |
|---|---|
| **On Level** | How full the area is when this is switched **on**, 0 to 1. Default 1: the area as it is. |
| **Off Level** | How full it is when switched **off**, 0 to 1. Default 0: empty, so the liquid sinks from the top of the area to the bottom. The game starts here. |
| **Rise Speed** | How fast the surface rises, in units a second. |
| **Lower Speed** | How fast it sinks. 0 is the same as Rise Speed. |

- For a pool that is full to begin with and **drains** when the switch is on, swap the levels: On Level 0, Off Level 1.
- A button set to **Allow Partial Presses** sets the level by how far it is pressed, between Off Level and On Level. A lever or a button
  that stays active goes between the two.
- **Stay Switched On** and **Switch Off Time**, on the Switch Liquid, work as they do on a door.
- The overlay is cut by the surface, and its picture stays where it is on the area, so lowering uncovers it rather than squashing it.
- Whatever is in the liquid responds as it goes: what's liquid at a point changes with the level, so characters sink and crates drop as
  it drains, and float up as it fills.
- In the scene view, with it selected, the surface is drawn across the area at each level, blue for on and white for off.
- In a network game the host does it, and sends how full it is (`LiquidLevelNetwork`, added by **Tools > Network > Make World Networked**).

## Liquid tilemaps

The older way: a tilemap with a `LiquidMap` component, which has the same **Density**, **Viscosity** and **Min Kick Height**. Everything on a tile of it is in that liquid. It still
works, and both can be used together in the same scene.

## Things to know

- **The scene needs a `Map`** for liquids to be found, as for tilemaps.
- The surface of real water is flat, but nothing makes an area's top edge level: a slanted top edge is a slanted surface. Characters
  and crates just feel whichever liquid they're in at that spot.
- **Other machines in a network game** work out their own liquids from the same scene: nothing about an area is sent, so an area that
  moves (on a boat, say) has to be moved by something that is itself sent.
- The Movement Lab's tanks are still tilemaps. It doesn't use areas.
