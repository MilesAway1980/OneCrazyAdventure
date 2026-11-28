# Floating objects

Floating objects are balloons-on-strings that hold things up: a platform, a crate, anything with a Rigidbody2D. They're much simpler than the
Balloon character. Nobody controls one, it doesn't walk or pick anything up, and it only does one thing: it has a **Lift**, and it holds up that much mass.

A platform with a mass of 100 and four floats of Lift 25 tied to it **stays exactly where it is**. Take one away (switch it off, destroy it) and the lift is
75: the platform starts to sink, at a steady pace. Tie five, and it rises.

## Setting one up

1. **The thing to hold up** needs a **Dynamic Rigidbody2D** with its mass set (a platform of 100, say), **and a collider** (a Box Collider 2D the size of its
   picture will do). The collider matters more than it seems: physics works out how hard a body is to turn from its colliders, and a body with none has
   almost no resistance to it, whatever its mass is set to, so the slightest uneven push spins it wildly. (It says so in the Console if there isn't one.)
2. **Add a `FloatingAnchorPoint`** to it, or to a child of it where the string should be tied (a corner of a platform). Add as many as you like.
3. **The float** is any object with a picture. Add the **`FloatingObject`** to it, set its **Lift**, and drag the anchor point into its string's **Anchor**.
4. Give the platform **Freeze Rotation Z** if it shouldn't tip when the floats don't match.

| Setting | What it does |
|---|---|
| **Lift** | How much mass it holds up. With several strings it's shared between them. |
| **Strings** | One or more. Each has an **Anchor** (the anchor point it's tied to; empty is a string that hangs loose and holds nothing), a **Length**, and a **Start Offset** (where on the float it starts). |
| **Drag Rate** | How much it holds back the body going up or down, so that a body out of balance sinks or rises at a steady pace and doesn't speed up and up. More is slower and steadier. 0 is none. |
| **Turn Damping** | How quickly the swinging of what it holds dies away, in proportion to how much of its weight is held up, so a rocking platform steadies. 0 is none. |
| **Level Spring** | 0 by default. Above 0, it turns what it holds back to level (the way it was when the game started), in proportion to how much of its weight is held up. This stops it tipping as far, and from hanging from one end. |
| **Bob Height**, **Bob Speed** | How far, and how fast, the float bobs. |
| **Bob Pull** | How much the bobbing pulls on what it holds: the lift swings up and down with the bob, by this fraction per unit of Bob Height. A float that bobs more pulls harder and rocks the body, and floats that don't bob together make it less steady. The lift is the same on average, so the body doesn't sink or rise. 0 is a bob that's only for looks. |
| **Follow Time** | How long the float takes to catch up with what it's tied to. A little lag makes it feel buoyant. |
| **String Color**, **String Width** | What the string looks like. |

The **anchor point** has two settings of its own:

| Setting | What it does |
|---|---|
| **Hold Until Attached** | On by default. When the game starts, the body is held still until every float tied to it has come on and put itself in place, so it doesn't start to fall (and tip) under the first of them while the rest are still being tied. |
| **Release Delay** | How many seconds more it's held after the last float is in place, for them to settle. |

Selected in the scene, an anchor point shows **Lift here | body mass**: what's held up there against what the body weighs.

## How it works

- **The lift is a force on the body**, equal to the weight of that much mass (`lift × gravity × the body's Gravity Scale`), so it cancels it exactly. So
  4 × 25 against a mass of 100 is exactly balanced, whatever gravity the body has. More lift than mass and it rises, less and it sinks.
- **The lift pushes up at the place the string is tied**, so it holds the body the way strings would. Floats spread evenly across the top of a platform hold it
  level (and a little steadier for being above its middle). Take away one end and that side sinks and the platform tips, until the lift's twisting and its
  weight balance. And one float at the end that holds all of the weight, say a platform of 500 with one float of lift 500 at one end, **hangs from that end**: it
  turns until its middle is straight below where it's tied (a quarter of a turn, 90 degrees, for a platform tied at the middle of its end), and swings there.
- **A string tied below the middle of the body** (underneath a platform) can't be a push from there: it would be a balance that can't be kept, and the platform
  would turn over. So it pushes up from the height of the middle of the body, off to the side by as far as it's tied. It makes no difference how far below it's tied,
  and the body still hangs from a single one as it should. (Ties above the middle, on top, are as they'd hang.)
- **Level Spring** isn't needed. It's there for a body that should stay nearly level however unevenly it's held, which also stops it hanging from one end.
  Freeze Rotation Z on the body turns off everything that turns it.
- **Bobbing pulls on what it holds.** The lift of each float swings with its bob (**Bob Pull** times **Bob Height** each way, 10% by default), around the same
  lift on average, so the body stays where it is but isn't perfectly still: it bobs and rocks a little, and more with floats that bob more or out of step.
- **Several strings on one float** share its lift equally between the anchors they're tied to.
- **Drag** acts on how fast the body is moving up or down as a whole (on its middle, so it doesn't fight its turning), in proportion to what's holding it, so the platform settles to a steady speed: the lift that's
  missing over the lift that's left, times gravity, over **Drag Rate**. A platform of 100 missing one float of 25 (three of 25 left) settles at about 1.6 units
  a second with a Drag Rate of 2.
- **A character standing on it is exactly his mass.** Characters are given gravity much heavier than the world's (a multiple of it, and a harder pull again
  falling) so that they fall and jump well, and standing, all of that presses down on what they're on: a character of mass 30 might press like 250. Left like that,
  a platform that's balanced would need about eight times a character's mass in extra lift to hold him up. So the floating objects take back all of it but his mass,
  as an upward force on the platform where he's standing (**Character Weighs Mass** on the anchor point, on by default; `Character.StandingWeightRatio` is what he
  presses with). A landing is still a push, and still dips the platform. The Brick on a balanced platform of 500 needs 10,000 more lift, and a Pebble needs 5.
- **The float goes along with what it holds**, a string's length above where it's tied (in the middle of them, with several), with a little lag and bob.
  It's moved by this script, so give it no physics of its own. (A Rigidbody2D on it is made Kinematic, and it can be solid and in the way.)
- **Starting up.** The floats come on a few at a time as the scene starts, and a body that's let fall under the first of them is already off level by the time the
  last is tied. So the body is made **Kinematic** (a `FloatingBody` is added to it by its first anchor point) until every float tied to it, on any of its anchor
  points, is on and in place, and Release Delay more. Then it's put back to what it was, standing still, and the floats take its weight. It's only held once, as
  the scene starts: a float that comes on later is just one more. A float that never gets into place doesn't hold it for more than 5 seconds, and says so in the
  Console. A float that is switched off from the start isn't waited for. On a machine that has joined someone else's game nothing is held, as the host decides.
- **Taking one away** (switching off or destroying the float) means it holds nothing up. A body that was asleep is woken so that it can sink. `Cut()`
  does the same without removing the float, for anything that wants to pop one: the float and its strings stay, and hold nothing.

## Networking

The host does the physics. Run **Tools > Network > Make World Networked** and the bodies that anchor points are on are given a position that's sent (the
floats go along with them on each machine by themselves, and apply no force on a machine that has joined someone else's game).

## For the code

- `FloatingObject` (`Assets/Scripts/Objects/Floating`): `HoldUp()` is the force and drag each physics step; `Follow()` keeps the float above where it's tied;
  `ShareFor(anchor)` is how much of its lift goes to an anchor.
- `FloatingAnchorPoint`: a place to tie, `Body` (found above it if not set), `Lift` (what all the floats tied here hold up), and it wakes the body when a float goes.
