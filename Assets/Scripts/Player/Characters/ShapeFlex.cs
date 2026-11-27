using UnityEngine;

namespace Characters
{
    // Squashes and stretches a character's picture, which gives a soft one such as the Balloon some life: it stretches a little the way he's
    // going, more the faster he goes, and when he's stopped suddenly (he hits something) it squishes flat against what he hit and rings back, like
    // jelly. Only the picture changes size, never his body, and the pictures of his animation set are left as they are.
    //
    // Every character has one, added by Character, and how much it does is his Softness: 0 is hard (a brick doesn't change at all, and this does
    // nothing for him) and 1 is as soft as a balloon. It's set on his Movement Profile, and left there at -1 it's what his class says. The numbers
    // here are what the very softest does.
    //
    // It works from where he is, by watching how fast he moves, so it does the same on the host and on a machine that has joined someone else's game,
    // where he is only shown. It multiplies whatever size the character has given his picture, so one that sets its own (the Bouncy Ball being
    // inflated, the Bulb turning as he's screwed in) is carried on as before.
    public class ShapeFlex : MonoBehaviour, IKeepsRunningWhenShownOnly
    {
        [Tooltip("The picture that is squashed. Left empty, it's the one on the character's Sprite Renderer, if that's not on the body itself (which " +
                 "would squash his physics).")]
        public Transform picture;

        [Header("Moving")]
        [Tooltip("How much the softest stretches the way it's going at its full speed, as a fraction: 0.08 is a tenth wider (and the same narrower) at most. " +
                 "A softness below 1 is that much of it. 0 turns it off.")]
        public float flex = 0.08f;

        [Tooltip("The speed, in units a second, at which he is stretched all of Flex.")]
        public float flexFullSpeed = 4f;

        [Tooltip("How quickly the stretch follows his speed. Higher is quicker.")]
        public float flexFollow = 10f;

        [Header("Hitting something")]
        [Tooltip("How much the softest squishes at the most, as a fraction of its size. A softness below 1 is that much of it. 0 turns it off.")]
        public float squish = 0.3f;

        [Tooltip("How much speed, in units a second, he has to lose in the way he was going for a squish of all of Squish. Less is a smaller one.")]
        public float squishFullSpeed = 6f;

        [Tooltip("He has to have been going at least this fast, in units a second, for a stop to be a hit.")]
        public float minHitSpeed = 1.5f;

        [Tooltip("How many times a second the squish rings back and forth.")]
        public float ringsPerSecond = 4.5f;

        [Tooltip("How quickly it settles: 0 would ring on for ever, and 1 settles without ringing back at all.")]
        [Range(0.05f, 1f)]
        public float settle = 0.3f;

        // Where he was, a little while ago, to tell how fast he's been going. A stop is how much slower he is in the last short while than the
        // short while before it.
        private const float Window = 0.05f;
        private const float HitAgainSeconds = 0.2f;
        private const float LostSpeed = 0.6f;
        private const float WarpSpeed = 40f;
        private const float LeastSoftness = 0.001f;
        private const int Kept = 48;

        private readonly float[] times = new float[Kept];
        private readonly Vector2[] places = new Vector2[Kept];
        private int count;
        private int next;

        private Character character;
        private Vector2 smoothVelocity;
        private float lastHit = -10f;

        // The squish, across him and up and down, and how fast each is changing: a spring that was knocked.
        private float squishX;
        private float squishXVelocity;
        private float squishY;
        private float squishYVelocity;

        // The size of the picture before this changed it, and what this made it, to tell when something else has set it since.
        private Vector3 baseScale = Vector3.one;
        private Vector3 lastWritten;
        private bool wrote;

        void Start()
        {
            character = GetComponent<Character>();

            if (!picture)
            {
                var sprite = GetComponentInChildren<SpriteRenderer>();

                if (sprite && sprite.transform != transform)
                {
                    picture = sprite.transform;
                }
            }
        }

        void OnDisable()
        {
            GiveBack();
        }

        void LateUpdate()
        {
            var dt = Time.deltaTime;

            if (!picture || dt <= 0f)
            {
                return;
            }

            var softness = character ? character.Softness : 0f;

            // A hard one: nothing to do, and nothing of his picture is touched, which is how the others that set its size by hand carry on.
            if (softness < LeastSoftness)
            {
                GiveBack();
                count = 0;
                squishX = squishY = squishXVelocity = squishYVelocity = 0f;
                smoothVelocity = Vector2.zero;
                return;
            }

            Record(transform.position);

            var now = Time.time;
            var velocity = (Place(now) - Place(now - Window)) / Window;
            var before = (Place(now - Window) - Place(now - 2f * Window)) / Window;

            // Put somewhere else in one go, such as by a teleporter or when he's called back: not a hit.
            if (velocity.magnitude > WarpSpeed || before.magnitude > WarpSpeed)
            {
                count = 0;
                smoothVelocity = Vector2.zero;
                squishXVelocity = squishYVelocity = 0f;
                squishX = squishY = 0f;
                Apply(0f, 0f, 0f);
                return;
            }

            DetectHit(velocity, before, now, softness);

            smoothVelocity = Vector2.Lerp(smoothVelocity, velocity, 1f - Mathf.Exp(-flexFollow * dt));
            Ring(dt);

            var along = flexFullSpeed > 0.01f
                ? Mathf.Clamp01(Mathf.Abs(smoothVelocity.x) / flexFullSpeed) - Mathf.Clamp01(Mathf.Abs(smoothVelocity.y) / flexFullSpeed)
                : 0f;

            Apply(flex * softness * along, squishX, squishY);
        }

        // Notes where he is. Kept for the last second or so, which is all that's looked back over.
        private void Record(Vector2 position)
        {
            times[next] = Time.time;
            places[next] = position;
            next = (next + 1) % Kept;
            count = Mathf.Min(count + 1, Kept);
        }

        // Where he was at a time, from the places noted: between the two either side of it, or the oldest if it's earlier than all of them.
        private Vector2 Place(float time)
        {
            var newest = (next - 1 + Kept) % Kept;

            for (var i = 0; i < count - 1; i++)
            {
                var later = (newest - i + Kept) % Kept;
                var earlier = (later - 1 + Kept) % Kept;

                if (times[earlier] <= time)
                {
                    var span = times[later] - times[earlier];
                    var t = span > 0.0001f ? Mathf.Clamp01((time - times[earlier]) / span) : 1f;
                    return Vector2.Lerp(places[earlier], places[later], t);
                }
            }

            return places[(newest - (count - 1) + Kept) % Kept];
        }

        // A hit: he was going quickly one way, and now has lost most of it. The squish is across the way he hit, and the other way gets the
        // bulge, which is what gives it away as soft.
        private void DetectHit(Vector2 velocity, Vector2 before, float now, float softness)
        {
            if (squish <= 0f || now - lastHit < HitAgainSeconds || count < 6)
            {
                return;
            }

            var omega = 2f * Mathf.PI * ringsPerSecond;
            var xLost = LostAlong(before.x, velocity.x);
            var yLost = LostAlong(before.y, velocity.y);

            if (xLost <= 0f && yLost <= 0f)
            {
                return;
            }

            lastHit = now;

            if (xLost >= yLost)
            {
                var amount = Mathf.Clamp01(xLost / squishFullSpeed) * squish * softness * omega;
                squishXVelocity -= amount;
                squishYVelocity += amount * 0.6f;
            }
            else
            {
                var amount = Mathf.Clamp01(yLost / squishFullSpeed) * squish * softness * omega;
                squishYVelocity -= amount;
                squishXVelocity += amount * 0.6f;
            }
        }

        // How much speed he has lost in the way he was going, along one line, or 0 if he hasn't lost enough for it to be a hit.
        private float LostAlong(float was, float isNow)
        {
            var speed = Mathf.Abs(was);

            if (speed < minHitSpeed)
            {
                return 0f;
            }

            var kept = isNow * Mathf.Sign(was);

            return kept < speed * (1f - LostSpeed) ? speed - Mathf.Max(kept, 0f) : 0f;
        }

        // The squish settling, as a spring.
        private void Ring(float dt)
        {
            var omega = 2f * Mathf.PI * ringsPerSecond;
            var drag = 2f * settle * omega;

            squishXVelocity += (-omega * omega * squishX - drag * squishXVelocity) * dt;
            squishX += squishXVelocity * dt;
            squishYVelocity += (-omega * omega * squishY - drag * squishYVelocity) * dt;
            squishY += squishYVelocity * dt;

            // However hard he's hit, he's only squashed so far.
            squishX = Mathf.Clamp(squishX, -0.6f, 0.6f);
            squishY = Mathf.Clamp(squishY, -0.6f, 0.6f);
        }

        // Sets his picture to the size it was given, times this. If something else has set its size since this last did (the Bouncy Ball blowing
        // up, the Bulb turning), that is the size it's of, and not what this made it.
        private void Apply(float stretch, float x, float y)
        {
            var current = picture.localScale;

            if (!wrote || current != lastWritten)
            {
                baseScale = current;
            }

            picture.localScale = new Vector3(
                baseScale.x * (1f + stretch) * (1f + x),
                baseScale.y * (1f - stretch) * (1f + y),
                baseScale.z);

            lastWritten = picture.localScale;
            wrote = true;
        }

        // Puts his picture back at the size it was given, if this has changed it, and lets go of it.
        private void GiveBack()
        {
            if (wrote && picture && picture.localScale == lastWritten)
            {
                picture.localScale = baseScale;
            }

            wrote = false;
        }
    }
}
