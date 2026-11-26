# Character Roles and Puzzle Design

A working design document for the characters of OneCrazyAdventure: what each one does, what kind of puzzle each one
solves, how each one helps the others, and what is built, planned or still open. It is meant to be updated as the
characters and the puzzles change.

Last reviewed against the code: November 2026.

---

## 1. Design goals

- **Up to four characters are used at once** to solve a puzzle. One player may switch between all four, or up to four
  players may each control one. The game is meant to be equally good either way.
- **Players work out which characters suit each puzzle.** Puzzles are designed to need particular characters, or
  combinations of them, and should not all be solvable by whoever is most powerful.
- **Characters are found as the game goes on.** A player may start with only a few, and unlock more. A newly found
  character may open up areas that couldn't be reached earlier. See section 6.
- **No character is a one-trick pony**, though not every character needs two abilities. Passive traits (speed, size,
  weight) count: they are things a character is simply good at without pressing a button.
- **Every character should be fun to play**, not only useful. Nobody should be the character everyone always picks, and
  nobody the character nobody wants. Each strong ability should come with a cost or a limit.

---

## 2. Things every character shares

These are properties of the game, and puzzles can be built on them:

- **Characters do not collide with each other.** A character can only stand on another if that one is made solid and put
  on the Ground layer, as the Brick does.
- **Characters keep their state when the player switches away.** The Brick stays solid, the Magnet stays on, the Bulb stays
  bright, the Umbrella stays open, and Piton's head stays out. That is what lets four characters work at once.
- **Weight is the mass of a character's Rigidbody2D.** Stand On tiles, falling platforms and buttons all go by weight, so
  a heavy character and a light one behave differently on them.
- **Size is the size of a character's collider.** A small one fits where a large one can't.
- **Liquid changes the cast.** The Torch is put out, the Flashlight shorts out, the Umbrella can't float, and the Balloon
  rises fast.
- **Heat and cold.** The Torch warms others and sets Fire tiles alight. Ice zones freeze characters (the freezing
  mechanic itself is parked).
- **Light.** Dark areas are lit by Bulb, Flashlight, Torch and sconces.

### Destructible tiles: what breaks what

| Damage type | Broken by | Notes |
|---|---|---|
| **Roll** | Bowling Ball, rolling at speed | A speed can be set, and which sides count. |
| **Stand On** | Anything with weight standing on it | Weights add up, or a single heavy one is needed. Brick is the obvious heavy one. |
| **Touch** | Anything touching it for long enough | Per side, with timers. |
| **Dig** | Drill | Hardness sets how long it takes. Radius makes it a bigger block. |
| **Fire** | Torch (anything that gives off heat) | Spreads to neighbouring Fire tiles. Intensity can be required. |
| **Dynamite** | Dynamite's blast | Flies as a block or breaks into debris. Force can be required. |
| **Wood** | Not built | Commented out until it's clear what it was for. |

---

## 3. The characters at a glance

"Built" is what the code does now. "Planned" is what is intended and not yet built.

| Character | Core idea | Status |
|---|---|---|
| Balloon | Floats anywhere, but slowly | Built. Planned: nothing needed; see notes. |
| Pebble | Fast, nimble and tiny | Movement and size by settings. Carried by the Balloon (works). |
| Torch | Fire, light and warmth | Built |
| Dynamite | Blasts tiles and launches things | Built |
| Piton | Rope across gaps | Built |
| Brick | A solid block others can stand on | Built |
| Bouncy Ball | Inflates and bounces, charged jump | Built |
| Drill | Digs up, down and sideways | Built |
| Umbrella | Slow fall and glide; rides blower fan updrafts | Built (wind untested) |
| Suction Cup | Sticks to walls and ceilings | Built. Planned: help others. |
| Bowling Ball | Rolls and smashes | Built. Planned: gains speed on ramps. |
| Ladder | Extends into a ladder | Built |
| Magnet | Pulls things toward him | Built. Planned: pulls himself onto magnetic things. |
| Bulb | Glows brighter; screws into light sockets | Built (sockets untested) |
| Flashlight | Aimable beam, reveals hidden tiles | Built |
| Brad, Die, Paperclip, Pencil | Tutorial characters | No coded abilities, by design. Brad carries and is fast, Die is heavy, Paperclip jumps high, Pencil floats. Brad being the only carrier needs the setup in section 4 |

---

## 4. Each character

### Balloon
- **Does:** floats up while the action is held (and rises fast in liquid), drifting sideways at a low speed. Things can
  hang from his string.
- **Solves:** high ledges, pits, vertical shafts, lifting a carried object.
- **Helps others:** lifts the objects on his string, and the Pebble (confirmed working).
- **Drawback (decided):** he can go anywhere, but he is tediously slow. That slowness is his cost, and it is what keeps
  him from being everyone's pick.
- **Parked ideas** (revisit when puzzles start): weighing almost nothing for crumbling floors and falling platforms, a
  limited air supply, popping on fire or spikes, lift that depends on the load.

### Pebble
- **Does:** nothing by button. He is fast and nimble (by movement settings), and small.
- **Solves:** fits into cracks and spaces that other characters can't, which are reached by the tiny size of his
  collider alone. Quick to scout and to cross ground.
- **Helps others:** reaches things in places others can't, such as a switch inside a crack. Can be lifted by the Balloon.
- **Done:** being carried by the Balloon works.
- **To do:** make sure his size and weight are what puzzles expect: he should be light for crumbling floors.

### Torch
- **Does:** burns, lights sconces he stands by, warms other characters, sets Fire tiles alight. Put out in liquid, and
  burns again when out of it.
- **Solves:** sconce and light puzzles, burning through Fire tiles, thawing frozen characters, lighting dark areas.
- **Helps others:** warms and lights them.
- **Drawback:** water puts him out.

### Dynamite
- **Does:** charge with the action, then explode, with a meter showing the fuse. The blast breaks or throws Dynamite
  tiles and pushes every body in range, including other characters.
- **Solves:** blast walls and floors, launch objects and characters over gaps.
- **Helps others:** launches them.
- **Drawback:** the charge time and the recovery after a blast.

### Piton
- **Does:** aim, throw his head on a rope. It sticks to the first thing it touches. Pull it back with a double tap.
- **Solves:** gaps and chasms. The rope can be crossed by others.
- **Helps others:** the rope stays out when the player switches, so it is a bridge for the rest.
- **Drawback:** he can't move while his head is out.

### Brick
- **Does:** toggles into a solid block, which other characters can stand on, jump from and push.
- **Solves:** a step or platform for others, a heavy weight on buttons, Stand On tiles and falling platforms, a plug for
  a gap.
- **Helps others:** strongly. By himself he is mostly a weight.
- **Drawback:** can't move while solid. Can't turn solid with anything inside him.

### Bouncy Ball
- **Does:** hold the action to inflate into a big bouncy ball. Push down with the action to squash and charge a high
  jump. Double tap to stay inflated.
- **Solves:** high ledges, bouncing across gaps.
- **Helps others:** little.
- **Drawback:** needs room to inflate.

### Drill
- **Does:** digs up (jump into the tile), down (jump, push down, hold the action) and sideways (press the action while
  running). Hardness sets how long a tile takes. A tile's radius makes it a larger block.
- **Solves:** tunnelling through Dig tiles, opening shortcuts and hidden routes.
- **Helps others:** the tunnels stay open for the time the tiles are set to, which can make timed passages.
- **Drawback:** only digs tiles marked for it, and can't walk while drilling.

### Umbrella
- **Does:** open while falling to float down slowly. Keeps the speed he had, so a running start then a float is a glide.
  Stays open if the player switches.
- **Solves:** long gaps, safe drops, falling without damage.
- **Wind (built, untested):** open, he catches blower fans' wind far more than anyone else (`openWindMultiplier`, 4 by
  default) and rides updrafts up. While the wind is lifting him, the extra pull his float settings put on an upward speed
  is left off, so he can be carried a long way. The Blower Fan has been reworked (no collider; the force falls off with
  distance from the fan) and has a `WindCatcher` hook that multiplies the force on anything that has one.
- **Helps others:** no.
- **Drawback:** can't float in liquid.

### Suction Cup
- **Does:** hold the action to stick to a wall, and climb it with the stick. Hangs from a ceiling.
- **Solves:** vertical and ceiling routes.
- **Planned (from his notes):** a step for others at a height they can't reach, a weight hung over a button, an anchor
  for Piton's rope. The step needs him to be solid and on the Ground layer, as the Brick does.
- **Helps others:** not yet.

### Bowling Ball
- **Does:** hold the action to roll. Rolling at speed breaks Roll tiles.
- **Solves:** breakable floors and walls.
- **Planned:** builds up speed down long ramps, which carries him over gaps.
- **Helps others:** clears the way.
- **To consider:** he is heavy, which could make him a weight for buttons and Stand On tiles.

### Ladder
- **Does:** extends into a ladder that others can climb, controlled with up and down while the action is held. A double
  tap keeps going by itself.
- **Solves:** any place that needs a climb.
- **Helps others:** strongly. It's his whole job.
- **Idea:** a horizontal bridge mode.

### Magnet
- **Does:** hold the action for a field that pulls things toward him. Stays on if the player switches.
- **Solves:** pulling loose objects, such as a weight onto a button.
- **Planned:** pulls himself onto magnetic items, which lifts him off the ground, and rides magnetic devices as they
  move (the Magnet Box moves back and forth).
- **Helps others:** moves things for them.

### Bulb
- **Does:** glows normally and brighter while the action is held. Stays as bright as he was left.
- **Solves:** dark areas, the brighten switch.
- **Light sockets (built, untested):** a `LightSocket` placed anywhere in the map. Standing at one, holding Interact and then
  pressing the action screws him in (the action doesn't light him while Interact is held, so he is only lit by being
  screwed in); his light grows hugely (radius and brightness multipliers are set on the socket) and he
  stays lit and held there, even if the player switches away, until he is played again and presses the action to screw
  himself out. One bulb per socket.
- **Helps others:** lights the area for the rest.

### Flashlight
- **Does:** an aimable spotlight with brightness levels (a tap steps up a level), a battery with a meter, and shorting
  out in liquid.
- **Solves:** lighting a direction, far away.
- **Hidden tiles (built):** an area of the map marked with a `HiddenTileSection` has tiles that only show where
  his beam shines. A shader cuts each tile out of the beam pixel by pixel, so the beam can cut a tile through at any
  angle (the shader is `Assets/Shaders/HiddenTileSprite.shader`). Tested and working. Only the Flashlight reveals them; no other light does. The dimmer his beam, the closer he must be and
  the less completely they show; they are fully shown only at his brightest level, and they flicker with a low battery.
  Each area chooses one of three ways: *Solid But Unseen* (always solid, only visible while lit), *Solid Only While Lit*
  (only solid while the beam is on them), or *Stays Revealed* (visible for good once found). This separates him from the
  Bulb.
- **Helps others:** reveals things for them.

### Tutorial characters
Brad, Die, Paperclip and Pencil teach the game's controls and how to work together. They aren't used later, and have **no coded
abilities** (no action button): each one is only good at something because of what he's made of, using what the game already
does. Each is meant to teach a different kind of puzzle, so no two of them are good at the same thing.

| Character | He's good at | The puzzles | Made from |
|---|---|---|---|
| **Paperclip** | Height. Jumps high (about 3 tiles). | Ledges nobody else can reach. | Jump trait |
| **Pencil** | Water. Floats while the others sink. | Getting across a pool. | Density (his material) |
| **Die** | Weight. Heavy, so fans don't blow him about and he sets off buttons the others can't. | Weight buttons, blower fans. | Mass, material |
| **Brad** | Carrying, and speed. The **only one who can carry things** (crates), and the fastest. | Crates that have to be taken somewhere, often against a timer: a door or bridge that only stays open for a few seconds. | Able To Carry Objects, run speed, Switch Off Time on the door |

- **Brad is a porter.** Carrying is a switch on the character (**Able To Carry Objects**), so for him to be the only one, it's on for him and
  off for the other three. He needs a **Carry Attachment Point** (a Kinematic body, such as an empty child object, that what he carries
  is held by) or the game warns. His speed is what makes the timed puzzles his: a crate on one side of a door that stays open for a
  few seconds, a button far from the door. Keep his jump ordinary (a normal Floatiness) so he isn't a second jumper next to Paperclip.
- **Why not two jumpers.** Paperclip (height) and Brad (distance) used to both be jumpers, which taught the same thing twice. Brad's
  difference is carrying and time, and Paperclip keeps the jumping.
- **Timed puzzles** use the **Switch Off Time** on the thing being switched (a door, a bridge, a moving platform), or a button that isn't
  held: it closes again that many seconds after it was last told.
- Ideas that weren't used, for later or for other characters: a **featherweight** (doesn't set off buttons or crumbling floors, and rides
  fans), a **glow** (a soft constant light for dark rooms), a **sturdy** one (no fall damage, for long drops).

---

## 5. Where combinations already work

- **Balloon with Pebble:** tiny and quick, taken up high.
- **Brick with anything** that needs a platform or a weight.
- **Ladder or Piton for the rest** on vertical routes and chasms.
- **Dynamite with anyone:** a blast launches, and also breaks tiles.
- **Torch with the Bulb or Flashlight:** several lights, and heat for ice.
- **Piton with Suction Cup:** an anchor for the rope, once he's built for it.
- **Magnet with buttons:** pulls a weight onto a button.
- **Umbrella with Blower Fans:** a long glide.

---

## 6. Unlocking characters

Characters are found as the game goes on, so the order they come in matters.

- **Early characters should be useful in many places**, and give the player something to do straight away, so the early
  game doesn't feel empty. Candidates: Pebble, Umbrella, Bouncy Ball, Torch.
- **Characters that open routes** (Balloon, Drill, Suction Cup, Piton) are the ones that gate areas. A level can be
  designed so a place first looks closed and is opened when one of them turns up.
- **Support characters** (Brick, Ladder, Magnet) are best met once the player has several characters to help.
- **Level design must say how a place is reached without the new character**, or whether it simply can't be. Two ways
  into a place make an earlier reach possible and a later one a shortcut.
- The set that is unlocked is currently a switch for each character on the CharacterScript asset. Four can be used at
  once (`maxCharactersAtOnce`).

---

## 7. Open questions and to-do

- Balloon: how slow, and whether a cost is needed beyond speed (see the parked ideas).
- Pebble: his weight and size, so that puzzles can rely on them.
- Flashlight: tune how far and how completely the beam reveals at each level as real puzzles use it. The beam isn't
  blocked by Shadow Casters, only by tiles that collide.
- Bulb: test the light sockets (`LightSocket`) and tune the multipliers. Possible later: sockets that power switches or
  other things when a bulb is in them.
- Bowling Ball: gaining speed on ramps and vaulting gaps. Possibly heavy, for buttons.
- Magnet: sticking to magnetic items and riding movers.
- Umbrella: test riding blower fan updrafts and tune `openWindMultiplier` and the fan strengths together.
- Suction Cup: becoming a step for others.
- Ladder: a horizontal mode, if wanted.
- Wood tiles: decide what they were for, or remove them.
- Freezing (ice zones): its own future mechanic.
- Which characters unlock in what order, and the levels that depend on it.
