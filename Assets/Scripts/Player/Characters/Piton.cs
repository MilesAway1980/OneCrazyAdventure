using UnityEngine;

namespace Characters
{
    public class Piton : Character
    {
        public float maxRopeLength;
        public GameObject throwPointer;
        public float throwAngleChangeSpeed;
        public Sprite feet;
        public Sprite head;
        public float throwForce;

        public GameObject ropeHead;
        public GameObject ropeTail;

        private bool throwing;
        private bool thrown;

        protected override void Start()
        {
            if (!throwPointer)
            {
                print("Piton's throw pointer gameobject is null!");
            }

            base.Start();
        }
        

        public override void DirectionPress(Vector2 directionValue)
        {
            if (performingAction)
            {
                if (thrown == false)
                {
                    throwing = true;
                    throwPointer.transform.position = new Vector3(
                        throwPointer.transform.position.x + directionValue.x * throwAngleChangeSpeed * Time.deltaTime,
                        throwPointer.transform.position.y + directionValue.y * throwAngleChangeSpeed * Time.deltaTime,
                        throwPointer.transform.position.z
                    );
                    
                    var throwAngle = GameMath.GetAngle(transform.position, throwPointer.transform.position);
                    
                    var dist = Vector2.Distance(throwPointer.transform.position, transform.position);
                    if (dist > maxRopeLength)
                    {
                        throwPointer.transform.position = GameMath.MoveTowardsAngle(transform.position, throwAngle, maxRopeLength);
                    }

                    throwPointer.SetActive(true);
                }

                return;
            }
            else
            {
                if (throwing)
                {
                    if (thrown == false)
                    {
                        var throwDirection = throwPointer.transform.position - transform.position;
                        
                        var throwObject = new GameObject
                        {
                            transform =
                            {
                                parent = transform,
                                position = transform.position
                            }
                        };

                        throwObject.transform.localScale = Vector3.one;

                        var sr = throwObject.AddComponent<SpriteRenderer>();
                        var rb = throwObject.AddComponent<Rigidbody2D>();
                        var col = throwObject.AddComponent<CircleCollider2D>();
                        rb.angularVelocity = -500;
                        
                        sr.sprite = head;
                        col.isTrigger = true;
                        
                        rb.AddForce(throwDirection * throwForce, ForceMode2D.Impulse);
                        
                        ropeHead.transform.parent = throwObject.transform;
                        
                        thrown = true;
                    }
                }

                throwPointer.transform.position = transform.position;
                throwPointer.SetActive(false);
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
    }
}