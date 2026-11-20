# Hints

A **Hint** is a message shown on screen while a character is in an area, if hints have been turned on. The Tutorial Yard uses them to
teach the controls and the puzzles, but a Hint can go anywhere.

## Making one

The quick way: **GameObject > Hint**. It makes an object with a Box Collider 2D for the area and the Hint component. Move and resize
the box where the hint should show.

By hand: make a GameObject, add a **Box Collider 2D** (or any other 2D collider) and size it, then add the **Hint** component. The
collider is made a **trigger** for you, so characters walk straight through it. It doesn't matter which you add first: if the collider
comes second, it's made a trigger as soon as you look at the Hint in the Inspector, and when the game starts.

The collider needs to be on a layer that collides with the characters, which the default layer does.

## What it shows

- **Topic** is a piece of text that names the hint to show. It's matched against the **Hints** list in the master text (**Tools >
  Game Text > Open Master Text**), where each entry has a topic and its words, and the Hint shows the words of the entry with the
  same topic. Capitals and spaces at the ends don't matter. To add a hint, add an entry to that list and give a Hint its topic: no
  code is needed, and there's no list to scroll, however many there are. The ones that start there are Move, Jump, SwitchCharacter,
  Interact, Carry, Button, SwitchOnButton, Teamwork and Hub.
- **If nothing matches** the topic, the Hint's Inspector shows a **warning** (and says if there's no topic at all), and the Console
  warns when the game starts. It shows nothing in the game. When it does match, the Inspector shows the words it will say. **Pick a
  Topic...** lists the topics that exist, if you'd rather not type, and **Open Master Text** goes to where they're written.
- **Custom Text** is optional. If it has anything in it, it's shown instead of looking up the topic, for a hint that's only used
  once. Controls can be put in curly brackets in any hint: `{move}`, `{jump}`, `{action}`, `{interact}`, `{next}` and `{previous}`.
  They're filled in with the buttons that player is using, such as "Space" or "Button South", so a hint always names the player's
  own controls.
- **Only Once** shows it only the first time someone goes in, and not again after they've left.

## When it shows

- Only while hints are **turned on**. They start off, so players who like working things out aren't told, and a player who is stuck
  turns them on from the hub menu. The choice is remembered between sessions.
- While a character in the **party** is inside the collider. It goes when they all leave, and if hints are turned off while it's
  showing, it goes at once. It's shown in the words of the player controlling the character that came in.
- On a machine that has joined someone else's game, who is in the area is worked out from where the characters are, since nothing
  touches anything there.

## Code

`Assets/Scripts/World/Tutorial/Hint.cs` (the component, `TutorialHints` for the on/off setting, and `ControlNames` for the buttons).
`Assets/Scripts/GameText.cs` has the list of hints (`hints`, each a `HintText`) and the lookup (`TryGetHint`).
`Assets/Scripts/Editor/HintEditor.cs` has the Inspector and the menu.

This was called `TutorialPrompt` before, and its topic was picked from a list. The script's identity (its `.meta`) was moved with
it, so hints in scenes kept working. The old nine texts are in the master text's list under the topics Move, Jump, SwitchCharacter,
Interact, Carry, Button, SwitchOnButton, Teamwork and Hub.
