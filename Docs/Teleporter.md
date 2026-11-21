# Teleporter

A pad that sends a character who steps on it to another pad. Two pads make a **pair**.

## Making a pair

The quick way: **GameObject > Teleporter Pair**. It makes two pads, linked, under one object. Move them where they're wanted, and give each
its own picture.

By hand: make a GameObject for each pad, give each a **Teleporter** (its collider is added for you, if it has none, and made a trigger),
and give one pad the other as its **Destination**. **The settings go on the pad that has the Destination.** The other pad needs nothing set.

## The options

| Setting | What it does |
|---|---|
| **Destination** | The pad a character who steps on this one is sent to. |
| **Direction** | **One Way**: only this pad sends, and the destination only receives. **Two Way**: both pads send, each to the other. |
| **Uses** | **Single Use**: the first time anyone is sent, in either direction, the **pair is used up**, and neither pad does anything again. **Repeated Use**: it works again after the cooldown. |
| **Speed (seconds)** | How long the trip takes. **0 is instant** (the default). Anything over 0 is the time the **camera takes to pan** from this pad to the destination. The character can't be seen while it goes, and arrives at the end. |
| **Cooldown** | Repeated Use: how many seconds after someone is sent before either pad works again (default 1). |
| **Arrival Offset** | Where a character who arrives at a pad is put, from that pad's position, in its own directions. The default, a little above it, lands them on it. |
| **Used Up Colour / Cooling Colour** | Tints the pad's sprite once the pair is used up, and while it's cooling down. |
| **On Sent / On Arrived** | Events, with the character, for a sound or an effect. Sent is called on the pad that sends, and Arrived on the one it arrives at. |

## How a character is sent

- When a character steps on a pad that's ready, it's put at the destination, at rest, and the move is sent to every machine in a network game.
- With a **Speed** over 0, the character is **in transit** for that many seconds instead. It can't be seen, can't be touched, can't be steered
  and can't do anything, but it's still in the game and still the character of whoever controls it. It's moved across from the source pad to
  the destination, easing in and out, and since the cameras follow the player's character, they pan across with it. At the end it's put
  down at the destination, as for an instant trip. The player can't switch away from it on the way, or the camera would be left behind.
  The cooldown starts as the trip does, and the trip's time is added to it. If the pad is switched off on the way, the character is put down at
  the destination at once.
- Whatever the character is **carrying** goes with it.
- A character who has just arrived isn't sent straight back: it has to **step off the pad first**, then on again. (Otherwise a Two Way pair would
  bounce it between the pads.)
- Any character in the game is sent, whether a player is controlling it or not.
- A pad with no Destination, and that no other pad sends back to, logs a warning and does nothing.
- In a Two Way pair, the other pad sends back to this one by itself, unless it has a Destination of its own, which it sends to instead.

## In the scene view

With a pad selected, an arrow is drawn to where it sends (and one back, for Two Way), with a small ring at each arrival point.

## Not done yet

- On a machine that has joined someone else's game, the characters are moved by the host, but the pad's **used-up and cooling tints** aren't
  sent, so they only show on the host.
- A character on a trip is shown and hidden on other machines too (`CharacterNetwork` sends whether it's in transit), and its position is
  sent as it moves, so it flies across on their screens as it does on the host's, if they're watching that place.
- Characters on a ladder or holding a grab point aren't let go; they're moved as they are.
