# Tutorial Yard (Area 1)

The first area. It teaches everything the player needs before the world opens up, using only the four starter characters
(Brad, Die, Paperclip and Pencil), who have no powers. It ends at the second hub and the way out to The Commons. See
[WorldMap.md](WorldMap.md) for where it sits.

What it has to teach, in this order:

1. Moving and jumping
2. Switching between characters
3. Interacting: levers, and picking things up
4. Buttons, and that a character or an object can hold one
5. Working together: one holding a door, another going through
6. A hub, and what it's for

Each lesson uses the one before it, and nothing needs a power, so the whole area can be done with the starters.

---

## The building blocks

| Piece | What it is | Notes |
|---|---|---|
| **Character Hub** | A hub: reached by touching, checkpoint, swap menu | One with **Start Hub** ticked at the start, one at the end. |
| **Tutorial Prompt** | A hint shown while someone is in its area | New. Pick the hint from a list, or write your own. The words are in the master text. Controls are filled in with the player's own buttons. |
| **Button** (prefab) | A pressure pad, moved by the script (no spring). It goes down by the fraction of **Minimum Weight** that's on it, as slowly as **Resistance** says, and switches things on once it's **Percent Pressed To Activate** of the way down. | Tick **Stays Active When On** so the door keeps being told while it's pressed. Run **Tools > Buttons > Upgrade Button Prefab** once. |
| **Lever** (prefab) | Pulled with Interact | Gives its target a push to open or close. |
| **Switch Door** | A door or gate that moves out of the way | New. Set **Open Offset** (for example 0, 4 to rise four units) and the speed. |
| **Crate** | A carryable box | A Rigidbody2D box with a Fixed Joint 2D and a Carryable Object, like the ones in the Tutorial scene. |
| **Kill Zone** | Sends a character back to the checkpoint | Below the pits. |

---

## The layout, left to right

All distances are a guide in tiles (1 unit each). Check them against how high and far the starters can really jump, and keep
every gap and step to about two thirds of what they can do.

```
 START HUB      JUMPS            GATE A + BUTTONS       CRATE + GATE B       LEVER       TEAMWORK        EXIT HUB
 [4 starters]   steps, a gap     (switching)            (carrying)           (interact)  (3 gates/pads)  [Yard Gate]
 ────────────   ───────────      ───────────────        ───────────────      ──────      ────────────    ──────────
   0–10          10–25            25–45                  45–70                70–85       85–110          110–120
```

### 1. Start Hub (0–10): moving
- A flat floor and the starting **Character Hub** (Start Hub ticked), with spawn points for four.
- A **Tutorial Prompt: Move** over the floor, a few units to the right of the hub.
- The four starters should be ticked on the CharacterScript asset, and placed at the hub.

### 2. Jumps (10–25): jumping
- A **Tutorial Prompt: Jump** at the first step.
- A low step, a higher one, then a gap with a **Kill Zone** under it. Falling is safe: you are put back at the hub. That
  teaches checkpoints for free.

### 3. Gate A (25–45): switching characters
- A **Switch Door** (Gate A) across the path, rising out of the way. A **Button A** in front of it opens it, but only
  while something is on it.
- **Tutorial Prompts:** Button, then Switch On Button, then Switch Character. The point: a character on the button holds
  the door, so the player has to switch to another character to walk through.
- Beyond the gate, a second button (**Button A2**) linked to the same door. The first character through stands on it,
  which holds Gate A open from the far side so the others can follow. Nobody is left behind.

### 4. The Crate (45–70): carrying
- A **Crate** on a ledge, and a long way from the next gate. **Tutorial Prompt: Carry.**
- **Gate B** is opened by **Button B**, which is too far from Gate B for a character to stay on while the rest pass. The player
  carries the crate onto the button, which holds Gate B open and frees everyone.
- This teaches that things can hold buttons, which later puzzles lean on, such as the Brick.

### 5. The Lever (70–85): interacting
- A **Lever** that opens **Gate C**, or lowers a ramp over a short gap. **Tutorial Prompt: Interact.**
- A lever stays where it is thrown, so nobody has to stay behind: the lesson is just pressing Interact.

### 6. Teamwork (85–110): needing several characters
- Two gates in a row, **Gate D1** then **Gate D2**, each with its own button **before** it.
  - One character stands on **Button D1**, holding Gate D1.
  - A second walks through D1 and stands on **Button D2**, holding Gate D2.
  - A third walks through both and on to the hub.
- **Tutorial Prompt: Teamwork.** It needs three characters, and one of the four is free, which matches the rule that an
  entry puzzle takes two or three, never four.

### 7. Exit Hub (110–120): the first real hub
- A second **Character Hub**, "Yard Gate", with spawn points. **Tutorial Prompt: Hub.**
- Reaching it makes it the checkpoint, and opens the way out to The Commons.
- The swap screen has only the starters in it at this point. That's fine: the player learns the screen here, and finds the
  first characters in The Commons.

---

## Building it with the helper

**Tools > Tutorial Yard > Open Builder** builds the whole layout into the scene that is open, under one object called
"Tutorial Yard". It needs a Ground Tile; the Button and Lever prefabs are found in `Assets/Prefabs`. It makes:

- the ground, steps, pit and the crate ledge, as a tilemap on the Ground layer
- the Start Hub and the Yard Gate hub, each with four spawn spots and a flag that shows once it is reached
- Gates A, B, C, D1 and D2 (plain brown blocks), with the buttons and lever that work them
- a crate on the ledge (from your Crate Prefab if you give one, or a plain one)
- the Tutorial Prompts, and kill zones (one in the pit, one far below)
- optionally, it moves Brad, Die, Paperclip and Pencil to the start hub and sets the CharacterScript to just those four

It can be undone, removed with its button, or built again. Everything it makes is ordinary: move it, resize it, swap the
plain blocks for art.

## Hints are off by default

The prompts show nothing until hints are turned on. A player who is stuck can turn them on with **Hints** on the hub menu,
and the choice is remembered. The words are in the master text under "Tutorial hints".

## What needs building, and what is new

| Needs | Status |
|---|---|
| Character Hub, Kill Zone, Character Pickup, hub menu, master text | Built |
| Button and Lever prefabs | Already in the project |
| **Switch Door** | Built (new) |
| **Tutorial Prompt**, on-screen hints, button names for each controller | Built (new) |
| Floors, walls and pits, the crate, and placing everything | In the scene |

## Checklist for placing the pieces
- [ ] Tick the four starters on the CharacterScript asset, and untick everyone else.
- [ ] Start hub with **Start Hub** ticked and four spawn points.
- [ ] A **Kill Zone** under every pit and below the whole area.
- [ ] Every Switch Door has its **Open Offset** set, and every button and lever points at the door it opens.
- [ ] Buttons that should hold a door only while pressed have **Stays Active When On** ticked.
- [ ] Walk it with each starter, and with two players, to check every gap and step.

## Decided
- The starters differ slightly in weight, speed and jump height, so the player sees from the start that characters differ.
  The jumps and the gates need to work for all four.
- Hints are off by default and turned on from the hub menu.

## Things to decide
- How tall the gates are, and the look of the Yard: a fence, a stone arch, a hedge.
- Whether a button should need more than one character's weight, using the starters' differences in weight.
