using UnityEngine;

namespace Characters
{
    public class Balloon : Character
    {
        // ----- Animation -----
        //
        // Holding the action blows him up: the clip for Action grows him from his ordinary size to his full one, and goes on by itself into Inflated, the
        // full size, held for as long as he is. It's the same clip in the air, so that it isn't started over as he lifts off. Letting go, he shrinks back
        // for a moment, which is his own state, Deflate (the same clip the other way round). While someone has hold of the end of his string, or is
        // carrying him, he's Held: the same clips, with the eyes squeezed shut, > and <, and no mouth. He also blinks, standing. His squashing and
        // stretching as he moves, and squishing when he hits something, is ShapeFlex, and not clips. See Docs/Animation.md.
        public const string InflatedState = "Inflated";
        public const string DeflateState = "Deflate";
        public const string HeldVariant = "Held";

        // How long the Deflate clip takes: its six pictures, at 14 a second.
        private const float DeflateSeconds = 0.43f;
        private float deflateUntil;

        private GrabPoint[] grabPoints;
        private CarryableObject carryable;

        // The pieces of his string, which are bodies of their own hung from him: what he lifts as he rises, apart from himself.
        private Rigidbody2D[] stringPieces;

        protected override float HangingWeight()
        {
            if (stringPieces == null)
            {
                var own = GetComponent<Rigidbody2D>();
                var found = new System.Collections.Generic.List<Rigidbody2D>();

                foreach (var body in GetComponentsInChildren<Rigidbody2D>(true))
                {
                    if (body != own)
                    {
                        found.Add(body);
                    }
                }

                stringPieces = found.ToArray();
            }

            var weight = 0f;

            foreach (var piece in stringPieces)
            {
                if (piece && piece.simulated)
                {
                    weight += piece.mass * piece.gravityScale;
                }
            }

            return weight;
        }

        // The end of his string is where things are carried, and it has to hang free like the rest of it. If it were Kinematic it would be held
        // fixed to him, both ends of the string pinned to the same body, and the string would fight him all the time.
        protected override bool CarryPointMovesWithHim
        {
            get { return false; }
        }

        // As soft as they come: ShapeFlex stretches him as he goes and squishes him when he hits something.
        protected override float DefaultSoftness
        {
            get { return 1f; }
        }

        protected override string AnimationState(string commonState)
        {
            // Inflated, he's one clip whether he's on the ground or in the air.
            if (commonState == CharacterAnimationSet.AirAction)
            {
                return CharacterAnimationSet.Action;
            }

            if (Time.time < deflateUntil && commonState != CharacterAnimationSet.Action)
            {
                return DeflateState;
            }

            return commonState;
        }

        protected override string AnimationVariant()
        {
            var ordinary = base.AnimationVariant();

            return ordinary.Length == 0 && IsHeldByAnother ? HeldVariant : ordinary;
        }

        // Whether someone is hanging from his string or carrying him.
        private bool IsHeldByAnother
        {
            get
            {
                if (carryable && carryable.IsCarried)
                {
                    return true;
                }

                if (grabPoints != null)
                {
                    foreach (var point in grabPoints)
                    {
                        if (point && point.IsHeld)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        protected override void Start()
        {
            // His string hangs from him, so taking hold of the end of it would tie him in a loop.
            ableToGrabObjects = false;

            // The end of his string is somewhere to take hold of, with Interact, and hang from: a light character can be lifted by it and a heavy one can't.
            // It's put on here if the scene doesn't have it, which is the same as having added it by hand.
            if (carryAttachmentPoint && !carryAttachmentPoint.GetComponent<GrabPoint>())
            {
                RegisterInteractable(carryAttachmentPoint.gameObject.AddComponent<GrabPoint>());
            }

            grabPoints = GetComponentsInChildren<GrabPoint>(true);
            carryable = GetComponent<CarryableObject>();

            base.Start();
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }

        public override void Action(bool active)
        {
            if (active)
            {
                deflateUntil = 0f;
            }
            else if (performingAction)
            {
                deflateUntil = Time.time + DeflateSeconds;
            }

            performingAction = active;
        }

        // Held, he lets air in to float; otherwise he moves as anyone does. What the liquid does to him is looked after.
        protected override string GetMovementMode()
        {
            return performingAction ? "float" : "base";
        }
    }
}
