using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    public class SuctionCup : Character
    {
        public Sprite wallHoldSprite;
        public Sprite ceilingHoldSprite;
        public float wallDrag;
        public float wallClimbForce;
        private bool wallHold;
        private bool ceilingHold;

        /*protected override void Start()
        {
            base.Start();
        }*/

        protected override void Update()
        {
            base.Update();

            if (!performingAction)
            {
                wallHold = false;
                ceilingHold = false;
            }

            if (wallHold)
            {
                spriteRenderer.sprite = wallHoldSprite;
                //rigidBody2D.linearVelocityY = 0;
                rigidBody2D.linearDamping = wallDrag;
            }

            if (ceilingHold)
            {
                spriteRenderer.sprite = ceilingHoldSprite;
                rigidBody2D.constraints = RigidbodyConstraints2D.FreezeAll;
                rigidBody2D.gravityScale = -rigidBody2D.gravityScale;
            }
            else
            {
                rigidBody2D.gravityScale = Mathf.Abs(rigidBody2D.gravityScale);
                rigidBody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            var yPress = directionValue.y;

            if (yPress < 0)
            {
                //Down
                if (wallHold)
                {
                    float force = yPress * wallClimbForce;
                    rigidBody2D.AddForce(new Vector2(0, force));
                }
            }
            else if (yPress > 0)
            {
                //Up
                if (wallHold)
                {
                    float force = yPress * wallClimbForce;
                    rigidBody2D.AddForce(new Vector2(0, force));
                }
            }
            else if ((int)yPress == 0)
            {

            }

            if (canMove)
            {
                Move(directionValue);
            }
        }

        public override void Action(bool active)
        {
            performingAction = active;
        }

        public override void CheckTouchingTiles(List<(Vector2Int, float)> up, List<(Vector2Int, float)> down,
            List<(Vector2Int, float)> left, List<(Vector2Int, float)> right)
        {
            if (!performingAction)
            {
                return;
            }

            //var suctionDist = 1.5f;

            wallHold = false;
            ceilingHold = false;

            /*foreach (var l in left)
            {
                var dist = Vector2.Distance(new Vector2(l.x + 0.5f, l.y + 0.5f), transform.position);
                if (dist <= suctionDist)
                {
                    facing = CharacterFacing.Left;
                    wallHold = true;
                }
            }

            foreach (var r in right)
            {
                var dist = Vector2.Distance(new Vector2(r.x + 0.5f, r.y + 0.5f), transform.position);
                if (dist <= suctionDist)
                {
                    facing = CharacterFacing.Right;
                    wallHold = true;
                }
            }

            foreach (var u in up)
            {
                var dist = Vector2.Distance(new Vector2(u.x + 0.5f, u.y + 0.5f), transform.position);
                if (dist <= suctionDist)
                {
                    ceilingHold = true;
                    //Can't have both
                    wallHold = false;
                }
            }*/
        }
    }
}