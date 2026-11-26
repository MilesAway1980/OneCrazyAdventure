using System;
using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    // All of one character's animations: a list of named clips, each a row of pictures. Made with Assets > Create > Characters > Animation
    // Set, and given to the character's Character Animator. See Docs/Animation.md.
    //
    // A clip is found by the name of what the character is doing (its state) and, if it has one, a variant: Walk, or Walk with the
    // variant Snuffed. A variant only needs clips where it looks different. Asking for Walk (Snuffed) and finding none gets Walk.
    [CreateAssetMenu(menuName = "Characters/Animation Set", fileName = "Animation Set")]
    public class CharacterAnimationSet : ScriptableObject
    {
        // What every character does, and so what the game picks for them. A character's own script can pick others (Roll, Explode).
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Rise = "Rise";
        public const string Fall = "Fall";
        public const string Action = "Action";
        public const string AirAction = "AirAction";

        // Using something: pulling a lever, picking something up, putting it down. It plays through once, for a moment.
        public const string Interact = "Interact";

        // A variant, for while he's carrying something. A clip for Idle, Walk, Rise or Fall with this variant is used when he is.
        public const string Carrying = "Carrying";

        // No clip. It's also what is sent to the other machines in a networked game, as the number 255.
        public const int None = -1;

        // Sent as one byte, so no more than this many clips in a set.
        public const int MostClips = 250;

        [Serializable]
        public class Clip
        {
            [Tooltip("What the character is doing: Idle, Walk, Rise, Fall, Action or AirAction, or a name that one of his own scripts picks, " +
                     "such as Roll or Explode.")]
            public string state = Idle;

            [Tooltip("Leave empty for the ordinary one. Something that changes how the character looks while he does it, such as Snuffed, " +
                     "which a script sets. A clip for a variant is used when there is one, and the ordinary clip when there isn't.")]
            public string variant;

            [Tooltip("The pictures, in order.")]
            public Sprite[] frames;

            [Min(0.1f)]
            [Tooltip("How many pictures are shown a second. For a Walk that follows his speed, it's at the fastest he walks.")]
            public float framesPerSecond = 12f;

            [Tooltip("Whether it goes round again at the end. If it doesn't, it stops on the last picture, or goes on to the clip in Then Play.")]
            public bool loop = true;

            [Tooltip("Whether it plays faster the faster he walks, and slower the slower he does, so a slow start looks like one.")]
            public bool followsWalkSpeed;

            [Tooltip("Only for a clip that doesn't loop: the state to go on to when it ends, with the same variant. Empty stays on the last picture.")]
            public string thenPlay;

            public bool HasFrames
            {
                get { return frames != null && frames.Length > 0; }
            }
        }

        public List<Clip> clips = new List<Clip>();

        // The clip for a state and variant, or None. With a variant that has no clip of its own, the ordinary clip for the state.
        public int Find(string state, string variant)
        {
            if (string.IsNullOrEmpty(state))
            {
                return None;
            }

            var ordinary = None;

            for (var i = 0; i < clips.Count && i < MostClips; i++)
            {
                var clip = clips[i];

                if (clip == null || !clip.HasFrames || clip.state != state)
                {
                    continue;
                }

                var clipVariant = clip.variant ?? string.Empty;

                if (clipVariant == (variant ?? string.Empty))
                {
                    return i;
                }

                if (clipVariant.Length == 0)
                {
                    ordinary = i;
                }
            }

            return ordinary;
        }

        // Puts a clip in the set, in place of the one for the same state and variant if there is one.
        public Clip SetClip(string state, string variant, Sprite[] frames, float framesPerSecond, bool loop, bool followsWalkSpeed)
        {
            Clip clip = null;

            foreach (var existing in clips)
            {
                if (existing != null && existing.state == state && (existing.variant ?? string.Empty) == (variant ?? string.Empty))
                {
                    clip = existing;
                    break;
                }
            }

            if (clip == null)
            {
                clip = new Clip();
                clips.Add(clip);
            }

            clip.state = state;
            clip.variant = variant;
            clip.frames = frames;
            clip.framesPerSecond = framesPerSecond;
            clip.loop = loop;
            clip.followsWalkSpeed = followsWalkSpeed;
            return clip;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (clips != null && clips.Count > MostClips)
            {
                Debug.LogWarning($"{name}: a set can have {MostClips} clips at the most, because the number of the clip is sent as one byte. " +
                                 "The rest are ignored.", this);
            }
        }
#endif
    }
}
