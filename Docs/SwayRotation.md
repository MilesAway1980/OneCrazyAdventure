# Sway Rotation

An object that swings back and forth between two angles, so that a light on it, or anything else, doesn't stay pointing the same way.

## Making a swaying spotlight

1. Make an object, and give it a **Light2D** set to **Spot**. Turn it to point where the middle of the swing should be.
2. Add the **Sway Rotation** component to the same object (or to a parent of it: the parent's own point is what turns).
3. Set the angles and the speed. That's all that's needed. The other settings make it less regular.

A spotlight points along its object's **up**, so it turns about the object's point. To swing a light about somewhere other than its own
position, put the light on a child, offset from a parent that has the Sway Rotation on it.

## Settings

| Setting | What it does |
|---|---|
| **Min Angle / Max Angle** | The two ends of the swing, in degrees from how the object was turned when the game started. Positive is counter-clockwise, as Unity's rotation is. Default -30 to 30. |
| **Speed** | How fast it sweeps, in degrees a second, on average. |
| **Smoothness** | 0 is the same speed all the way across; 1 slows right down into each end and is quickest in the middle. |
| **End Randomness** | Each sweep stops short of the end by a random amount, up to this many degrees. It never goes past the two angles. |
| **Speed Randomness** | Each sweep is a little faster or slower than the one before, by up to this fraction of Speed. |
| **Pause At Ends / Pause Randomness** | How long it holds at each end, and a random extra on top, in seconds. |
| **Wobble / Wobble Speed** | A small, smooth drift added to the angle, up to this many degrees either way, changing this many times a second. It stops the swing settling into a pattern. |
| **Random Start** | Starts part of the way through a sweep, in either direction, so several lights next to each other aren't in step. |

In the scene view, with the object selected, the swing is drawn from its point: the two ends and the arc between them in yellow, and the
way it was pointing to begin with in white. It's drawn from how the object is turned now, so it shows the middle of the swing while the
game isn't running.

## Things to know

- It's decoration. Nothing is moved by it except the object's turn, so don't use it for a platform or anything that has to push things.
- The randomness is different on each machine in a network game, so a light that sways isn't in step between them.
- Switching the component off holds the object where it is. Switching it on carries on from there.
