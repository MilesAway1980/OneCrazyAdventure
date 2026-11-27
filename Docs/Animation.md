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

The Magnet's Action is his field being on (`Magnet.cs`): standing with the field lines flickering round him. **AirAction** is the same in the air. Walking with the field on is his own state, `ActionWalk`: his script gives it back instead of Action while he's moving, if his set has a clip for it (without one he shows the standing Action). Tools > Animation > Create Magnet Animation Set makes the set.

The Brick turns solid as his Action (`Brick.cs`): the clip for **Action** pulls his arms and legs in and shuts his eyes, and doesn't loop, so its **Then Play** is `Solid`, the solid block, held as long as he is. **AirAction** is the block too. Turning back, his script gives back `Unsolid` for a moment (while he would otherwise be Idle or Walk), and its clip is the first one the other way round. Tools > Animation > Create Brick Animation Set makes the set.

The Bulb has no face, so his filament does the acting: it flickers a little while he stands (his Idle clip is six pictures going round slowly), flares as he pulls a lever, and goes orange under a load. His Action is his light, the filament flickering bright and a glow in the glass (`Bulb.cs`): **Action** standing, **AirAction** in the air, and walking lit is his own state, `ActionWalk`, which his script gives back instead of Action while he's moving, if his set has a clip for it. Tools > Animation > Create Bulb Animation Set makes the set.

The Balloon has no arms or legs, so his animation is his shape and his face. Standing he blinks (his Idle clip is a long open-eyed picture and a short blink going round). Holding the action blows him up (`Balloon.cs`): the clip for **Action** grows him from his ordinary size to his full one in six pictures, and its **Then Play** is `Inflated`, the full size, held for as long as he is. It's the same clip in the air as on the ground (his script says `Action` for `AirAction`), so that it isn't started over as he lifts off. Letting go, his script gives back `Deflate` for a moment, which is the blowing up the other way round. While someone has hold of the end of his string, or is carrying him, he has the variant `Held`: the same clips with his eyes squeezed shut, > and <, and no mouth. His Interact and his Carrying are that face too. Tools > Animation > Create Balloon Animation Set makes the set.

Squashing and stretching are not clips: `ShapeFlex` stretches a character's picture a little the way he's going and squishes it, ringing back like jelly, when he's stopped suddenly. Every character has one (added by `Character`), and how much it does for him is his **Softness**, from 0 (hard: nothing at all, not even touching his picture) to 1 (as soft as a balloon). It's set on his Movement Profile, under What it is; left at -1 it's what his class says: the Balloon and the Bouncy Ball are 1, the Suction Cup 0.6, the Umbrella 0.3, Zipper and the other plain characters 0.15, and the rest 0. The numbers on the `ShapeFlex` component are what a softness of 1 does, and a softer one is that fraction of them. It watches how fast he moves, so it does the same on the other machines in a networked game, and only the picture changes size, never his body. It multiplies whatever size his picture has been given, so the Bouncy Ball blowing up and the Bulb turning as he's screwed in carry on as they were.

The Bouncy Ball is a soft blob with two eyes, so his clips are his shape and face. Standing he breathes and blinks, and walking he squashes and stretches. Charging a jump (`BouncyBall.cs`) is his own state, `Charge`: it squashes him down in four pictures over the two seconds his charge takes (his clip is two a second), then goes on to `ChargeFull`, trembling, until he's let go. While he's blown up (the button held or locked) his state is `Inflated`, the same shape pulsing. His script now eases his picture up to its inflated size over a fraction of a second instead of at once, and the clips don't change his size. The jumping and landing squash is `ShapeFlex`, which he's the softest of. Tools > Animation > Create Bouncy Ball Animation Set makes the set.

The Pebble has no action of his own: he's fast, nimble and small. He fidgets standing (a toe tap, a little hop, a blink), runs in six pictures, and at the top of a big jump he rolls (`Pebble.cs`): his own state, `Roll`, which his script gives back for half a second from just before the top of the jump, in place of Rise or Fall. The clip is ten pictures: tucking in, one full turn of the whole stone, shading and face, and opening out. He only rolls if he rose faster than **Roll Min Jump Speed** at some point in the jump, so a small hop, a jump let go of early, a fall off a ledge, and a jump while carrying something don't. Running at his top speed for a second (**Sprint After Seconds**), he's `Sprint`, another state of his own: his run with his hands in the air, happy eyes and a smile, because he loves it. It goes as fast as he runs, and drops back to the ordinary run once he slows down. It's skipped while he's carrying something. Tools > Animation > Create Pebble Animation Set makes the set.

The Torch has a whole second set of clips for when he's put out under liquid: the variants `Snuffed` and, if he's carrying, `SnuffedCarrying` (`Torch.cs` asks for them while he's in liquid), drawn charred with no flame. Tools > Animation > Create Torch Animation Set makes both sets of clips. His old `Unlit Stand` and `Unlit Jump` pictures are only used if he has no clips drawing him.

He's put out the moment he's in liquid, but lights up in stages when he comes out: `Torch.cs` runs through **Relight Seconds**, a small flame (the variants `Flickering` and `FlickeringCarrying`, drawn as a small flame on the charred head), then out, then a small flame, and so on, ending on the small flame, and then the full one. His light follows (the small flame's brightness is **Small Flame Light**), and he only lights sconces and warms things once he's burning fully. Empty Relight Seconds and he's lit as soon as he's out. The other machines are sent how much he's burning (`Torch.Flame`), so they see the flickers too.

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
