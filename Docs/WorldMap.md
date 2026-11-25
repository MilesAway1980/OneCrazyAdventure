# World Map and Character Progression

A plan for the map of OneCrazyAdventure and the order the characters are found in. It goes with
[CharacterRoles.md](CharacterRoles.md), which says what each character does. This is a design sketch to work from, not a
fixed layout: areas, finds and gates can all move.

---

## 1. How progression works

- **Four starter characters** (Brad, Die, Paperclip and Pencil) have no powers. They walk, jump, carry and pull levers.
  They are the baseline, and they teach the controls.
- **Up to four characters are in the party at once.** The rest wait in the roster. Every character found has to earn a
  slot in the party, so each should open something the others don't.
- **Each find opens something new.** Finding a character lets the player reach somewhere they had already seen and
  couldn't get to.
- **The order is not a line.** Several areas can be done in any order, and most gates have more than one key.
- **Hubs.** Reaching a hub takes a puzzle that needs several characters working together. At a hub the player can swap
  the party for any characters they have found.

### Rules for building areas

1. **Seen before reachable.** Show a place long before the player can get to it.
2. **Two keys where possible.** A chasm may be crossed by the Umbrella, or later by Piton or the Balloon. Each find is
   reachable with at least one character the player can already have.
3. **Entry puzzles need two or three characters, never four.** The party limit is four, and one slot should always be
   free to bring someone to help.
4. **Skeleton keys stay in check.** Dynamite and the Balloon open the most, so they come late, and tiles set a hardness or
   a force needed so they can't simply replace the Drill or the Bowling Ball.
5. **Gates match what the character does:** Roll tiles for the Bowling Ball, Fire tiles for the Torch, Dig tiles for the
   Drill, Dynamite tiles for the Dynamite, hidden tiles for the Flashlight, sockets for the Bulb, cracks for the Pebble,
   wind for the Umbrella, magnetic things for the Magnet, weights for the Brick.

---

## 2. The ten areas

| # | Area | Theme | Needed to get in | Found here |
|---|---|---|---|---|
| 1 | **Tutorial Yard** | Learn to walk, jump, switch, carry and press a button | Nothing | None |
| 2 | **The Commons** | Open meadows with four paths, each ending in a character | The starters only | Pebble, Umbrella, Bowling Ball, Ladder |
| 3 | **The Chasm** | A wide gap with a dark cave beyond | Umbrella (or Piton / Balloon later) | Bulb |
| 4 | **The Quarry** | Walls and floors of loose rock | Bowling Ball (or Dynamite later) | Torch |
| 5 | **The Cracks** | Tight crevices between cliffs | Pebble | Suction Cup |
| 6 | **The Highlands** | Ledges, cliffs and a wall with an anchor on top | Ladder or Bouncy Ball for ledges, Suction Cup for the wall | Bouncy Ball, Piton |
| 7 | **The Foundry** | Fire, heat and ice, with a Fire gate at the door | Torch | Drill |
| 8 | **The Deep Caverns** | Dark, with hidden paths | Bulb and Suction Cup | Flashlight, Dynamite |
| 9 | **The Windworks** | Wind shafts, magnets and heavy machinery | Umbrella and Drill, plus a weight puzzle | Brick, Magnet |
| 10 | **The Summit** | The final climb, a last wall of blast tiles | Dynamite | Balloon |

Fifteen characters are found across the ten areas, with the four starters, which makes nineteen in all.

### Order of play

- **Wave 1: The Commons.** The player gets four characters from the first area, in any order: Pebble, Umbrella, Bowling
  Ball and Ladder.
- **Wave 2: any order.** The Chasm (Umbrella), The Quarry (Bowling Ball), The Cracks (Pebble) and The Highlands (Ladder).
- **Wave 3: each needs a wave-2 find.** The Foundry (Torch, from the Quarry), the Deep Caverns (Bulb and Suction Cup, from
  the Chasm and the Cracks), and the Piton, found on the Highlands wall (Suction Cup, from the Cracks).
- **Wave 4:** The Windworks (Umbrella and Drill), then The Summit (Dynamite, from the Deep Caverns). The Balloon is the
  last find, and is mostly there to open the secrets left in earlier areas.

---

## 3. What opens what

```
Starters ──► The Commons ──┬─► Pebble ─────► The Cracks ──► Suction Cup ──┐
                           ├─► Umbrella ───► The Chasm ───► Bulb ─────────┤
                           ├─► Bowling Ball ► The Quarry ─► Torch         │
                           └─► Ladder ─────► The Highlands ► Bouncy Ball  │
                                                        └──► Piton (needs Suction Cup)
Torch ──► The Foundry ──► Drill
Bulb + Suction Cup ──► The Deep Caverns ──► Flashlight, Dynamite (hidden path)
Umbrella + Drill ──► The Windworks ──► Brick, Magnet
Dynamite ──► The Summit ──► Balloon
```

### Where an area can be reached two ways
- The Chasm: Umbrella, or later Piton or Balloon.
- The Quarry: Bowling Ball, or later Dynamite.
- The Highlands ledges: Ladder, Bouncy Ball, or later Balloon.
- The Cracks: Pebble only, which is why he is free in the first area.

---

## 4. Hubs

Decided rules:

- A **hub** is a place where the party can be swapped. Each area has at least one, and large areas have a hub in the
  middle too. **The game starts at a hub**, so there is always one.
- Reaching a hub needs a **combination of two or three characters** working together, so a hub is also a reward.
- **Finding a character does not put them in the party.** They are added to the characters that can be chosen, and wait
  for the player at the hubs. This means a character found somewhere never has to get back out of it: the player goes
  back to a hub and swaps. The party is the four characters in the game right now.
- **A hub is reached as soon as any party member touches it,** and it becomes the checkpoint.
- **Hubs are checkpoints.** A character who dies comes back at the hub that was reached last.
- **A character at a hub can bring everyone else to it** (warp the rest of the party and the other players there).
- **A player stays as their character** until they are at a hub, or have been brought to one. Then they can choose another.
- **Sharing the party between players already works** through the existing character switching: whoever switches takes the
  first character nobody else is controlling. With two players, one may end up with three characters and the other one, by
  switching. Over a playthrough, players can swap around to play any of them. (Player 1 having the final say over the
  split isn't built, and the existing switching may make it unnecessary.)
- **Only the characters at the hub can be swapped out.** A character out in the field is shown as AWAY on the swap
  screen and can't be changed. A player can swap just their own character, or two characters if two are at the hub.
  Changing the party doesn't move anyone else: the characters out in the field stay where they are.
- **Someone must stay at the hub.** The swap won't let the last character at the hub be taken out without another being picked
  first, so whoever is playing there always has a character, and nobody can be left unable to get back to a hub.
- Characters that are swapped out are put away, and the ones swapped in appear at the hub, in the free spawn spots. What a
  character left out in the world (a lit Torch, a thrown Piton head, a solid Brick) is not kept when they are swapped out.

### What is built
- **`CharacterSelecter`** keeps the roster (found characters) apart from the party (up to four, in the game).
  The ticks on the CharacterScript asset are the characters found from the start.
- **`CharacterPickup`**: place it where a character is found. It shows the character, and a party member touching it adds
  them to the roster.
- **`CharacterHub`**: a trigger area with spawn points. It sets the checkpoint, and Interact in its area opens the menu.
- **`HubMenu`**: Swap Characters, Bring Everyone Here, Hints (on or off), Close. Operated by the player who opened it.
- **Master text (`GameText`)**: every word the hub menu, the messages, the join and leave prompts and the Flashlight's
  "FLAT" show is in one list. Open it with Tools > Game Text > Open Master Text and change the words in the inspector.
  Hub names are set on each hub, and character names are the names of their scene objects.
- **Respawn:** a character with hit points who dies is brought back at the current hub.
- **`KillZone`**: a trigger area that sends any party member who touches it back to the checkpoint, for the bottom of the
  map and for pits. It can take some hit points off on the way, and it works for characters with no hit points as well.
  Anything with **`ReturnsToStart`** that touches it is put back where it began instead (see below).
- **`ReturnsToStart`**: remembers where an object was when the game started (its place and angle) and, when a Kill Zone
  touches it, puts it back there at rest, on every machine. A carryable crate has it automatically, so a crate knocked off
  a ledge isn't lost. To make anything else with a Rigidbody2D come back, such as a pushable block, add the component by
  hand. A crate that is being carried is never sent back (if the carrier falls in, they are sent to the hub and it goes with
  them). The Kill Zone's layer has to collide with the object's layer, as it does with the characters'. If something else is
  standing on the start spot when it comes back, they overlap; nothing clears the way.

### Still to do
- **Who may open the hub menu.** Any player at a hub can open it, and whoever changes the party reassigns everyone: a
  player whose character is swapped out is given the first free one. If this causes trouble in play, a rule such as
  "Player 1 only" can be added.
- **Saving what has been found** between sessions. The roster is kept for the session only.
