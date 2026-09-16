using System;
using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    public class Ladder : Character
    {
        public Sprite topSprite;
        public Sprite middleSprite;
        public Sprite bottomSprite;

        public float maxLength;
        public float extendSpeed;

        public float addLadderDistance;

        private bool extended;

        private Vector2 topSpot;

        private GameObject topLadderObject;
        private GameObject bottomLadderObject;
        private List<GameObject> middleLadderObjects;

        BoxCollider2D boxCollider;
        PolygonCollider2D polygonCollider;

        private int extendState;

        public float extendLength;

        //private SpriteRenderer top;
        //private SpriteRenderer bottom;
        //private List<SpriteRenderer> middles;

        private Vector2 originalColliderOffset;
        private Vector2 originalColliderSize;
        
        InteractableLadder interactableLadder;

        protected override void Start()
        {
            base.Start();

            //middles = new List<SpriteRenderer>();
            middleLadderObjects = new List<GameObject>();

            topLadderObject = new GameObject
            {
                name = "Top Ladder",
                transform =
                {
                    parent = transform,
                    localScale = Vector3.one
                }
            };

            bottomLadderObject = new GameObject();
            bottomLadderObject.name = "Bottom Ladder";
            bottomLadderObject.transform.parent = transform;
            bottomLadderObject.transform.localScale = Vector3.one;

            //top = topLadderObject.AddComponent<SpriteRenderer>();
            //bottom = bottomLadderObject.AddComponent<SpriteRenderer>();

            topLadderObject.SetActive(false);
            bottomLadderObject.SetActive(false);

            var top = topLadderObject.AddComponent<SpriteRenderer>();
            var bottom = bottomLadderObject.AddComponent<SpriteRenderer>();

            top.sprite = topSprite;
            bottom.sprite = bottomSprite;

            boxCollider = GetComponent<BoxCollider2D>();
            polygonCollider = GetComponent<PolygonCollider2D>();
            originalColliderOffset = boxCollider.offset;
            originalColliderSize = boxCollider.size;
            
            interactableLadder = GetComponent<InteractableLadder>();
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();

            if (extendState == 1)
            {
                if (extendLength < maxLength)
                {
                    var dist = Map.DistanceFromDirection(topLadderObject.transform.position, Vector2.up);

                    if (dist == null || dist > 1.0f)
                    {
                        extendLength += extendSpeed * Time.deltaTime;
                    }
                }
                else
                {
                    extendLength = maxLength;
                }
            }
            else if (extendState == -1)
            {
                extendLength -= extendSpeed * Time.deltaTime;
                if (extendLength < 0)
                {
                    extendLength = 0;
                }
            }

            if (extendLength > 0)
            {
                spriteRenderer.enabled = false;
                canMove = false;
                inUse = true;
                bottomLadderObject.SetActive(true);
                topLadderObject.SetActive(true);
                bottomLadderObject.transform.position = transform.position;
                topLadderObject.transform.position = new Vector3(
                    transform.position.x,
                    transform.position.y + extendLength,
                    transform.position.z
                );

                for (int i = 0; i < middleLadderObjects.Count; i++)
                {
                    middleLadderObjects[i].SetActive(false);
                }

                var neededLadders = (int)Mathf.Floor(extendLength / addLadderDistance);

                if (neededLadders > 0)
                {
                    if (middleLadderObjects.Count < neededLadders)
                    {
                        var newLadderMiddle = new GameObject();
                        newLadderMiddle.transform.parent = transform;
                        newLadderMiddle.transform.localScale = Vector3.one;
                        var newSpriteRenderer = newLadderMiddle.AddComponent<SpriteRenderer>();
                        newSpriteRenderer.sprite = middleSprite;
                        middleLadderObjects.Add(newLadderMiddle);
                    }

                    //distance = topLadderObject.transform.position.y - bottomLadderObject.transform.position.y;
                    //var points = distance / (neededLadders + 1);
                    var points = extendLength / (neededLadders + 1);

                    for (int i = 0; i < neededLadders; i++)
                    {
                        middleLadderObjects[i].SetActive(true);
                        middleLadderObjects[i].transform.position = new Vector3(
                            transform.position.x,
                            transform.position.y + points * (i + 1),
                            transform.position.z
                        );

                        middleLadderObjects[i].name = "Middle Ladder " + (i + 1);
                    }
                }

                var size = extendLength / transform.localScale.y + originalColliderSize.y;
                var offset = extendLength / (2 * transform.localScale.y) + originalColliderOffset.y;

                extended = true;

                boxCollider.size = new Vector2(boxCollider.size.x, size);
                boxCollider.offset = new Vector2(boxCollider.offset.x, offset);
                //boxCollider.isTrigger = true;
                
                boxCollider.enabled = true;
                polygonCollider.enabled = false;
                
                //rigidBody2D.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            else
            {
                spriteRenderer.enabled = true;
                canMove = true;
                inUse = false;
                bottomLadderObject.SetActive(false);
                topLadderObject.SetActive(false);

                boxCollider.offset = originalColliderOffset;
                boxCollider.size = originalColliderSize;
                boxCollider.enabled = false;
                //boxCollider.isTrigger = false;
                polygonCollider.enabled = true;

                extended = false;

                //rigidBody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            interactableLadder.climbable = extended;
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            var yPress = directionValue.y;

            if (yPress < 0)
            {
                RetractLadder();
            }
            else if (yPress > 0)
            {
                ExtendLadder();
            }
            else if ((int)yPress == 0)
            {
                HoldLadder();
            }

            if (canMove)
            {
                Move(directionValue);
            }
        }

        public override void Action(bool active)
        {

        }

        private void ExtendLadder()
        {
            extendState = 1;
        }

        private void RetractLadder()
        {
            extendState = -1;
        }

        private void HoldLadder()
        {
            extendState = 0;
        }

        public bool IsExtended()
        {
            return extended;
        }
    }
}