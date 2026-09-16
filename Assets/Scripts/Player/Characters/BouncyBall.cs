using UnityEngine;

namespace Characters
{
    public class BouncyBall : Character
    {
        PhysicsMaterial2D bouncyBallMaterial;
        public float inflateSize;
        public float maxHeight;
        public GameObject bouncyShell;
        public Sprite chargeSprite;
        public float chargeValueIncreaseSpeed;
        public float maximumChargeValue;

        private CapsuleCollider2D capsuleCollider;
        private Vector3 originalSpriteSize;
        private Vector2 originalColliderDimensions;
        private bool charging;
        public float chargeValue;



        protected override void Start()
        {
            base.Start();
            bouncyBallMaterial = rigidBody2D.sharedMaterial;
            originalSpriteSize = spriteRenderer.transform.localScale;
            bouncyShell.SetActive(false);

            capsuleCollider = GetComponent<CapsuleCollider2D>();
            originalColliderDimensions = capsuleCollider.size;
        }

        protected override void Update()
        {
            base.Update();

            if (transform.position.y > maxHeight)
            {
                maxHeight = transform.position.y;
            }

            if (performingAction && !charging)
            {
                rigidBody2D.sharedMaterial = bouncyBallMaterial;
                spriteRenderer.transform.localScale = new Vector3(
                    originalSpriteSize.x * inflateSize,
                    originalSpriteSize.y * inflateSize,
                    originalSpriteSize.z
                );

                capsuleCollider.size = new Vector2(originalColliderDimensions.x * inflateSize,
                    originalColliderDimensions.y * inflateSize);
                ShiftFloorDetectors(new Vector2(0, -0.02f));

                bouncyShell.SetActive(true);
                bouncyShell.transform.position = transform.position;
            }
            else
            {
                if (charging)
                {
                    capsuleCollider.size = new Vector2(originalColliderDimensions.x, 0.12f);
                    spriteRenderer.sprite = chargeSprite;
                    chargeValue += chargeValueIncreaseSpeed * Time.deltaTime;
                    if (chargeValue > maximumChargeValue)
                    {
                        chargeValue = maximumChargeValue;
                    }
                }
                else
                {
                    ShiftFloorDetectors(Vector2.zero);
                    spriteRenderer.transform.localScale = originalSpriteSize;
                    rigidBody2D.sharedMaterial = null;
                    bouncyShell.SetActive(false);
                    capsuleCollider.size = new Vector2(originalColliderDimensions.x, originalColliderDimensions.y);
                }
            }

            if (chargeValue > 0 && !charging)
            {
                Release();
            }
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }

            if (directionValue.y < 0)
            {
                if (!charging && !performingAction)
                {
                    if (canJump) // && !inLiquid)
                    {
                        alreadyJumping = true;
                        charging = true;
                    }
                }
            }
            else
            {
                charging = false;
            }
        }

        public override void Action(bool active)
        {
            performingAction = active;
        }

        private void Release()
        {
            alreadyJumping = false;
            canJump = true;
            var value = 1.0f + chargeValue;
            Jump(true, value);
            chargeValue = 0;
        }

        protected override void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            string settingName;

            if (isInLiquid)
            {
                if (performingAction)
                {
                    settingName = "bouncyliquid";
                }
                else
                {
                    if (charging)
                    {
                        settingName = "chargingliquid";
                    }
                    else
                    {
                        settingName = "liquid";
                    }
                }
            }
            else
            {
                if (performingAction)
                {
                    settingName = "bouncy";
                }
                else
                {
                    if (charging)
                    {
                        settingName = "charging";
                    }
                    else
                    {
                        settingName = "base";
                    }
                }
            }

            currentMovementSettings = GetMovementSetting(settingName, density);

            //Do not call the base function
        }
    }
}