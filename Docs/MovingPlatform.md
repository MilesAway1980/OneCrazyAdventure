# Moving platform (`SwitchMovingPlatform`)

A switchable object: a platform that follows a path of points when a lever, a button or anything else that works switchable objects
tells it to. It can stop at the end of the path, turn around, or go round in a loop; take corners sharply or in a curve; and, for a
button, follow how far the button is pressed.

## Setting one up

1. **The platform.** Put it where the path starts, with a `Rigidbody2D` set to **Kinematic** and a collider, so what stands on it is
   carried. (Without a Rigidbody2D it moves by its transform instead, and carries nothing.)
2. **The points.** Make an empty object for each place it goes, in order, and put them in **Path Points**. It starts from where it
   is, so the first point is where it goes first, not where it starts. They mustn't be children of the platform, or they'd move with
   it. Where they are when the game starts is the path.
3. Add `SwitchMovingPlatform` to the platform (or to another object, with **Platform** set to it).
4. Wire it to a switch, as for a door.

Select the platform in the scene view: a cyan line shows the path as it will be followed, with the corners rounded, and the points
are circled. Check it before playing.

## Settings

| Setting | What it does |
|---|---|
| **Speed** | How fast it goes, in units per second. |
| **Path End** | At the last point it **Stops**, **Turns Around** (back to the first point, and so on, between the first point and the last), or **Loops** (on from the last point to the first). Where it started isn't part of the back and forth or the loop: it goes there from the start once, and only switching it off takes it back. |
| **When Switched Off** | When the switch is turned off it either **Stops** where it is (and carries on from there when switched on again) or **Returns To Start**: back to the first point, then straight to where it started. |
| **Corner Radius** | 0 goes right to each point before turning. Above 0 the corners are curves, rounder the higher it is, up to what the path has room for: a corner can use at most half of each piece of path it shares with another rounded corner. A turn of almost 180 degrees, or hardly any turn at all, isn't rounded. |
| **Movement Method** | How speed changes along the way (below). |
| **Ease Seconds** | For the methods that ease: how long it takes to get up to speed from rest, and to slow to rest. |
| **Pause At Points** | How long it waits at each point on its way, in seconds. With rounded corners it waits at the middle of the curve. |
| **Stay Switched On**, **Switch Off Time** | As for any switchable object: it stays running, or is switched off by itself that many seconds after it was last told to run. |

### Movement methods

| Method | Speed |
|---|---|
| **Constant** | The same speed all the way, starting and stopping all at once. |
| **Smooth Ends** | Speeds up from rest at the start and slows to rest at the end. Going back and forth it also slows to rest at the first point, where it turns round. In a loop, which has no ends, it's steady. |
| **Ease Between Points** | Slows to a near stop at every point and speeds up again. |

## Switches

- **A lever, or a button pressed all the way.** Switching on sets it off along the path, as Path End says. Switching off does what
  **When Switched Off** says.
- **A button set to Allow Partial Presses.** Its position follows how far down the button is: half pressed takes it half way along the
  path, measured along the path (round its corners), not in a straight line to the last point. A character of weight 20 on a button
  that needs 50 moves it 40% of the way. It moves toward that place at its own speed and method, and follows the button back up. When
  the button is let go it goes back to the start. This part ignores Path End and When Switched Off: it's always the path from the
  start to the last point and back.
- **Stay Switched On** with such a button keeps it as far along as the button has taken it.

## Things to know

- **Characters ride it.** A character standing on the platform is carried with it, and can still walk and jump. This needs the
  platform to have a Kinematic Rigidbody2D, and carries sideways movement only (see Riding in [Movement.md](Movement.md)).
- **So do crates.** A carryable crate, or anything with a `PushableSolid`, is carried by the platform it stands on, and can still be
  pushed along it. For anything else the physics moves, add a `PlatformRider` component.
- **The start isn't a point.** Switched on, it goes from where it started to the first point, then goes between the points as Path End
  says, and never goes back to the start unless it's switched off with **Returns To Start**. In the scene view, a loop shows its
  closing line from the last point to the first, and the line from the start to the first point is only used once.
- **Rounded corners** count the middle of the curve as the point. The first point has the start on one side of it, so with Corner
  Radius above 0 it's a curve too, and a back and forth turns round, waits and slows at the middle of that curve rather than at the
  exact spot. The last point has nothing after it, so it's always reached exactly.
- **A loop of one point** has nowhere to go round, so the platform goes there and stays, as Stop does.
- **At the end of a path that Stops**, the platform stays at the end until it's been taken back: switching it on again does nothing
  until a switch-off has returned it (**When Switched Off: Returns To Start**) or a button has taken it back.
- **Returns To Start in a loop** goes round the loop the nearer way to the first point, then straight back to where it began.
- The points are read when the game starts, so moving them while playing changes nothing.
- **Over a network**, the host moves the platform and its position is sent. Run **Tools > Network > Make World Networked** after
  adding one.
- The older `FollowPath` script is a different thing: it isn't switched, and has no corners or methods.
