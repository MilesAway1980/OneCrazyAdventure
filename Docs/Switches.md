# Switches

Levers (`LeverSwitch`) and buttons (`ButtonSwitch`) work **switchable objects**: doors, drawbridges, moving platforms, liquid levels, lights and
the rest. A switch has a list of the things it switches. This page is about what a switch can do with that list. (The scene-view arrows from a
switch to its things are in [SwitchArrows.md](SwitchArrows.md).)

## Timing: one after another

By default a switch switches everything in its list at the same moment. Set **Delay Between Objects** to switch them **one by one**, in the
order of the list, with that many seconds between each. It's for a row of sconces or lights that come on in turn.

| Setting | What it does |
|---|---|
| **Delay Between Objects** | Seconds between one thing and the next. The first is switched at once, the second after this long, the third after twice as long, and so on. **0 (the default) switches them all together**, as before. |
| **Switch Off Order** | How they go off, when there's a delay: **All At Once** (the default), **Same Order** (one after another, the first first, with the same delay), or **Reverse Order** (the last first, so lights that came on from the left go out from the right). |

- The order is the order of the **list** on the switch. Reorder it in the Inspector to change it.
- Anything in the list that is missing, switched off, or left out by the switch doesn't take a turn, so there's no gap where it would have been.
  (A button set to Allow Partial Presses doesn't count the things that follow how far it's pressed, since they move with the pad.)
- Switching the other way **part-way through** stops the ones that haven't had their turn, and starts the other way. A lever thrown back
  half-way through a row of lights leaves the rest unlit, and puts out the ones that were on.
- A switch set to **Stays Active When On** keeps telling every thing that has had its turn, every frame, as it does without a delay.
- With the switch selected in the scene view, the arrows to its things are **numbered** in the order they're switched, while Delay Between Objects
  is more than 0.
- It runs on the host, as the switch does: the things that it switches are the ones that are sent to other machines, as before.

## What switches can work

Anything that's a **switchable object**: doors, drawbridges, moving platforms, liquid levels ([Liquids.md](Liquids.md)), fans ([Fan.md](Fan.md)),
lights ([Lights.md](Lights.md)), and the rest.

## For the code

- `SwitchSequence` (in `Assets/Scripts/Objects/Switches`) does it: `Begin`, `Tick` every frame while it's `Pending`, and `Cancel`.
  `LeverSwitch` and `ButtonSwitch` both use it, and any new switch that has a list of things can too.
