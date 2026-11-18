# Drawbridge (`SwitchDrawbridge`)

A switchable object: a long platform that swings about a hinge when a button, lever or anything else that works switchable objects
tells it to. It might start upright and lower across a gap, or start flat and raise to close an opening. How heavy it is decides
whether it comes to rest gently or slams.

## Setting one up

1. **The bridge.** Make the platform, and put it in the position it *starts* in (upright or flat). Give it a `Rigidbody2D` set to
   **Kinematic** and a collider, so what stands on it is carried. (Without a Rigidbody2D it moves by its transform instead, and
   carries nothing.)
2. **The pivot.** Make an empty object where the hinge is: the bottom of an upright bridge, or one end of a flat one. It can be a
   child of the bridge. Where it is when the game starts is the hinge, and it stays there.
3. **The component.** Add `SwitchDrawbridge` (on the bridge, or on another object with **Bridge** set to it), and set **Pivot**.
4. **Swing Degrees.** How far it turns when it's switched on, from how it's placed. Positive is counterclockwise.

   | It starts | Hinged at | To lower/raise it | Swing Degrees |
   |---|---|---|---|
   | Upright | its foot, bridge reaching up from it | Lowers to the right | -90 |
   | Upright | its foot | Lowers to the left | 90 |
   | Flat, reaching right of the hinge | its left end | Raises | 90 |
   | Flat, reaching left of the hinge | its right end | Raises | -90 |

   Select the bridge in the scene view: a green arc shows the swing and a wire outline shows where it ends up. Check it before
   playing.
5. **Wire it to a switch**: add it to a button's or lever's list of switchable objects, as for a door.

## Speed and weight

- **Speed** is the fastest it swings, in degrees per second.
- **Weight** is how heavy it is. A motor swings it, and does the same job for any weight, so a heavy bridge is slow to get going.
  The motor starts slowing the bridge down at the point where a light one could be stopped, but a heavy one can't be: it arrives at
  the end with speed left over, and hits the stop. A light bridge, even a fast one, is slowed in time and comes to rest gently.
- It's a **slam** if the hit is hard enough: half the weight times the turning speed in radians per second, squared, has to be at
  least **Slam Energy** (default 1). Below that it simply stops. A slam can **bounce**, sets off **Slam Sound** (louder for a harder
  hit) and the **On Slam** event, which is told how hard the hit was.

At the default Slam Energy and a Speed of 90:

| Weight | What it does |
|---|---|
| 1 | Comes to rest gently, whatever the speed up to the motor's limit |
| 2 | About the edge of a slam |
| 4 | Slams |
| 10 | Slams hard, and is slow to get going |

A slow heavy bridge doesn't slam, because it hasn't the speed; a fast light one doesn't, because it can stop. A fast heavy one does.
To make a slam bounce more, raise **Bounciness** (0 stops dead, 1 keeps all its speed off the stop); it only bounces after a slam,
and each bounce that's still hard enough is a slam of its own, getting smaller until it settles. Lower **Slam Energy** to make a
bridge slam more easily.

**Motor Strength** (Advanced, default 720) is how hard the motor can accelerate a weight of 1, in degrees per second squared. It
also sets how early the motor starts slowing the bridge. Raise it for a snappier bridge overall; leave it alone to tune weight and
speed against each other.

## Switching

It works like a door:

- With a **button**, tick *Stays Active When On* on the button so that it keeps telling the bridge while pressed; the bridge swings
  back when it's let go. A button set to **Allow Partial Presses** swings it by how far down the button is.
- With a **lever**, it swings when the lever is thrown and back when it's thrown back. A *One Time* lever leaves it.
- **Stay Switched On**, on the bridge, leaves it swung. **Switch Off Time** swings it back that many seconds after it was last told
  to.

## Over a network

The host moves the bridge, and its position and rotation are sent: run **Tools > Network > Make World Networked** after adding
one. The slam sound and On Slam event only happen on the host.

## Not done

A screen shake, dust or crushing on a slam are left to the **On Slam** event: nothing is hooked to it yet. A bridge doesn't crush or
hurt anything caught under it (the kinematic body just pushes what it can).
