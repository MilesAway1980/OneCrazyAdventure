# Network test checklist

Two editors in Multiplayer Play Mode: the **main** editor hosts ("Host a game"), the **second** joins ("Join"). "Host" below means
the main editor and "Joined" means the second. Tick each box when what's said happens.

## Before you start (once for each scene)
- [ ] Run **Tools > Network > Make Characters Networked**, then **Make World Networked**, then save the scene.
- [ ] Play the scenario. Host in the main editor, then Join in the second. The second's panel should say "Joined… ready… physics off".

## 1. Connecting
- [ ] The host's panel says "Hosting… 2 connected".
- [ ] The joined editor shows the same characters, at the same places, as the host.

## 2. Players and seats
- [ ] Press a button on the joined editor's controller: a player appears there and takes a free character.
- [ ] That player can walk, jump, and use their ability, and the host sees every move.
- [ ] Switch character with **1** and **2** (gamepad: d-pad left and right) from both editors. They never end up on the same character.
- [ ] Hold the leave button on the joined player: their character becomes free on the host.
- [ ] Close the joined editor while its player is in: the host frees that character.
- [ ] With four players in (host and joined together), a fifth is refused with "the game is full".

## 3. Characters and the party
- [ ] A character swapped out at a hub disappears on both editors, and one swapped in appears on both.
- [ ] Facing, jumping, and the "using ability" picture match on both editors.
- [ ] A hub warp or a kill zone puts a character straight at the new place on both, with no sliding.

## 4. Flashlight
- [ ] Aim the head, turn the light on, change level: the head angle and the beam match on both.
- [ ] The battery bar shows for the player using him, on their own editor.
- [ ] The beam goes out in water and when the battery runs flat, and flickers on a low battery, on both.
- [ ] Hidden tiles are revealed by the beam on both editors.

## 5. Bulb and sockets
- [ ] Hold the action: the glow gets brighter on both.
- [ ] At a socket, hold Interact and press the action: he slides, spins and sinks in, and his light grows, on both.
- [ ] Press the action again: he turns, rises out and the light shrinks, on both.
- [ ] Leave him in a socket and switch to another character: he stays lit on both.

## 6. Doors, buttons and levers
- [ ] Stand on a button: the pad goes down and the door opens on both editors. Step off: the door closes on both.
- [ ] Pull a lever (host's player, then the joined player): it swings on both and what it opens opens on both.
- [ ] A door that stays open stays open on both.

## 7. Crates and other movers
- [ ] Push the crate: it moves on both.
- [ ] Carry the crate with a character that can, from each machine: it's carried on both, and dropped on both.
- [ ] A crate on a button holds the door open on both.
- [ ] Falling platforms, path followers and the magnet box (if the scene has any) move on both.

## 8. Found characters
- [ ] Collect a character pickup (from the host's player, then from the joined one): it disappears on both editors, and "found" shows on both.
- [ ] The new character is on the hub swap list.
- [ ] A pickup that was collected before the joined editor connected is already gone there, with no message.

## 9. Joining late and leaving
- [ ] Do something (open a door, collect a pickup, move the crate), then join with the second editor: it matches.
- [ ] Press "Back to playing locally" on the joined editor: the game runs normally on its own again.
- [ ] Stop the host while the other is joined: the joined editor goes back to playing on its own, with a Console warning.

## 10. Hubs, lights and messages
(Run **Make World Networked** again first, then save: it adds the hubs, sconces and ambient light.)
- [ ] Reach a hub with a character: its flag shows on both editors, and "hub reached" appears on **both** screens.
- [ ] Reach a second hub: the first stays reached, the second becomes the checkpoint on both. A character sent to a kill zone goes back to the new one.
- [ ] A kill zone shows "sent back to the hub" on both screens.
- [ ] Light and snuff a sconce (or anything lit by a switch or a flame): it lights and goes out on both. A sconce that burns for a set time goes out on both together.
- [ ] A switch that brightens the scene changes the ambient light on both, and back again when it switches off.
- [ ] Bring the party to a hub from the menu (host only): "party brought to the hub" appears on both. Collecting a pickup shows "found" on both, once each.

## 11. The hub menu, and tutorial hints, for a joined player
(Needs the hubs networked: run **Make World Networked** and save.)
- [ ] A joined player stands at a reached hub and presses Interact: the menu opens **on the joined editor**, and their character stands still.
- [ ] They play the menu with their own controls: move round, Jump picks, Interact goes back.
- [ ] Swap Characters on the joined editor shows the characters found, with the party ticked. Pick a new party and press Action to finish: the new characters appear at the hub on **both** editors, with "party changed" on both.
- [ ] Characters out in the field can't be swapped out, and the last one at the hub can't be taken out (the same messages as on the host).
- [ ] Choosing the same party again changes nothing and closes quietly.
- [ ] Bring Everyone Here from the joined editor brings the rest of the party to the hub, with the message on both.
- [ ] Hints on or off, from the joined editor's menu, changes only that editor's hints.
- [ ] While one machine's menu is open, the other machine's player can still open their own.
- [ ] Walk into a tutorial prompt's area with the hints on, on each editor: the hint shows on the editor where hints are on, naming that player's own buttons, and goes away on leaving.

## 13. The other characters
(Run **Make Characters Networked** again and save: it adds a small script to each of these. Each is played by the host's player, then the joined player. Compare the two editors.)
- [ ] **Torch:** put him in water: his light goes out and his unlit pictures show, on both. Out of the water he lights again. He still lights a sconce he stands by, on both.
- [ ] **Magnet:** hold the action: the magnetic field shows on both, and goes when the action is let go. A crate near him is pulled on both.
- [ ] **Bowling Ball:** hold the action to roll: his picture turns round as he moves, on both, the right way for the direction, and stops turning when he stops. Rolling into a tile that breaks by rolling breaks it on both.
- [ ] **Brick:** press the action to go solid (the action picture shows on both), and again to turn back. With something in the way, he shudders red on both.
- [ ] **Suction Cup:** hold the action against a wall: his wall picture shows on both, facing the right way, and climbs with the stick. On a ceiling, his ceiling picture shows. Let go and he drops, on both.
- [ ] **Drill:** hold the action in a tile that can be dug: the tiles darken and break on both. Jump, push down and hold the action: he flips upside down on both. Run and press the action: he turns on his side on both.
- [ ] **Bouncy Ball:** hold the action: he inflates, with his shell, on both, and bounces. Hold the action and down: he squashes (his charging picture) and the charge bar fills for the player using him, then he jumps on release, on both. Double-tap the action: he stays inflated, on both.
- [ ] **Dynamite:** hold the action: his fuse bar fills for the player using him. When he goes off: his exploded picture shows and the pieces fly out on both, and tiles in reach break on both, and crates in reach are pushed on both.
- [ ] **Ladder:** hold the action and press up: the ladder extends on both, with its rungs and its height bar. Press down to retract. A double tap makes it carry on by itself. Another character can climb it, from either machine.
- [ ] **Piton:** with his head on, the rope he carries hangs in a loop from his body on both. Hold the action and push the stick: the aim arrow shows and turns with the stick, with its length for how hard, on both. Let go: his head flies out in an arc, spinning, with the rope paying out behind it, on both, and he shows as his feet only.
- [ ] **Piton (stuck):** the head sticks to a wall or a crate: it stops spinning and the rope is drawn as a line through its links, on both. Another character can cross the rope from either machine, and appears on it where they should on both. Tap the action twice: the head comes back, with the rope drawn behind it, and he gets his head back and his carried rope, on both.
- [ ] **Piton (a crate):** stick the head to a crate and push the crate: the head and the rope follow it on both.
- [ ] **Umbrella** and **Balloon:** hold the action: the open picture shows on both. (The Balloon has no string in any scene yet, so there's nothing more to check on him.)

## 12. Tiles that break
(Run **Make World Networked** and save: it adds a "Tile Network" object to a scene that has damageable tiles. Tiles on a moving tilemap aren't included.)
- [ ] A tile that crumbles when stood on, from the host's player: it shows the warning (darkening and shaking) on **both** editors, then breaks with the debris, sound or particles on both, and is gone on both.
- [ ] The same standing on it as the **joined** player: the warning shows on the joined editor too, and the tile goes on both.
- [ ] A tile with a time to come back returns on both editors, at the same time, and anything in its space is moved out.
- [ ] A tile that stays broken stays broken on both.
- [ ] Heat a tile with something that gives off heat: it glows, catches fire with the flames, burns blacker, spreads to the tiles beside it, and falls, all matching on both.
- [ ] A tile hit by something that breaks it by touch, or rolling into it, breaks on both.
- [ ] The debris on the joined editor falls and fades. It falls through the ground instead of landing on it, which is expected.
- [ ] Break some tiles, then join with the second editor: the tiles that were broken are already gone there, with no break effects.
- [ ] Tiles that have a Dig setting (Drill) and Dynamite settings (Dynamite) break on both when those characters are used, once those characters are networked (not yet).

## Known gaps: not expected to work yet
- The rope bridge, and enemies.
- Ability effects of every character except the Flashlight and the Bulb.

## When something fails
Note **which item**, **which editor did it**, **what each editor showed**, and any red errors in either Console.
