# Character sorting

Characters that stand in the same place used to flicker, each fighting to be drawn in front. With the same sorting layer and the same order, sprites at the
same depth tie, and which is drawn first is left to chance. `CharacterSorting` (`Assets/Scripts/Player/Characters/CharacterSorting.cs`) fixes that without any
character's prefab or scene object being touched: `Character` adds it to every character when the game starts.

## What it does

- Gives the character a **Sorting Group** (if he hasn't got one). The group draws all of his pictures (his body, the Balloon's string, the Ladder's top piece,
  anything a script adds under him) as one, keeping their order among themselves.
- Sets the group's sorting order to **his own picture's order, plus one, plus a number that's different for every character** (counted as they start). So each
  character is always in the same place in front of or behind each other one, and nothing ties.
- The character **a player is controlling is drawn in front of the others** (their number plus the count of characters made, so still a small range). Change
  `ControlledInFront` in the script to turn that off.
- If there's a sorting layer called **Characters**, he and all of his pictures that shared his main picture's layer are moved onto it. Without it, he stays on
  whatever layer he's on, and the fix still works.

The pointer over a character's head after switching, the player tags and the pins are not part of the group: they're drawn at orders 500 and over, on the layer of
his main picture, so they stay in front of every character.

## Setting up the layers (once)

In **Edit > Project Settings > Tags and Layers > Sorting Layers**, make these, in this order from the top of the list (the bottom of the list is drawn in front):

1. **Background**: scenery behind everything (sky, far hills).
2. **Default**: the level itself (ground, walls, levers, buttons, crates). What's there already.
3. **Characters**: the characters. Made by name, so it has to be spelled like this.
4. **Foreground**: what should cover the characters (bushes in front, a dark wall a character walks behind, vines).

Then give the scenery its layer on its Tilemap Renderer or Sprite Renderer. Nothing about the characters needs to be set.

## Things that can go wrong

- **Something is drawn behind a character that should be in front of him** (a foreground bush): it's on Default, and the characters are on Characters. Put it on
  Foreground.
- **Something that should be behind him is in front** (the sky): put it on Background.
- **Water** (a Liquid Area's overlay) is on Default at order 5, which is in front of characters now but would be behind them once they're on Characters. If a
  character should look submerged, set the Liquid Area's **Sorting Layer Name** to `Foreground` (or `Characters` with a Sorting Order over 100).
- **Anything of yours that copies a character's layer** works as before, as it copies the layer his main picture has by then (Characters).
- An effect that you want *between* a character's pictures (say, a light between his body and his flame) can't be, now that they're one group.
