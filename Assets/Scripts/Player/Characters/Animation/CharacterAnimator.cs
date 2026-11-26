using UnityEngine;

namespace Characters
{
    // Plays a character's animations: shows one clip of its Animation Set, a picture at a time, on the sprite renderer. It doesn't decide
    // what he's doing. The Character does that, on the machine that runs the game, and says which clip to show (see
    // Character.ShowSprite). So everyone sees the same thing.
    //
    // A character with none of this, or without a clip for what he's doing, is shown with his ordinary pictures. See Docs/Animation.md.
    [RequireComponent(typeof(Character))]
    public class CharacterAnimator : MonoBehaviour
    {
        [Tooltip("This character's clips. Assets > Create > Characters > Animation Set. Tools > Animation > Add Animation To Characters finds " +
                 "the set that's named like the character, and fills this in.")]
        public CharacterAnimationSet animationSet;

        private SpriteRenderer spriteRenderer;

        // The clip that's been asked for, so that it's only started over when what's asked for changes, and the clip that's playing,
        // which is the same unless it ended and went on to another.
        private int requested = CharacterAnimationSet.None;
        private int playing = CharacterAnimationSet.None;
        private float time;

        // The clip for what a character is doing, or None.
        public int Find(string state, string variant)
        {
            return animationSet ? animationSet.Find(state, variant) : CharacterAnimationSet.None;
        }

        // How long a clip takes to play once through, in seconds.
        public float ClipSeconds(int clip)
        {
            if (!animationSet || clip < 0 || clip >= animationSet.clips.Count)
            {
                return 0f;
            }

            var found = animationSet.clips[clip];
            return found != null && found.HasFrames ? found.frames.Length / found.framesPerSecond : 0f;
        }

        // Shows the clip, moved on by this frame, and says whether it's drawing the character. If it isn't (there's no such clip), the
        // character is shown with an ordinary picture instead. Called every frame. Speed is how fast he's walking, as a fraction of the
        // fastest he does, for a clip that follows it.
        public bool Show(int clip, float speed)
        {
            if (!animationSet || clip < 0 || clip >= animationSet.clips.Count)
            {
                requested = CharacterAnimationSet.None;
                playing = CharacterAnimationSet.None;
                return false;
            }

            if (!spriteRenderer)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

                if (!spriteRenderer)
                {
                    return false;
                }
            }

            // Asked for something different: start it from the beginning.
            if (clip != requested)
            {
                requested = clip;
                playing = clip;
                time = 0f;
            }

            var current = animationSet.clips[playing];

            if (current == null || !current.HasFrames)
            {
                return false;
            }

            time += Time.deltaTime * (current.followsWalkSpeed ? speed : 1f);

            var count = current.frames.Length;
            var frame = Mathf.FloorToInt(time * current.framesPerSecond);

            if (current.loop)
            {
                // Kept small, so that a character walking for an hour doesn't lose his accuracy.
                var duration = count / current.framesPerSecond;

                if (time >= duration)
                {
                    time -= duration * Mathf.Floor(time / duration);
                    frame = Mathf.FloorToInt(time * current.framesPerSecond);
                }

                frame = Mathf.Clamp(frame, 0, count - 1);
            }
            else if (frame >= count)
            {
                var next = string.IsNullOrEmpty(current.thenPlay) ? CharacterAnimationSet.None : animationSet.Find(current.thenPlay, current.variant);

                if (next != CharacterAnimationSet.None && next != playing)
                {
                    playing = next;
                    time = 0f;
                    current = animationSet.clips[playing];
                    frame = 0;
                }
                else
                {
                    frame = count - 1;
                }
            }

            spriteRenderer.sprite = current.frames[Mathf.Clamp(frame, 0, current.frames.Length - 1)];
            return true;
        }
    }
}
