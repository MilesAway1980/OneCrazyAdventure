# Showing who controls which character

When characters are close together it's hard to tell which one you're controlling. What's shown depends on how many players there are.

## One player: a pointer

When a player takes over a character, a small white triangle with a dark rim points down at it, fading in and out above its head over
about a second, bobbing slightly. That's the **switch hint** (`SwitchHint.cs`).

It's shown whenever the character a player controls changes: switching to the next or previous one, being given one on arriving in
the game, or being given another when theirs is put away (at a hub, say). If the player switches again before it has finished, it's
taken off the first character at once.

## Several players: P1, P2, P3, P4

With more than one player in the game, **no pointer is shown**. Instead every character a player is controlling has a small tag over
its head all the time, **P1** to **P4**, in that player's colour (P1 red, P2 blue, P3 green, P4 yellow), with a dark rim so it can be
read over light and dark. It goes when the player moves on to another character, and appears on the new one. That's the **player tag**
(`PlayerTag.cs`), made and taken away by `PlayerTags.cs`.

How many players there are is looked at every frame, so when a second player joins, the tags start and the pointer stops, and when
only one is left, the tags go and the pointer comes back.

### The numbers

The host's numbers are used everywhere. Its own players are numbered as they are on its screen (`Player.playerNum`), and players on
other machines follow them, in the order they joined. The host works out how many players there are and which number has which
character, and sends it to every machine (`ConnectionPlayer.PlayerTotal` and `controllerNumbers`), so all machines show the same
number over the same character. A machine that has joined shows the host's numbers, not its own.

## How it works

- Neither is part of the character: each is an object of its own that follows the top of the character's picture, so a character that
  rolls or spins doesn't turn it, and nothing that looks over a character's own parts sees it.
- The triangle and the letters are drawn once in code (the letters in a small pixel font, in `PlayerTag.cs`), so there's no picture or
  font to keep with the project.
- `SwitchHint.cs` has the pointer's timings and size as constants at the top, and `PlayerTag.cs` has the colours, size and height of
  the tags.
- The pointer is made by the `Player` on the machine the player is sitting at, when its character changes. Nothing about it is sent
  over the network.
