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
| **Overlay Style** | **Stretch** fits the picture to the area's box. **Tile** repeats it across the area at its own size. For Tile, the picture's texture needs its **Wrap Mode** set to **Repeat** in its import settings, and the sprite should be the whole texture. The console says so if it isn't. |
| **Tint** | A colour the picture is tinted. White leaves it as it is. |
| **Transparency** | How see-through it is, as a percentage: **0 is solid, 100 is invisible**. |
| **Sorting Order** | Which things it's drawn in front of. The default, 5, is in front of ordinary things, so a character in the water looks under it. |

Areas that overlap each draw their own overlay, so where they overlap the picture is less see-through. The overlay is made while
playing and in the editor; it's never saved in the scene, and doesn't show in the hierarchy.

### What responds

Everything asks `Map.FluidAt(point)` where a liquid is, and areas answer it as tilemaps do:

- **Characters**: float, sink and are slowed, and can kick off the bottom, as set by their movement profile. A liquid isn't something
  solid, so a character can't jump off it, with one exception: one that is **floating at the surface** can hop out. See [Movement.md](Movement.md).
- **Things the physics moves**, with a `FloatsInFluid` component, are held up by as much of them as is under the surface and slowed by
  the liquid's thickness, so they float and sink as characters do. It's added by itself to a **carryable crate**, as wood (density
  0.6), which floats. To change it, or to make anything else float, add `FloatsInFluid` by hand and set its Material or Density. A
  crate being carried isn't affected while it's held.

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
