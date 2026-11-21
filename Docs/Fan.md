# Blower fan

A fan that blows things along a direction, harder the closer they are to it. It needs no collider: it finds what is in its reach by itself.
Anything with a Rigidbody2D in the area is pushed by a force, and the force falls away with the distance from the fan along a curve.
A character pushed by a fan keeps the push as momentum, so it's hard to walk into a strong one (see [Movement.md](Movement.md)).

## The area and the force

| Setting | What it does |
|---|---|
| **Angle** | The way it blows, from straight up: 0 is up, 90 is right, 180 is down, 270 is left. It turns with the object. |
| **Strength** | The force at the fan itself. It's a force, so the same strength moves something light further than something heavy. |
| **Range** | How far out it blows, in units. |
| **Falloff** | The strength at each distance, as a curve: the bottom is how close to the fan (1 at the fan, 0 at the end of the range) and the side is the fraction of the strength. A curve that rises, which is every preset, is strongest at the fan. |
| **Shape** | **Column** (a strip of a fixed width) or **Cone** (a wedge). **Width** and **Cone Angle** size them. |
| **Offset** | Shifts the whole area, across and up, in the fan's own space. |
| **Affects / Blocked By Ground** | Which layers it pushes, and whether a wall between the fan and something stops it. |

## Switching it

The fan is a **switchable object**, so a lever or a button can change how hard it blows. Its power is a **multiple of Strength**:

| Setting | What it does |
|---|---|
| **Power When Off** | How hard it blows while it's switched off: 1 is the Strength as set, 0 is off, 2 is twice as hard. **The game starts here.** |
| **Power When On** | How hard it blows while switched on. 0 turns it off, 1 is as set, 2 is double, and so on. |
| **Change Seconds** | How long it takes to change by a whole Strength (from off to as set, or as set to off). 0 changes at once. A change of less takes less time. |

Both powers default to **1**, so a fan that no switch changes blows exactly as it always has. To use a switch, set the two powers:

- **A fan that a lever turns on:** Power When Off 0, Power When On 1.
- **A fan that a lever shuts off:** Power When Off 1, Power When On 0.
- **A fan that a switch turns up:** Power When Off 1, Power When On 2 (or any more). A switch can also turn it down: Power When On 0.5.
- A button set to **Allow Partial Presses** sets the power by how far it's pressed, anywhere between the two powers.
- **Stay Switched On** and **Switch Off Time** (on the fan, since it's a switchable object) work as they do on a door.
- The fan's picture turns as fast as it blows: faster when it's harder, and still when it's off. (If it has two pictures to flip between.)
- A switch with a **Delay Between Objects** can start a row of fans one after another.
- In a network game, the host pushes things, and sends the power so that the picture turns at the right speed (`BlowerFanNetwork`, added by
  **Tools > Network > Make World Networked** to fans that a switch changes).
