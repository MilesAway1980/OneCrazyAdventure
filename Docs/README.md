# OneCrazyAdventure docs

Notes for working on the game. They're meant to be kept up to date as things change.

## How the game works

| Doc | What it covers |
|---|---|
| [Movement.md](Movement.md) | How a character's running, jumping, falling and floating are worked out from its weight and material, and how to add a character, a liquid or a material. |
| [MovingPlatform.md](MovingPlatform.md) | The moving platform switchable object: path, corners, movement methods, and how levers and buttons run it. |
| [Drawbridge.md](Drawbridge.md) | The drawbridge switchable object: how to set one up, and how speed and weight make it slam or settle. |
| [CharacterRoles.md](CharacterRoles.md) | What each character does, what puzzles it solves and how it helps the others. |
| [WorldMap.md](WorldMap.md) | The map of the game and the order the characters are found in. |
| [TutorialYard.md](TutorialYard.md) | The first area, and the editor tool that builds it. |

## Multiplayer

| Doc | What it covers |
|---|---|
| [NetworkAudit.md](NetworkAudit.md) | What each script needs for playing over a network, where the host runs the game and everyone else only shows it. |
| [NetworkTestChecklist.md](NetworkTestChecklist.md) | A step-by-step test with two editors (one hosting, one joining), with known gaps. |
