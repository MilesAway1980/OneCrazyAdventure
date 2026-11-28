# OneCrazyAdventure docs

Notes for working on the game. They're meant to be kept up to date as things change.

## How the game works

| Doc | What it covers |
|---|---|
| [Movement.md](Movement.md) | How a character's running, jumping, falling and floating are worked out from its weight and material, and how to add a character, a liquid or a material. |
| [CharacterSorting.md](CharacterSorting.md) | How characters are kept from flickering over each other when they stand together: an automatic order for each, and the sorting layers to set up. |
| [Liquids.md](Liquids.md) | Making bodies of water: Liquid Areas (freeform outlines with a fluid and an overlay), and what responds to them. |
| [MovingPlatform.md](MovingPlatform.md) | The moving platform switchable object: path, corners, movement methods, and how levers and buttons run it. |
| [Drawbridge.md](Drawbridge.md) | The drawbridge switchable object: how to set one up, and how speed and weight make it slam or settle. |
| [CharacterRoles.md](CharacterRoles.md) | What each character does, what puzzles it solves and how it helps the others. |
| [Teleporter.md](Teleporter.md) | The teleporter pad: pairs that are one way or two way, single use or repeated use, and how a character is sent. |
| [Lights.md](Lights.md) | Lights: the LightSource (lit and unlit looks), sconces, and the Switch Light that lets a lever or button light them, dim them, or chain them one by one. |
| [Fan.md](Fan.md) | The blower fan: its area, strength, falloff and offset, and how a switch turns it up, down or off. |
| [Switches.md](Switches.md) | What levers and buttons can do with the things they switch: timing them one after another, and more as it's added. |
| [SwitchArrows.md](SwitchArrows.md) | The scene-view arrows from levers and buttons to the things they switch, and how to turn them off. |
| [Hints.md](Hints.md) | The Hint component: how to put a hint in the world with a collider and one component, and how the words and the on/off setting work. |
| [SwayRotation.md](SwayRotation.md) | An object that swings back and forth between two angles, for a spotlight that doesn't stay pointing one way: speed, easing, randomness and wobble. |
| [SwitchHint.md](SwitchHint.md) | Showing who controls which character: a pointer above its head with one player, and a P1 to P4 tag with several. |
| [WorldMap.md](WorldMap.md) | The map of the game and the order the characters are found in. |
| [TutorialYard.md](TutorialYard.md) | The first area, and the editor tool that builds it. |

## Multiplayer

| Doc | What it covers |
|---|---|
| [NetworkAudit.md](NetworkAudit.md) | What each script needs for playing over a network, where the host runs the game and everyone else only shows it. |
| [NetworkTestChecklist.md](NetworkTestChecklist.md) | A step-by-step test with two editors (one hosting, one joining), with known gaps. |
