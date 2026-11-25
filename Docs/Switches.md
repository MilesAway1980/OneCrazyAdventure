# Switches

Levers (`LeverSwitch`), buttons (`ButtonSwitch`) and walk-in areas (`WalkSwitch`) work **switchable objects**: doors, drawbridges, moving platforms, liquid levels, lights and
the rest. A switch has a list of the things it switches. This page is about what a switch can do with that list. (The scene-view arrows from a
switch to its things are in [SwitchArrows.md](SwitchArrows.md).)

## Walk Switch

An area that switches things when a character walks into it, or out of it, with no lever to pull or pad to press. **GameObject > Walk Switch**
makes one: an object with a **Box Collider 2D** for the area (made a trigger for you) and the `WalkSwitch`. Resize the box, then give it the
switchable objects. (Or put the component on any object with a collider, or add a collider to one that has the component.) The collider needs
to be on a layer that collides with characters, which the default layer does.

| Setting | What it does |
|---|---|
| **Activate On** | **Enter** (the default): it goes by someone walking in. **Exit**: it goes by someone walking out. With several characters, Enter is the first one coming into an empty area, and Exit is the last one leaving, which empties it. |
| **Mode** | What it does when that happens. **Hold** (the default), **Toggle** or **Stays On**: see below. |
| **Stays Active When On** | On by default. While it's on it keeps switching its things on every frame, which doors and anything else that closes by itself need to be held open. Turn it off to switch things on once and let them time themselves out. |
| **Initial Delay**, **Delay Between Objects**, **Switch Off Order** | As on a lever or a button: see below. |

What each Mode does, for Enter and for Exit:

| Mode | Activate On: Enter | Activate On: Exit |
|---|---|---|
| **Hold** | On while anyone is in the area, off when the last has left. | On from when someone leaves, until someone walks back in. (It's not on before anyone has been in.) |
| **Toggle** | Flips each time someone walks in. Leaving does nothing. | Flips each time someone walks out. Walking in does nothing. |
| **Stays On** | Switched on the first time someone walks in, and never off again. | Switched on the first time someone walks out, and never off again. |

- Any character in the game counts, with any of their colliders. Characters that are put away, or part way through a teleporter's trip, don't.
  One put away while in the area counts as having left it.
- It runs on the host, as other switches do. There's nothing to network on the switch: the things it switches are the ones sent to other machines.
- Selected in the scene view, it draws arrows from the middle of the area to its things, like the others.

## Timing: one after another

By default a switch switches everything in its list at the same moment. Set **Delay Between Objects** to switch them **one by one**, in the
order of the list, with that many seconds between each. It's for a row of sconces or lights that come on in turn. **Initial Delay** is a
separate wait before the first, so the first can come on quickly (or after a pause) and the rest follow at their own pace.

| Setting | What it does |
|---|---|
| **Initial Delay** | Seconds before the first thing is switched. **0 (the default) switches the first at once.** The rest come after it, Delay Between Objects apart. |
| **Delay Between Objects** | Seconds between one thing and the next, counting from the first: the second is this long after the first, the third twice as long, and so on. **0 (the default) switches them all together**, as before. |
| **Switch Off Order** | How they go off, when there's a delay: **All At Once** (the default), **Same Order** (one after another, the first first, with the same delay), or **Reverse Order** (the last first, so lights that came on from the left go out from the right). |

- Example: Initial Delay 0.2 and Delay Between Objects 1 switch the first thing at 0.2s, the second at 1.2s, the third at 2.2s.
- An Initial Delay on its own (Delay Between Objects 0) waits, then switches everything together.
- When they go off one after another (Same Order or Reverse Order), the Initial Delay comes before the first going off too. With **All At
  Once**, they go off immediately.
- The order is the order of the **list** on the switch. Reorder it in the Inspector to change it.
- Anything in the list that is missing, switched off, or left out by the switch doesn't take a turn, so there's no gap where it would have been.
  (A button set to Allow Partial Presses doesn't count the things that follow how far it's pressed, since they move with the pad.)
- Switching the other way **part-way through** stops the ones that haven't had their turn, and starts the other way. A lever thrown back
  half-way through a row of lights leaves the rest unlit, and puts out the ones that were on.
- A switch set to **Stays Active When On** keeps telling every thing that has had its turn, every frame, as it does without a delay.
- With the switch selected in the scene view, the arrows to its things are **numbered** in the order they're switched, while there's an Initial Delay or a
  Delay Between Objects.
- It runs on the host, as the switch does: the things that it switches are the ones that are sent to other machines, as before.

## Signals: minimum and maximum, and adding up

A switch normally sends all or nothing: off is 0%, on is 100%. Every switch (lever, button and walk switch) has a **Signal** section that changes
what it sends, for the things that can be set to a part of their range: a door's height, how full a liquid is, how far along its path a moving
platform is, how bright a light is, how strong a fan is. These are the things that follow how far a button is pressed when it's set to Allow Partial
Presses.

| Setting | What it does |
|---|---|
| **Signal Mode** | How this switch's signal is put with the signals of the other switches that work the same thing: **Min/Max** (the default), **Add** or **Subtract**. |
| **Minimum Signal** | Min/Max only. What the things get while the switch is off. **0 (the default) is the ordinary off.** |
| **Maximum Signal** | What the things get while the switch is on. **100 (the default) is the ordinary on.** In Add and Subtract, it's how much the switch adds or takes off. |

### Min/Max: the last switch wins

The switch sends its own signal: the Minimum Signal while it's off and the Maximum Signal while it's on. With Minimum 20 and Maximum 80 it sends
**80%** while it's on and **20%** while it's off. A pool with Minimum 50 only goes down half way when the lever is off.

A thing that several Min/Max switches work goes by **whichever spoke last**, as it always has. (Don't give one thing two switches like this if you want
them to agree.)

### Add and Subtract: the switches add up

A thing keeps track of every Add and Subtract switch that works it, and goes by the **total**:

- An **Add** switch adds its Maximum Signal to the total while it's on, and nothing while it's off.
- A **Subtract** switch takes its Maximum Signal off the total while it's on, and nothing while it's off.
- The total is kept between 0% and 100%.
- A **Min/Max** switch on the same thing sets what the total starts from (its signal, or 0), and the Add and Subtract switches count on top of that.

Example: four levers that all work one pool, each set to Add with a Maximum Signal of 25 (and the pool's On Level 100% and Off Level 0%). All off,
the pool is empty. One lever on, it's 25% full; two, 50%; three, 75%; all four, 100%. Turn one off, and it goes back down 25%. Give them different
amounts (10, 20, 30, 40) and any combination adds up to its own level.

- **Buttons** work too: a button set to Add with Allow Partial Presses adds its Maximum Signal times how far down the pad is. So **four buttons**, each
  25, with a character on each, take a door up 100%, instead of four characters standing on one button adding their weights. A button without
  Allow Partial Presses just adds its Maximum Signal while it's pressed far enough.
- A **walk switch** adds its Maximum Signal while it's on.
- **Things that can only be on or off** (no part-way) are on once the total reaches **100%**. Five switches that are each Add 20 are all needed for a
  gate that can only open or shut, while a door that opens by an amount opens a fifth with each.
- **Stay Switched On**, on the thing, keeps the most it has been given, so it never comes back down as switches go off. Switch Off Time puts it
  back to nothing after that long without a signal. Leave both off for something that follows the total.
- A switch that **stays active** (a lever's, button's or walk switch's Stays Active When On) matters to things that switch themselves off every
  frame unless they're told again, such as a force: tell it every frame, or it only gets the signal for one.
- The signals work with a delay between things (above): each thing gets the signal in its turn.
- It runs on the host, and the amount is worked out there. Nothing about it is networked beyond what the things already send.

### Notes

- Leave the signals at 0 and 100 in Min/Max and nothing changes. Anything other than that matters to a **platform**: one that gets 100% is switched on
  and goes round its path as Path End says, but one that gets less is taken that far along the path and held, as a half-pressed button does.
- **Minimum Signal above 0 is sent all the time** the switch is off, starting from when the game does, and not only when it's thrown back, so the things
  rest at that part. It only applies in Min/Max: Add and Subtract send nothing while off.
- Maximum below Minimum is allowed in Min/Max, and turns it round: the thing goes to the larger number while the switch is off.

## What switches can work

Anything that's a **switchable object**: doors, drawbridges, moving platforms, liquid levels ([Liquids.md](Liquids.md)), fans ([Fan.md](Fan.md)),
lights ([Lights.md](Lights.md)), and the rest.

## For the code

- `SwitchSequence` (in `Assets/Scripts/Objects/Switches`) does it: `Begin`, `Tick` every frame while it's `Pending`, and `Cancel`. `Begin` can
  take an amount, who is sending it and how it combines (`SignalMode`).
- `SwitchableObject.Signal(source, mode, on, amount)` is how a switch tells a thing: the thing keeps each Add and Subtract switch's share and
  works out the total (`SwitchSignal` has what the switches share: the amounts to send and holding a lowest signal). A new switch should use it.
  `LeverSwitch`, `ButtonSwitch` and `WalkSwitch` all use these, and any new switch that has a list of things can too.
