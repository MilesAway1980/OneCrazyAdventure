using UnityEngine;

namespace Characters
{
    // Holding the action button, he sticks to the wall he's touching, where he's pulled gently against it, slides down only
    // slowly and climbs up and down at a set speed with the stick. Touching a ceiling he hangs from it, held still. He
    // stays stuck if the player moves to another character, as characters are meant to work together.
    //
    // Ideas for what he could be used for in puzzles, not built yet:
    //  - Stuck to a wall, he is a step or ledge for other characters to stand on, at a height they couldn't reach.
    //  - Hung from a ceiling, he is a weight over a pressure plate or a pit, or something to hang other things from.
    //  - He is a fixed anchor, such as for Piton's rope.
    public class SuctionCup : Character
    {
        // How soft he is when his profile doesn't say, for ShapeFlex. Rubber: it gives a good deal.
        protected override float DefaultSoftness
        {
            get { return 0.6f; }
        }

        [Tooltip("The picture shown while he's holding on to a wall.")]
        public Sprite wallHoldSprite;
        [Tooltip("The picture shown while he's holding on to a ceiling.")]
        public Sprite ceilingHoldSprite;

        [Range(0f, 90f)]
        [Tooltip("How steep, in degrees from flat, a surface he touches has to be before he sticks to it. Gentler than this he " +
                 "takes for a floor and ignores it, so at 45 a 45 degree slope is a wall. The same goes for the ceiling.")]
        public float wallSlopeLimit = 45f;
        [Tooltip("How fast, in units per second, he's pulled against the wall he's stuck to, so that he doesn't lose his grip.")]
        public float suctionPull = 1f;
        [Tooltip("How much he resists moving while he's on a wall, so he only slides down slowly.")]
        public float wallDrag;
        [Tooltip("How fast, in units per second, he climbs up or down a wall when the stick is pushed up or down.")]
        public float wallClimbSpeed = 3f;

        private enum Hold
        {
            None,
            Wall,
            Ceiling
        }

        // How far the stick has to be pushed up or down to climb.
        private const float ClimbDeadZone = 0.25f;

        // How long he keeps his hold after he last touched what he's stuck to, as touching can stop for an instant.
        private const float HoldGraceSeconds = 0.15f;

        private Hold hold;

        // Which way the surface he's stuck to faces, away from it and toward him.
        private Vector2 surfaceNormal;

        private float lastTouchTime;
        private float climbInput;
        private RigidbodyConstraints2D constraintsBeforeCeiling;

        private ContactFilter2D groundFilter;
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];

        protected override void Start()
        {
            base.Start();

            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetLayerMask(LayerMask.GetMask("Ground"));
        }

        // ----- On a machine that has joined someone else's game -----

        // What he's stuck to, 0 for nothing, 1 for a wall and 2 for a ceiling, and which way he faces on a wall.
        public byte HoldForNetwork
        {
            get { return (byte)hold; }
        }

        public bool FacesRightOnWall
        {
            get { return surfaceNormal.x > 0; }
        }

        // Shows him as the host says he's holding on. Called after his picture has been chosen for walking and jumping.
        public void ShowHold(byte kind, bool facesRight)
        {
            if (!spriteRenderer)
            {
                return;
            }

            var shown = (Hold)kind;

            if (shown == Hold.Wall)
            {
                spriteRenderer.sprite = wallHoldSprite;
                spriteRenderer.flipX = facesRight;
            }
            else if (shown == Hold.Ceiling)
            {
                spriteRenderer.sprite = ceilingHoldSprite;
            }
        }

        protected override void Update()
        {
            base.Update();

            var found = performingAction ? FindHold() : Hold.None;

            if (found != Hold.None)
            {
                lastTouchTime = Time.time;
            }
            else if (performingAction && Time.time - lastTouchTime < HoldGraceSeconds)
            {
                found = hold;
            }

            SetHold(found);

            if (hold != Hold.None)
            {
                // He can't walk while he's stuck, and the walking he tries shouldn't be waiting for him when he lets go.
                moveSpeed = 0;
            }

            if (hold == Hold.Wall)
            {
                spriteRenderer.sprite = wallHoldSprite;
                spriteRenderer.flipX = surfaceNormal.x > 0;
                rigidBody2D.linearDamping = wallDrag;
            }
            else if (hold == Hold.Ceiling)
            {
                spriteRenderer.sprite = ceilingHoldSprite;
            }
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (hold != Hold.Wall)
            {
                return;
            }

            // Along the wall, up, whichever way it leans. His speed along it is whatever gravity and the drag have made
            // it, unless he's climbing, which sets it. Across it, he's pulled against the wall. Done to his speed, rather
            // than by pushing him, so it's the same at any frame rate.
            var along = new Vector2(-surfaceNormal.y, surfaceNormal.x);
            if (along.y < 0)
            {
                along = -along;
            }

            var slide = Vector2.Dot(rigidBody2D.linearVelocity, along);
            if (Mathf.Abs(climbInput) >= ClimbDeadZone)
            {
                slide = climbInput * wallClimbSpeed;
            }

            rigidBody2D.linearVelocity = along * slide - surfaceNormal * suctionPull;
        }

        // What he's touching that he can stick to. A ceiling comes before a wall. Floors, and slopes gentler than the
        // limit, are ignored.
        private Hold FindHold()
        {
            var count = rigidBody2D.GetContacts(groundFilter, contacts);
            var limit = Mathf.Cos(wallSlopeLimit * Mathf.Deg2Rad) + 0.001f;
            var found = Hold.None;

            for (int i = 0; i < count; i++)
            {
                var normal = contacts[i].normal;

                // Which way a contact's normal points depends on which of the two it's seen from, so it's turned to point
                // away from the surface, which is toward him.
                if (Vector2.Dot(normal, rigidBody2D.worldCenterOfMass - contacts[i].point) < 0)
                {
                    normal = -normal;
                }

                if (normal.y > limit)
                {
                    continue;
                }

                if (normal.y < -limit)
                {
                    surfaceNormal = normal;
                    return Hold.Ceiling;
                }

                if (found == Hold.None)
                {
                    surfaceNormal = normal;
                    found = Hold.Wall;
                }
            }

            return found;
        }

        private void SetHold(Hold newHold)
        {
            if (newHold == hold)
            {
                return;
            }

            if (hold == Hold.Ceiling)
            {
                // Let go. The fall that built up while he was held is not to be let go of all at once.
                rigidBody2D.constraints = constraintsBeforeCeiling;
                rigidBody2D.linearVelocity = Vector2.zero;
            }

            if (newHold == Hold.Ceiling)
            {
                constraintsBeforeCeiling = rigidBody2D.constraints;
                rigidBody2D.constraints = RigidbodyConstraints2D.FreezeAll;
                rigidBody2D.linearVelocity = Vector2.zero;
            }

            hold = newHold;
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            climbInput = directionValue.y;

            if (canMove)
            {
                Move(directionValue);
            }
        }

        // The stick isn't being read any more, so he doesn't go on climbing. He stays stuck, though.
        public override void ControlLost()
        {
            climbInput = 0;
        }

        public override void Action(bool active)
        {
            performingAction = active;
        }
    }
}
