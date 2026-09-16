using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    public class BowlingBall : Character
    {
        public float rotationDampening;

        private bool isRolling;
        private float rotationAngle;

        private PolygonCollider2D polygonCollider;
        private CircleCollider2D circleCollider;
        private RollDamage rollDamage;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void Start()
        {
            base.Start();

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            polygonCollider = GetComponent<PolygonCollider2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            rollDamage = GetComponent<RollDamage>();

            canMove = true;
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();

            if (isRolling)
            {
                rotationAngle -= moveSpeed * rotationDampening;
                //rotationAngle -= rigidBody.linearVelocity.x * rotationDampening;
                if (rotationAngle >= 360)
                {
                    rotationAngle -= 360;
                }

                spriteRenderer.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
                rollDamage.enabled = true;

                //rigidBody.constraints = RigidbodyConstraints2D.None;
            }
            else
            {
                //rigidBody.constraints = RigidbodyConstraints2D.FreezeRotation;
                rollDamage.enabled = false;
            }

            //print (rigidBody.linearVelocity);
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }

        public override void Action(bool rolling)
        {
            isRolling = rolling;
            performingAction = rolling;

            if (isRolling == false)
            {
                rotationAngle = 0;
                spriteRenderer.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
                polygonCollider.enabled = true;
                circleCollider.enabled = false;
            }
            else
            {
                polygonCollider.enabled = false;
                circleCollider.enabled = true;
            }
        }

        public override void CheckTouchingTiles(List<(Vector2Int, float)> up, List<(Vector2Int, float)> down,
            List<(Vector2Int, float)> left, List<(Vector2Int, float)> right)
        {
            /*
            if (isRolling == false)
            {
                return;
            }

            foreach (var u in up)
            {

            }

            foreach (var d in down)
            {

            }

            foreach (var l in left)
            {

            }

            foreach (var r in right)
            {

            }
            */
        }

        protected override void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            string settingName;

            if (isInLiquid)
            {
                if (isRolling)
                {
                    settingName = "rollingliquid";
                }
                else
                {
                    settingName = "liquid";
                }
            }
            else
            {
                if (isRolling)
                {
                    settingName = "rolling";
                }
                else
                {
                    settingName = "base";
                }
            }

            currentMovementSettings = GetMovementSetting(settingName, density);
        }
    }
}