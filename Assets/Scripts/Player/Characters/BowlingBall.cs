using UnityEngine;

namespace Characters
{
    [RequireComponent(typeof(PolygonCollider2D), typeof(CircleCollider2D), typeof(RollDamage))]
    public class BowlingBall : Character
    {
        private float rotationAngle;

        private PolygonCollider2D polygonCollider;
        private CircleCollider2D circleCollider;
        private RollDamage rollDamage;

        // Whether everything he needs was found. Until then there is nothing to switch.
        private bool ready;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void Start()
        {
            base.Start();

            polygonCollider = GetComponent<PolygonCollider2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            rollDamage = GetComponent<RollDamage>();

            if (!polygonCollider || !circleCollider || !rollDamage)
            {
                Debug.LogError(
                    $"{name}: the Bowling Ball needs a PolygonCollider2D, a CircleCollider2D and a RollDamage.", this);
                return;
            }

            ready = true;

            // The action may already have been pressed before he was ready.
            ApplyRolling();
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();

            if (!ready || !performingAction)
            {
                return;
            }

            // Rolling without slipping: the angle turned is the distance covered along the ground over the radius.
            // It's his real speed, so it also turns when something else moves him, and not when he's held still.
            var radius = circleCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            if (radius > 0f)
            {
                var turned = rigidBody2D.linearVelocity.x * Time.deltaTime / radius * Mathf.Rad2Deg;

                // Rolling right turns clockwise, which is the negative direction.
                rotationAngle = Mathf.Repeat(rotationAngle - turned, 360f);
            }

            spriteRenderer.transform.localRotation = Quaternion.Euler(0, 0, rotationAngle);
        }

        // ----- On a machine that has joined someone else's game -----
        //
        // He has no speed of his own there, so how far he has rolled is worked out from how far he has moved.

        private float shownLastX;
        private bool shownHasLast;

        // Called every frame with whether the host says he's rolling.
        public void ShowRolling(bool rolling)
        {
            if (!ready)
            {
                return;
            }

            if (!rolling)
            {
                rotationAngle = 0f;
                shownHasLast = false;
                spriteRenderer.transform.localRotation = Quaternion.identity;
                return;
            }

            var x = transform.position.x;

            if (shownHasLast)
            {
                var radius = circleCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

                if (radius > 0f)
                {
                    rotationAngle = Mathf.Repeat(rotationAngle - (x - shownLastX) / radius * Mathf.Rad2Deg, 360f);
                    spriteRenderer.transform.localRotation = Quaternion.Euler(0, 0, rotationAngle);
                }
            }

            shownLastX = x;
            shownHasLast = true;
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }

        // Called every frame with whether the button is held, so only a change does anything.
        public override void Action(bool rolling)
        {
            if (rolling == performingAction)
            {
                return;
            }

            performingAction = rolling;

            if (ready)
            {
                ApplyRolling();
            }
        }

        // Puts him into or out of rolling: a ball that does damage, or the ordinary shape.
        private void ApplyRolling()
        {
            rollDamage.enabled = performingAction;
            circleCollider.enabled = performingAction;
            polygonCollider.enabled = !performingAction;

            if (!performingAction)
            {
                rotationAngle = 0;
                spriteRenderer.transform.localRotation = Quaternion.identity;
            }
        }

        protected override string GetMovementMode()
        {
            return performingAction ? "rolling" : "base";
        }
    }
}
