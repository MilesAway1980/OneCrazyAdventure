# Carrying things

A character with **Able To Carry Objects** can pick up a **Carryable Object** (a crate, say) with Interact, carry it, and put it down with Interact
again. (See also [CharacterRoles.md](CharacterRoles.md) for who can carry.)

## Making something carryable

Add a **Carryable Object** to it. That's all: it gets what it needs.

| It needs | What for | How it gets it |
|---|---|---|
| **Rigidbody2D** | The thing that's carried and dropped. | Required: adding the Carryable Object adds it. A body that still has Unity's defaults is given a crate's weight (mass 3, gravity 2, Continuous collision). |
| **Fixed Joint 2D** | What it hangs from the carrier's carry point by. | Required: adding the Carryable Object adds it, and it starts switched off. |
| **A solid collider** | Its shape. | Added (a Box Collider 2D the size of its picture) if it has no solid collider. |
| **A trigger collider** | How near a character has to be to pick it up. | Added (a Box Collider 2D 1.6 times the size of its picture) if it has no trigger collider. |
| **Pushable Solid** | So a character walking up to it isn't stopped short, as if by a wall. | Required: adding the Carryable Object adds it. |

The colliders are added when the Carryable Object is first added (or **Reset** is chosen on it), and are sized from its picture, so a picture on the same
object gives the right size. Change them to fit. Something it already has isn't added twice.

### Walking up to it

Characters stop walking when their wall detectors sense a wall 0.75 units ahead, which is how they avoid walking into the side of a tile. The detectors
see anything on the Ground layer, a crate included, so without the **Pushable Solid** a character would stop a good way short of the crate, as though there
were an invisible wall in front of it. The detectors look straight through anything with a Pushable Solid, so they walk right up to the crate, and
their walking pushes it, as it would any pushable. (A crate made before this was required gets one when the game starts.)

It also gets, as the game starts, what lets it ride on platforms, float or sink in a liquid, and come back to where it started if it falls into a
Kill Zone.

### The joint's Auto Configure Connected Anchor

The Fixed Joint 2D must have **Auto Configure Connected Anchor off**. With it on, Unity works the anchor out from where the object is when it's picked
up, so it stays where it was in relation to the carrier instead of going to the carry point. It's switched off for you: when the Carryable Object is added,
on any carryable object in the open scene (the Console says when it does), and in the game before an object is picked up. So the joint can't be left
wrong.

## The weight of what's carried

While a character holds something, its mass is **added to his own**, and he behaves as if he were that heavy:

- **Buttons and crumbling floors** weigh the two together. (Buttons used to add the carried object's mass by itself, and no longer do, as it's in his.)
  It's also in how hard he's pushed, and how much a fall hurts him.
- **His walking and jumping are cut by his own mass over his mass with the load.** With a mass of 30 carrying 40, that's 30 over 70: a walking speed of 1.5
  becomes 0.6428. Speeding up and slowing down are cut by the same fraction, so he takes the same time to get to his top speed and to stop. The jump
  *height* is cut by the fraction too (so he leaves the ground at its square root). Climbing and swimming aren't touched.
- **Carry Strength** on the character, from 0 to 100, is the percentage of the weight he **doesn't feel**. 0 is all of it. At 50, a crate of mass 200 counts as 100,
  and one of 70 as 35. At 100 nothing weighs on him. So with a Carry Strength of 50, a character of 30 carrying 70 is 30 + 35 = 65, and his speed is 30 over 65.
- **Maximum Carry Weight** on the character is the heaviest thing he can pick up, by the thing's own mass (before Carry Strength takes any off). Something
  heavier doesn't move or join to him when he presses Interact: he does his Interact animation, reaching for it, and that's all. 0 is no limit. If there's
  something lighter in reach as well, he picks that up instead.
- It's his real `Rigidbody2D` mass that changes, only while he holds something. Anything else that sets his mass (the Brick going solid) is taken as his own
  mass from then on.

## The carrier's carry point

A character who can carry things needs a **Carry Attachment Point**: an empty child object with a **Rigidbody2D**, set to Kinematic (it's made
Kinematic for you, and the Console says so). The carried object's joint connects to it, and a joint can only connect to a body, which is why it has
to be a Rigidbody2D and not just a Transform. It's kinematic so that the object's weight doesn't push the carrier back. The object's centre is
taken to the point, so put the point where the middle of what's carried should be.
