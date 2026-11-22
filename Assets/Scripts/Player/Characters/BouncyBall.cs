using UnityEngine;

namespace Characters
{
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class BouncyBall : Character
    {
        PhysicsMaterial2D bouncyBallMaterial;
        [Tooltip("How many times bigger he gets when he's fully inflated.")]
        public float inflateSize;
        [Tooltip("The bouncy picture, with its own collider, that's shown while he's inflated.")]
        public GameObject bouncyShell;
        [Tooltip("The picture shown while he's charging.")]
        public Sprite chargeSprite;
        [Tooltip("How fast, each second, his charge builds up while the ability button is held.")]
        public float chargeValueIncreaseSpeed;
        [Tooltip("The most charge he can build up. At this he's fully inflated.")]
        public float maximumChargeValue;

        [Tooltip("How tall his collider is, in units, while he's squashed down charging a jump.")]
        public float chargingColliderHeight = 0.12f;

        private enum Form
        {
            Normal,
            Charging,
            Inflated
        }

        // The form his shape was last put in, which is nothing until the first frame.
        private Form? appliedForm;

        private CapsuleCollider2D capsuleCollider;
        private Vector3 originalSpriteSize;
        private Vector2 originalColliderDimensions;
        private bool charging;

        // How far his jump is charged, which the meter shows.
        private float chargeValue;

        // Down has to be pushed this far to count, so walking with an analog stick doesn't start a charge.
        private const float DownThreshold = -0.5f;

        [Tooltip("Two presses of the action button this close together, in seconds, count as a double tap.")]
        public float doubleTapSeconds = 0.3f;

        [Tooltip("How long, in seconds, he stays inflated after the action button is let go, so that tapping it twice doesn't make him deflate and inflate again in between.")]
        public float deflateDelaySeconds = 0.25f;

        [Header("Charge Meter")]
        [Tooltip("A bar in the upper right of the view of the player using him, showing how far his jump is charged. It appears while he charges and fades just after he jumps.")]
        public MeterLayout chargeMeter = new MeterLayout { showSeconds = 0.5f };

        private ScreenMeter meter;
        private float meterShowUntil;

        // The charge the bar shows. It holds the last value while the bar fades out after the jump.
        private float displayedCharge;

        private bool actionHeld;
        private bool downPressed;

        // Until when he stays inflated after the button is let go.
        private float inflatedUntil;

        private bool inflated;

        // How much smaller than his inflated shape the space he needs is tested as, in units.
        private const float RoomMargin = 0.02f;

        private ContactFilter2D roomFilter;
        private readonly Collider2D[] roomOverlaps = new Collider2D[1];

        // Set when a charged jump is let go and until he lands. The action button is usually still held then, but the
        // jump and the fall are an ordinary one, not an inflated one: those settings don't pull him back down.
        private bool chargedJumpInAir;
        private float chargedJumpTime;

        // A double tap keeps him inflated without the button held, even while another character is being played.
        private bool inflateLocked;
        private float lastPressTime = float.NegativeInfinity;

        protected override void Start()
        {
            base.Start();
            bouncyBallMaterial = rigidBody2D.sharedMaterial;
            originalSpriteSize = spriteRenderer.transform.localScale;
            bouncyShell.SetActive(false);

            capsuleCollider = GetComponent<CapsuleCollider2D>();
            originalColliderDimensions = capsuleCollider.size;

            roomFilter = new ContactFilter2D { useTriggers = false };
            roomFilter.SetLayerMask(LayerMask.GetMask("Ground"));

            meter = gameObject.AddComponent<ScreenMeter>();
        }

        // ----- On a machine that has joined someone else's game -----
        //
        // The host works out which form he's in and how far his jump is charged, and sends them. This machine shows him in that form, with
        // his charge bar. His shape for the physics, and how he bounces, are the host's.

        // Which form he's in: 0 normal, 1 charging, 2 inflated.
        public byte FormForNetwork
        {
            get { return (byte)(IsInflated() ? Form.Inflated : (charging ? Form.Charging : Form.Normal)); }
        }

        // How far his jump is charged, from 0 to 1.
        public float ChargeFraction
        {
            get { return maximumChargeValue > 0f ? Mathf.Clamp01(chargeValue / maximumChargeValue) : 0f; }
        }

        // Called every frame, after his picture has been chosen for walking and jumping.
        public void ShowForm(byte form, float chargeFraction)
        {
            if (!spriteRenderer || !bouncyShell)
            {
                return;
            }

            var shown = (Form)form;
            var inflatedNow = shown == Form.Inflated;

            spriteRenderer.transform.localScale = inflatedNow
                ? new Vector3(originalSpriteSize.x * inflateSize, originalSpriteSize.y * inflateSize, originalSpriteSize.z)
                : originalSpriteSize;

            if (bouncyShell.activeSelf != inflatedNow)
            {
                bouncyShell.SetActive(inflatedNow);
            }

            if (inflatedNow)
            {
                bouncyShell.transform.position = transform.position;
            }
            else if (shown == Form.Charging)
            {
                spriteRenderer.sprite = chargeSprite;
            }

            // The charge bar, for the player using him.
            charging = shown == Form.Charging;
            chargeValue = chargeFraction * maximumChargeValue;
            UpdateMeter();
        }

        protected override void Update()
        {
            // Before the base class works out his movement settings, which depend on whether he's charging or inflated.
            UpdateCharging();

            // Let go of a charge before he's drawn, so he isn't inflated for a frame as he leaves the ground.
            if (chargeValue > 0 && !charging)
            {
                Release();
            }

            if (performingAction)
            {
                inflatedUntil = Time.time + deflateDelaySeconds;
            }

            // Worked out once, here, as the base class and the rest of this all go by it. Blocked while the action is held,
            // he inflates as soon as there's room. When he does, he's pushed into the empty space it needs.
            var wantsToInflate = WantsToInflate();

            if (wantsToInflate && !inflated)
            {
                if (TryFindInflatePosition(out var shift))
                {
                    inflated = true;

                    if (shift != Vector2.zero)
                    {
                        SetPosition(rigidBody2D.position + shift);
                    }
                }
            }
            else
            {
                inflated = wantsToInflate;
            }

            base.Update();

            var form = IsInflated() ? Form.Inflated : (charging ? Form.Charging : Form.Normal);

            // His shape, collider and material are only changed when he changes form, as setting them is not free.
            if (form != appliedForm)
            {
                ApplyForm(form);
                appliedForm = form;
            }

            if (form == Form.Inflated)
            {
                bouncyShell.transform.position = transform.position;
            }
            else if (form == Form.Charging)
            {
                spriteRenderer.sprite = chargeSprite;
                chargeValue += chargeValueIncreaseSpeed * Time.deltaTime;
                if (chargeValue > maximumChargeValue)
                {
                    chargeValue = maximumChargeValue;
                }
            }

            UpdateMeter();
        }

        // Puts him in the shape for a form, whatever the one before was.
        private void ApplyForm(Form form)
        {
            if (form == Form.Inflated)
            {
                rigidBody2D.sharedMaterial = bouncyBallMaterial;
                spriteRenderer.transform.localScale = new Vector3(
                    originalSpriteSize.x * inflateSize,
                    originalSpriteSize.y * inflateSize,
                    originalSpriteSize.z
                );

                capsuleCollider.size = originalColliderDimensions * inflateSize;
                ShiftFloorDetectors(new Vector2(0, -0.02f));

                bouncyShell.SetActive(true);
                return;
            }

            ShiftFloorDetectors(Vector2.zero);
            spriteRenderer.transform.localScale = originalSpriteSize;
            rigidBody2D.sharedMaterial = null;
            bouncyShell.SetActive(false);

            capsuleCollider.size = form == Form.Charging
                ? new Vector2(originalColliderDimensions.x, chargingColliderHeight)
                : originalColliderDimensions;
        }

        // Keeps the charge bar up to date: shown while charging, and for a moment after the jump.
        private void UpdateMeter()
        {
            if (!meter)
            {
                return;
            }

            meter.SetLayout(chargeMeter);

            if (charging)
            {
                displayedCharge = maximumChargeValue > 0f ? chargeValue / maximumChargeValue : 0f;
                meterShowUntil = Time.time + chargeMeter.showSeconds;
            }

            meter.Show(
                GetPlayerCamera(),
                displayedCharge,
                chargeMeter.show && Time.time < meterShowUntil,
                Color.Lerp(Color.yellow, new Color(1f, 0.25f, 0f), displayedCharge));
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }

            downPressed = directionValue.y < DownThreshold;
        }

        // Called every frame with whether the button is held. Held on its own, he inflates. Held with down, he
        // deflates and charges his jump instead.
        public override void Action(bool active)
        {
            actionHeld = active;
            performingAction = active || inflateLocked;
        }

        // Called once as the button goes down. A single press lets go of an inflate that was locked on. Pressing twice
        // in quick succession is a double tap, which locks him inflated, unless down is held, as that's a charge.
        public override void FirstActionPress()
        {
            // A new press in mid-air inflates him again, even before he has landed.
            chargedJumpInAir = false;

            var doubleTap = Time.time - lastPressTime <= doubleTapSeconds;

            // A third quick press starts counting afresh, rather than being a double tap with the second.
            lastPressTime = doubleTap ? float.NegativeInfinity : Time.time;

            inflateLocked = doubleTap && !downPressed;
        }

        // The player has moved on to another character, so the buttons they were holding will never be seen let go.
        // A charge in progress is cancelled rather than released. He stays inflated only if he was locked that way.
        public override void ControlLost()
        {
            actionHeld = false;
            downPressed = false;
            charging = false;
            chargeValue = 0;
            chargedJumpInAir = false;
            performingAction = inflateLocked;
        }

        // Charging takes the action button and down together. Letting go of either one releases the jump.
        private void UpdateCharging()
        {
            // He has landed from a charged jump. If the action is still held he inflates again.
            if (chargedJumpInAir && canJump && Time.time - chargedJumpTime > 0.3f)
            {
                chargedJumpInAir = false;
            }

            var wantsToCharge = actionHeld && downPressed;

            if (wantsToCharge)
            {
                if (!charging && canJump)
                {
                    alreadyJumping = true;
                    charging = true;

                    // Charging a jump lets go of a locked inflate, straight away.
                    inflateLocked = false;
                    inflatedUntil = 0f;
                }
            }
            else
            {
                charging = false;
            }
        }

        // Whether he is inflated. He wants to be while the action is held or locked, and for a moment after it's let go,
        // but never while charging, or while in the air after a charged jump. Once inflated he stays that way, but he can't
        // inflate where there isn't the room.
        private bool IsInflated()
        {
            return inflated;
        }

        private bool WantsToInflate()
        {
            return !charging && !chargedJumpInAir && (performingAction || Time.time < inflatedUntil);
        }

        // Finds where he can inflate: where he is if that's clear, and otherwise the nearest place that is, which is no
        // further than he grows, and is empty space for him to be pushed into. Up comes first, as that's the floor. He
        // can't inflate if there is something on opposite sides of him, such as a floor and a ceiling, or a wall each side,
        // as there is nowhere then that his grown shape fits.
        private bool TryFindInflatePosition(out Vector2 shift)
        {
            var scale = transform.lossyScale;
            var growth = (originalColliderDimensions * inflateSize - originalColliderDimensions) * 0.5f;
            var dx = growth.x * Mathf.Abs(scale.x);
            var dy = growth.y * Mathf.Abs(scale.y);

            var shifts = new[]
            {
                Vector2.zero,
                new Vector2(0, dy),
                new Vector2(dx, 0),
                new Vector2(-dx, 0),
                new Vector2(0, -dy),
                new Vector2(dx, dy),
                new Vector2(-dx, dy),
                new Vector2(dx, -dy),
                new Vector2(-dx, -dy)
            };

            foreach (var candidate in shifts)
            {
                if (InflatedShapeFitsAt(candidate))
                {
                    shift = candidate;
                    return true;
                }
            }

            shift = Vector2.zero;
            return false;
        }

        // Whether his inflated shape would be clear of anything solid if he were moved by the shift.
        private bool InflatedShapeFitsAt(Vector2 shift)
        {
            var scale = transform.lossyScale;
            var grownSize = originalColliderDimensions * inflateSize;
            var centre = (Vector2)transform.TransformPoint(capsuleCollider.offset) + shift;

            // Slightly smaller than he'll be, so that touching something is not the same as being in it.
            var size = new Vector2(
                Mathf.Max(grownSize.x * Mathf.Abs(scale.x) - 2f * RoomMargin, 0.01f),
                Mathf.Max(grownSize.y * Mathf.Abs(scale.y) - 2f * RoomMargin, 0.01f)
            );

            return Physics2D.OverlapCapsule(
                centre, size, capsuleCollider.direction, transform.eulerAngles.z, roomFilter, roomOverlaps) == 0;
        }

        private void Release()
        {
            alreadyJumping = false;
            canJump = true;
            var value = 1.0f + chargeValue;

            // The jump has the force of an ordinary one however he is when it's let go, as the action button is
            // usually still held then and the inflated settings are for bouncing, not jumping.
            currentMovementSettings = GetMovementSetting("base", currentFluid);

            Jump(true, value);
            chargeValue = 0;

            chargedJumpInAir = true;
            chargedJumpTime = Time.time;
        }

        protected override string GetMovementMode()
        {
            // Charging comes first: the action button is held then too, but he isn't inflated.
            if (charging)
            {
                return "charging";
            }

            return IsInflated() ? "bouncy" : "base";
        }
    }
}