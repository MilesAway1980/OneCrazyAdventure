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
| **Mode** | What it does when that happens. **Hold** (the default), **Toggle**, **Stays On** or **Timer**: see below. |
| **Seconds To Complete** | Timer only. How many seconds someone has to be in the area, without leaving, for it to be complete. |
| **Seconds To Reset** | Timer only. How many seconds it takes to go back to the start once the area is empty. 0 is at once. |
| **Stays Completed** | Timer only. Once it's complete it stays complete, and its things stay on, whoever leaves. |
| **Stays Active When On** | On by default. While it's on it keeps switching its things on every frame, which doors and anything else that closes by itself need to be held open. Turn it off to switch things on once and let them time themselves out. |
| **Initial Delay**, **Delay Between Objects**, **Switch Off Order** | As on a lever or a button: see below. |

What each Mode does, for Enter and for Exit:

| Mode | Activate On: Enter | Activate On: Exit |
|---|---|---|
| **Hold** | On while anyone is in the area, off when the last has left. | On from when someone leaves, until someone walks back in. (It's not on before anyone has been in.) |
| **Toggle** | Flips each time someone walks in. Leaving does nothing. | Flips each time someone walks out. Walking in does nothing. |
| **Stays On** | Switched on the first time someone walks in, and never off again. | Switched on the first time someone walks out, and never off again. |

**Timer** goes by how long someone is in the area, not by their coming or going (so Activate On is hidden for it):

- It **fills while anyone is in the area**, taking **Seconds To Complete**, and it's **on once it's full**, for as long as someone is still in it. When the area is
  empty it **drains back** to the start over **Seconds To Reset**, going off the moment it's no longer full. Someone who leaves and comes back soon carries on
  from where it had got to, and it only starts again from the start if they were gone long enough for it to empty. A **Seconds To Reset** of 0 starts again at once.
- With **Stays Completed**, it's on for good once it's full: it doesn't drain, whoever leaves (a door that stays open, lights that stay on).
- Things that **go exactly with the switch** (a door with **Use Own Speed** off) follow how full it is, so the door opens as they stand there. Everything
  else is switched on when it's complete and off when it's not, and the delays between things work on those.
- Several characters in the area count as one: it fills at the same speed for one as for three.
- It's hidden if the object has no sprite, and works from the **Box Collider 2D** alone: put it where you want the invisible area, and size the box.

- **On for good.** A Walk Switch in **Stays On** mode, or a **Timer** with **Stays Completed**, holds the things it has switched on **for good**: once it has
  switched one on (after its delays), no other switch that works that thing can switch it off again, move it, or fight it, whatever they send. A button
  pressed or a lever thrown afterward does nothing to it. (Before then they work it as usual.) If a thing has a partial Maximum Signal from it, it's held at that.
  In the code this is `ILatchedSwitch`, which `SwitchableObject.Signal` remembers, so another switch's signal is ignored in `Resolve`.
- Any character in the game counts, with any of their colliders. Characters that are put away, or part way through a teleporter's trip, don't.
  One put away while in the area counts as having left it.
- It runs on the host, as other switches do. There's nothing to network on the switch: the things it switches are the ones sent to other machines.
- Selected in the scene view, it draws arrows from the middle of the area to its things, like the others.

## Levers you have to hold

Ordinarily a press of Interact sets a lever swinging all the way to the other state by itself, taking its **Seconds To Move**. With
**Hold To Operate** on the lever, the character has to **keep the Interact button held** for it to move. The Inspector shows only the settings
that apply:

| Setting | What it does |
|---|---|
| **Hold To Operate** | Off (the default) is the ordinary lever. On: the lever moves only while it's held. |
| **Seconds To Move** | Ordinary lever only. How many seconds it takes to swing from off to on by itself after a press (and back). |
| **Seconds To Hold** | Hold To Operate only. How many seconds it has to be held to take the lever all the way to the other state. |
| **Seconds To Return** | Hold To Operate only. How many seconds it takes to go all the way back to where it was, when it's let go part of the way (or, with Spring Loaded, from on). |
| **Spring Loaded** | Hold To Operate only. The lever never stays on. It's on only while it's held all the way, and the moment it's let go it's off and swings back. |

- **Held, it goes to the other state; let go, it goes back to the one it was in.** A lever with Seconds To Hold 10 is on once it's been held for 10 seconds
  in a row. Let go at 6 and it falls back to off. Once it has got all the way there, that's its state, and it has to be let go of and taken hold of
  again to go back. (**One Time** works as before: once it's on, it can't be used again.)
- **Spring Loaded** makes it momentary, like a button: held all the way, it's on, and whoever is holding it keeps hold of it (he isn't let go of at the
  end, as he is on an ordinary hold lever). The moment he lets go, or moves away, it's off, what it switches goes off at once, and the lever swings back
  in Seconds To Return. It never stays on, so One Time doesn't apply to it, and **Stays Active When On** still reapplies the switch while it's held on.
- **It follows how far it is.** The things that can be set to a part of their range (a door, a liquid level, a moving platform, a light, a fan) are
  given how far the lever has got, the same way a button gives them how far down its pad is, and in the same **Signal Mode** and
  Minimum and Maximum Signal. Held 5 seconds of 10, a door is half open. Let go, and it closes again as the lever falls back.
- **Things that can only be on or off** (a gate) switch when the lever gets all the way there, and switch back when it's pulled all the way back.
  The delays between things (below) and **Stays Active When On** work on those, as before.
- **Moving away** (out of the lever's trigger area), being put away, or letting go all count as letting go.
- **One character at a time** works the lever. Another has to wait until it's let go of.
- **The character's Interact animation shows the whole time** he's working it, and goes back to normal when he lets go. (See [Animation.md](Animation.md).)
  An ordinary lever does the same from the press until it has swung all the way.
- If a character keeps letting go when they shouldn't, tick **Log Holding**: it writes to the Console when they take hold and when they let go, and why.
- On another machine in a networked game the lever shows where the host's is, as it moves.

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

## How far a button's pad goes down

A button (`ButtonSwitch`) has a **pad** that goes down when weight is put on it, into a **base** around it. **Press Percent** is how far the pad
goes down when it's pressed all the way, as a percentage of the way from where it sits to the top of the base:

| Press Percent | What the pad does |
|---|---|
| **100** (the default) | Goes down until its top is flush with the top of the base, a hair (0.02 units) above it. |
| **50** | Goes down half of that way. |
| **0** | Doesn't move. It only has to be stood on to count as pressed all the way, taking its Resistance time, and the Return Time to come back. |

- The base is found when the game starts: any other solid collider on the button that overlaps the pad and has its top under the pad's top.
  If there's none, the pad's own height is the way, so a taller pad goes further.
- The pad stops a hair **above** the base on purpose. If it went lower, whoever was standing on it would be standing on the base, and the pad
  would drop out from under them, find nothing on it, come up, and go down again: the button would bounce, and the things it works with it.
- The pad never moves faster than a gentle speed, so a long way takes longer. A very short way (under 0.05 units) is only timed.
- Selected in the scene view, a yellow line shows how far the pad goes, and a green one where it switches on.

## What switches can work

Anything that's a **switchable object**: doors, drawbridges, moving platforms, liquid levels ([Liquids.md](Liquids.md)), fans ([Fan.md](Fan.md)),
lights ([Lights.md](Lights.md)), and the rest.

**Doors** (`SwitchDoor`) have **Use Own Speed**. On (the default, as doors have always been), it opens and closes at its **Speed**, in units a second, whatever
the switch does. Off, **Speed** is hidden and the door goes exactly with the switch, one to one: it's open by the same fraction as the switch is on, moving as
fast as the switch does, from the moment it starts. It follows the Minimum and Maximum Signal like anything else that takes an amount.

- **A lever**, ordinary or one that has to be held, gives it how far along its swing it is. An ordinary one doesn't need Hold To Operate: set a long
  **Seconds To Move** for a slow lever, and the door goes with it, and turns round if the lever is pressed again part way.
- **A button** gives it how far down the pad is: half way down, half open, and as the pad comes back up, the door closes by the same amount. This is whether or not
  **Allow Partial Presses** is ticked on the button: that only decides whether the *other* things it works that can take an amount follow the pad. **Percent
  Pressed To Activate** doesn't apply to a door that follows it.
- **A walk-in area** is only on or off, so there's no halfway, and it takes the door all the way at once.

In the code, a switchable object says it does this with `FollowsSwitchPosition`, and a lever (`LeverSwitch.PassOnPosition`) and a button
(`ButtonSwitch.PassOnAmount`) give those things their position in the same way as for anything that takes an amount.

## Needing all of the switches

Tick **Needs All Switches** on a switchable object (a door, a gate, a light, anything with it in its Inspector) and, when more than one switch works it, **every
one of them has to be on before it does anything**: a door with four switches stays shut until all four are on, and goes shut again as soon as one goes off.
(Add mode, where the switches add up to a total, is the other way to combine them: it gives a share for each.)

- Which switches it waits for are the levers, buttons and Walk Switches that have it in their lists, and that are switched on in the scene. A switch that is
  switched off or removed isn't waited for. With one switch, it makes no difference.
- **A switch is on from the moment it's sending something**, not only at the end: a button pressed any of the way down, a lever held or swung away from its
  start, a Walk Switch that has gone on. (A thing that follows how far a switch is gets nothing until every switch is on, and then goes by what they say.)
- **Switches that subtract** (Signal Mode: Subtract) aren't waited for, as they're meant to take something away and not to be on.
- Once they're all on, it's switched as it always is: by what the last of them said in Min/Max, or by the total in Add and Subtract. A switch that is
  **on for good** (see Walk Switch above) counts as on, but it can't hold the thing open if one of the others goes off.

## For the code

- `SwitchSequence` (in `Assets/Scripts/Objects/Switches`) does it: `Begin`, `Tick` every frame while it's `Pending`, and `Cancel`. `Begin` can
  take an amount, who is sending it and how it combines (`SignalMode`).
- `SwitchableObject.Signal(source, mode, on, amount)` is how a switch tells a thing: the thing keeps each Add and Subtract switch's share and
  works out the total (`SwitchSignal` has what the switches share: the amounts to send and holding a lowest signal). A new switch should use it.
  `LeverSwitch`, `ButtonSwitch` and `WalkSwitch` all use these, and any new switch that has a list of things can too.
