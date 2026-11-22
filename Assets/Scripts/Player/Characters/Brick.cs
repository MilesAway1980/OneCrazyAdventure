using UnityEngine;

namespace Characters
{
    // Press the action once to turn into a solid block, and again to turn back. While solid he's part of the map:
    // other characters can stand on him and jump from him, and can push him along. He stays solid when the player
    // switches to someone else.
    //
    // He can only turn solid when nothing is inside him, since whatever was would be shoved out, possibly into a wall.
    public class Brick : Character
    {
        [Tooltip("His mass while solid, which sets how hard he is to push. 0 keeps his normal mass. Characters set their own speed directly, so something far heavier than they are barely moves.")]
        public float solidMass = 50f;

        [Tooltip("Seconds he shudders, in his action sprite and the Blocked Color, when he can't turn solid because something is in the way. 0 turns it off.")]
        public float blockedFlashSeconds = 0.3f;

        [Tooltip("The colour he flashes while he can't turn solid because something is in the way.")]
        public Color blockedColor = Color.red;

        [Tooltip("How far, in world units, his sprite is thrown about while he shudders.")]
        public float blockedShakeDistance = 0.06f;

        // How far inside his outline something has to be before it counts as being in the way, so that a character
        // merely standing against him doesn't.
        private const float OverlapMargin = 0.05f;

        private bool isSolid;
        private int originalLayer;
        private float originalMass;
        private PushableSolid pushable;

        private float flashTimeRemaining;
        private Color originalColor;
        private Vector3 originalSpritePosition;

        protected override void Start()
        {
            base.Start();

            originalLayer = gameObject.layer;
            originalMass = rigidBody2D.mass;

            pushable = GetComponent<PushableSolid>();

            if (!pushable)
            {
                pushable = gameObject.AddComponent<PushableSolid>();
            }

            pushable.enabled = false;
        }

        protected override void Update()
        {
            base.Update();

            if (flashTimeRemaining > 0f)
            {
                flashTimeRemaining -= Time.deltaTime;

                if (flashTimeRemaining <= 0f)
                {
                    EndFlash();
                }
                else
                {
                    ShowBlocked();
                }
            }
        }

        // Called once each time the action button goes down.
        public override void FirstActionPress()
        {
            if (isSolid)
            {
                SetSolid(false);
            }
            else if (IsClear())
            {
                SetSolid(true);
            }
            else
            {
                FlashBlocked();
            }
        }

        // Called every frame with whether the button is held, which doesn't matter here: it's a toggle.
        public override void Action(bool active)
        {
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }

        public bool IsSolid()
        {
            return isSolid;
        }

        // Whether nothing is inside him, so he can turn solid without shoving anything.
        public bool IsClear()
        {
            var colliders = new Collider2D[rigidBody2D.attachedColliderCount];
            rigidBody2D.GetAttachedColliders(colliders);

            foreach (var own in colliders)
            {
                if (own.isTrigger)
                {
                    continue;
                }

                var bounds = own.bounds;
                var size = new Vector2(
                    Mathf.Max(bounds.size.x - OverlapMargin * 2f, 0.01f),
                    Mathf.Max(bounds.size.y - OverlapMargin * 2f, 0.01f));

                foreach (var other in Physics2D.OverlapBoxAll(bounds.center, size, 0f))
                {
                    if (other.isTrigger)
                    {
                        continue;
                    }

                    // The map itself and other fixed things aren't what gets shoved around.
                    var body = other.attachedRigidbody;

                    if (body && body != rigidBody2D && body.bodyType != RigidbodyType2D.Static)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Turns solid or back without checking whether the space is clear.
        public void SetSolid(bool solid)
        {
            if (solid == isSolid)
            {
                return;
            }

            isSolid = solid;

            if (flashTimeRemaining > 0f)
            {
                EndFlash();
            }

            // Shows his action sprite while solid.
            performingAction = solid;

            // A solid brick sits where he is; he doesn't walk or jump.
            canMove = !solid;

            if (solid)
            {
                moveSpeed = 0;
                rigidBody2D.linearVelocityX = 0;

                // Characters don't collide with each other, but they do collide with the ground, and they sense
                // the ground (to stand and jump from) by the Ground layer. Joining it makes him both.
                gameObject.layer = LayerMask.NameToLayer("Ground");

                if (solidMass > 0)
                {
                    rigidBody2D.mass = solidMass;
                }
            }
            else
            {
                gameObject.layer = originalLayer;
                rigidBody2D.mass = originalMass;
            }

            pushable.enabled = solid;
        }

        // How many times he has shuddered, which another machine goes by to shudder when he does.
        public int BlockedCount { get; private set; }

        // ----- On a machine that has joined someone else's game -----

        private bool shownFlashing;
        private float shownFlashUntil;

        // The host says he has just shuddered.
        public void StartShownFlash()
        {
            if (blockedFlashSeconds <= 0f || !spriteRenderer)
            {
                return;
            }

            if (!shownFlashing)
            {
                originalColor = spriteRenderer.color;
                originalSpritePosition = spriteRenderer.transform.localPosition;
                shownFlashing = true;
            }

            shownFlashUntil = Time.time + blockedFlashSeconds;
        }

        // Called every frame, after his picture has been chosen, to go on shuddering until the time is up.
        public void UpdateShownFlash()
        {
            if (!shownFlashing)
            {
                return;
            }

            if (Time.time >= shownFlashUntil)
            {
                shownFlashing = false;
                EndFlash();
            }
            else
            {
                ShowBlocked();
            }
        }

        private void FlashBlocked()
        {
            if (blockedFlashSeconds <= 0f)
            {
                return;
            }

            BlockedCount++;

            if (flashTimeRemaining <= 0f)
            {
                originalColor = spriteRenderer.color;
                originalSpritePosition = spriteRenderer.transform.localPosition;
            }

            flashTimeRemaining = blockedFlashSeconds;
            ShowBlocked();
        }

        // His action sprite, tinted, and thrown about a little. Drawn over whatever the character's own sprite
        // handling chose this frame.
        private void ShowBlocked()
        {
            if (actionSprite)
            {
                spriteRenderer.sprite = actionSprite;
            }

            spriteRenderer.color = blockedColor;

            // Only the sprite is shaken. If it's on the same object as the body, that would shake the physics too.
            if (spriteRenderer.transform != transform)
            {
                spriteRenderer.transform.localPosition =
                    originalSpritePosition + (Vector3)(Random.insideUnitCircle * blockedShakeDistance);
            }
        }

        private void EndFlash()
        {
            flashTimeRemaining = 0f;
            spriteRenderer.color = originalColor;

            if (spriteRenderer.transform != transform)
            {
                spriteRenderer.transform.localPosition = originalSpritePosition;
            }
        }
    }
}
