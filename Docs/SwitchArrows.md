# Switch arrows

In the scene view, while a **lever** or **button** that switches things is **selected**, it draws an **arrow to each of the things it
switches**, so it's clear what is wired to what without opening anything. They're only drawn for the selected one, as the range of a
swaying light is, so the scene isn't covered in them.

- The arrows are **yellow**.
- Each arrow goes from the lever or button to the thing it switches. It stops a little short at both ends, with a small head. If the two
  are too close for an arrow, a small ring is drawn on the switch instead.
- Missing entries in the list of things to switch are skipped.
- An arrow points to the thing that moves or changes, not just to the object the script is on: a door's arrow goes to the door, a drawbridge's
  to the bridge, a moving platform's to the platform, and a liquid switch's to the middle of its Liquid Area.
- They're scene-view gizmos, so they also show in the Game view if Gizmos is on there, and they aren't in a built game.

Turn them all off or on with **Tools > Gizmos > Show Switch Arrows** (it's remembered between sessions). The Gizmos button in the scene
view hides them along with the other gizmos.

## For the code

- `SwitchLinkGizmo` (in `Assets/Scripts/Objects`) draws them: `SwitchLinkGizmo.Draw(from, targets, selected)`.
- `InteractableObject` draws them for anything that gives it targets, by overriding `SwitchTargets` (`LeverSwitch` does). `ButtonSwitch`
  isn't an `InteractableObject`, so it calls the same drawing itself.
- A switchable object can say where its arrows should point by overriding `GizmoPoint` (`SwitchableObject`).
