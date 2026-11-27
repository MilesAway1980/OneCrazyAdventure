# Character animation

Each character can have an **Animation Set**: named clips, each a row of pictures. The game works out what the character is doing, picks the clip
for it, and shows the pictures one after another. A character with no set, or without a clip for what he's doing, is shown with his ordinary
pictures (**Walk Sprite**, **Jump Sprite** and the rest on the Character), so characters can be moved over one at a time.

There's no Unity Animator and no transitions between clips. What a character is doing is one name, picked by a short list in code, so a new
animation is one new clip and, at most, a line of code.

## Setting up a character

1. **Make his set.** Assets > Create > Characters > Animation Set, in `Assets/Animations/Characters`, **named like the character** (`Zipper.asset`
   for the character called Zipper). Zipper's is made for you: **Tools > Animation > Create Zipper Animation Set**.
2. **Give it to him.** **Tools > Animation > Add Animation To Characters** puts a **Character Animator** on every character in the open scene, and
   fills in the set that's named like each one. The characters are objects in each scene, not prefabs, so do it once for every scene. (Or add the
   Character Animator to a character by hand and drag his set onto it.)
3. **Fill in the clips** on the set: see below. Add the pictures to a clip by dragging them onto its **Frames**.

## A clip

| Setting | What it does |
|---|---|
| **State** | What he's doing when it shows: the names below, or one of his own. |
| **Variant** | Empty for the ordinary clip. See Variants. |
| **Frames** | The pictures, in order. A clip with none is the same as no clip. |
| **Frames Per Second** | How fast they go. For a Walk that follows his speed, this is at the fastest he walks. |
| **Loop** | Goes round again at the end. Without it, the clip stops on its last picture. |
| **Follows Walk Speed** | Plays faster the faster he walks and slower the slower, so a slow start looks like a slow start. Meant for Walk. |
| **Then Play** | For a clip that doesn't loop: the state to go on to when it ends. Empty stays on the last picture. |

## What the game picks

Every character is given one of these, picked in this order (the first that's true):

| State | When |
|---|---|
| **AirAction** | He's using his ability, in the air. |
| **Action** | He's using his ability, on the ground. |
| **Rise** | He's in the air and going up. |
| **Fall** | He's in the air and not going up. |
| **Interact** | He's just pulled a lever, picked something up or put it down, on the ground, for as long as the clip takes. Or he's working a lever that has to be held ([Switches.md](Switches.md)), for as long as he is: the clip plays through once and stays on its last picture, so end it on the pose to hold. Give it a clip that doesn't loop. |
| **Walk** | He's walking on the ground. |
| **Idle** | Anything else. |

A character with no ability (Zipper, Die, Paperclip, Pencil) has no Action or AirAction clip. Pressing the action button doesn't change anything for him: he goes on with the next line that's true, Rise, Fall, Interact, Walk or Idle, whichever he'd be without it.

Hurt, pushed back, landing and the like will be more lines in the list as they're added. Each is a state with a clip.

## A character's own states

A character's own script can change what he's doing. It overrides `AnimationState`, which is given the ordinary state and returns the one it wants:

```csharp
protected override string AnimationState(string commonState)
{
    return isRolling ? "Roll" : commonState;
}
```

Then the set gets a clip whose State is `Roll`. If a character says a state that has no clip, he's shown with his ordinary pictures. Bowling Ball
rolling up into a ball, Dynamite exploding and the like are all done this way, and never touch anyone else's animations.

The Bowling Ball is the example (`BowlingBall.cs`). Rolling is his Action, so **Action** is the clip that curls him up into the ball, and it doesn't loop: its
**Then Play** is `RollLoop`, a state only that clip goes on to, which is the ball itself (the code turns it as he rolls). **AirAction** is the ball, as
there's nothing to curl up from in the air. And when he lets go he unrolls: his script gives back `Unroll` for a moment (while he would otherwise be Idle or
Walk), and its clip is the curl the other way round. Tools > Animation > Create Bowling Ball Animation Set makes the set.

He also has the **Carrying** clips, for if he's ever allowed to carry something (Able To Carry Objects is a switch on the character, and the clips don't turn it
on). Every character's set can have them: clips for Idle, Walk, Rise and Fall with the variant Carrying. Zipper has them, and each set made from here on
should too, so any character can be given something to carry without more art.

## Variants

**Carrying** is a variant the game sets by itself, while he's carrying something. A clip for Idle, Walk, Rise or Fall with the variant **Carrying**
is used then, and without one he looks as he usually does. (Interact with Carrying is also looked for, when he picks something up, and without
one the ordinary Interact is used.)

Other variants are for something that changes how a character looks while he does anything, such as Torch being snuffed out under
water. The character's script overrides `AnimationVariant` and returns a name (Snuffed, say; returning `base.AnimationVariant()` otherwise keeps Carrying), and the game looks for the clip with that variant first
(**Walk** with the variant **Snuffed**). **If there isn't one, it uses the ordinary clip.** So a variant only needs clips where it looks different, and
doesn't double up the set.

## Networked games

The host works out the clip, and sends the other machines its number in the set and how fast he's walking (two small values on `CharacterNetwork`).
Everyone shows the same clip, so every machine needs the same set. Putting a clip in the middle of the list renumbers the ones after it, which is
fine as long as everyone runs the same version of the game.

A set can have 250 clips at most.

## Adding to it

- **A new clip for an existing state** (Zipper jumping, say): add a clip to his set with the State `Rise`, or `Fall`, or `Action`.
- **A new thing for everyone to do** (hurt): add it to `ChooseAnimation` in `Character.cs`, where the list above is, and a constant for its name in
  `CharacterAnimationSet`. Characters without a clip for it are shown as before.
- **A thing that only one character does:** the override above.

## For the code

- `CharacterAnimationSet` (`Assets/Scripts/Player/Characters/Animation`): the clips, and `Find(state, variant)`.
- `CharacterAnimator`: plays a clip on the sprite renderer. `Show(clip, walkSpeed)` is called every frame by `Character.ShowSprite`, and says whether
  it drew the character.
- `Character.ChooseAnimation` picks the state, and so the clip. `Character.ShowSprite(facingLeft, airborne, performing, clip, walkSpeed)` shows it,
  on the host and on the other machines.
- `CharacterAnimationMenu` (Editor): the Tools > Animation menu.
