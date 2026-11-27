using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    [RequireComponent(typeof(BoxCollider2D), typeof(InteractableLadder))]
    public class Ladder : Character
    {
        [Tooltip("The picture at the top of the ladder.")]
        public Sprite topSprite;
        [Tooltip("The picture of each rung between the top and the bottom.")]
        public Sprite middleSprite;
        [Tooltip("The picture at the bottom of the ladder.")]
        public Sprite bottomSprite;

        [Tooltip("How long, in units, the ladder can stretch.")]
        public float maxLength;
        [Tooltip("How fast, in units a second, the ladder stretches and shrinks.")]
        public float extendSpeed;

        [Tooltip("How far, in units, the ladder stretches for each rung it adds.")]
        public float addLadderDistance;

        [Tooltip("Two presses of the action button this close together, in seconds, count as a double tap.")]
        public float doubleTapSeconds = 0.3f;

        // ----- Animation -----
        //
        // Standing, walking and the rest are his ordinary clips, on the whole ladder. Stretched out he's the Action state (the clip for holding it out),
        // and while he's being stretched or shrunk he's Extend or Retract. Those clips are of the top of the ladder, which is where his face is, and
        // the top piece is shown with whatever picture they're on (the whole ladder's own picture is hidden then). His eyes point in random, wrong
        // directions when he does something hard, which is all of those. See Docs/Animation.md.
        public const string ExtendState = "Extend";
        public const string RetractState = "Retract";

        protected override string AnimationState(string commonState)
        {
            if (commonState != CharacterAnimationSet.Action && commonState != CharacterAnimationSet.AirAction)
            {
                return commonState;
            }

            var going = autoState != 0 ? autoState : extendState;

            return going > 0 ? ExtendState : (going < 0 ? RetractState : CharacterAnimationSet.Action);
        }

        private SpriteRenderer topRenderer;

        // Puts the picture his clip is on onto the top piece of the ladder while it's stretched out, which is the part with his face, and its own
        // picture back when it isn't.
        private void ShowTopPicture()
        {
            if (!topRenderer)
            {
                return;
            }

            if (extendLength > 0f && ShowingAnimation && spriteRenderer)
            {
                topRenderer.sprite = spriteRenderer.sprite;
                topRenderer.flipX = spriteRenderer.flipX;
            }
            else
            {
                topRenderer.sprite = topSprite;
                topRenderer.flipX = false;
            }
        }

        private bool extended;

        private Vector2 topSpot;

        private GameObject topLadderObject;
        private GameObject bottomLadderObject;
        private List<GameObject> middleLadderObjects;

        BoxCollider2D boxCollider;
        PolygonCollider2D polygonCollider;

        // 1 is extending, -1 is retracting and 0 is holding still, according to up and down while the action is held.
        private int extendState;

        // Set by a double tap: keeps extending or retracting on its own until it's all the way, or it's stopped.
        private int autoState;

        private bool actionHeld;

        // The up and down being pressed this frame.
        private float directionY;

        // Whether the current press of the action was the second of a double tap, and when the last one began.
        private bool doubleTapHeld;
        private float lastPressTime = float.NegativeInfinity;

        // How far out he is, which the meter shows.
        private float extendLength;

        [Header("Height Meter")]
        [Tooltip("A bar in the upper right of the view of the player using him, showing how far he is extended out of his greatest length. It appears while he is extended or the action is held, and fades a moment after he is back to normal.")]
        public MeterLayout heightMeter = new MeterLayout { showSeconds = 1.5f };

        private ScreenMeter meter;
        private float meterShowUntil;

        private Vector2 originalColliderOffset;
        private Vector2 originalColliderSize;
        
        InteractableLadder interactableLadder;

        protected override void Start()
        {
            base.Start();

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

            topLadderObject.SetActive(false);
            bottomLadderObject.SetActive(false);

            var top = topLadderObject.AddComponent<SpriteRenderer>();
            var bottom = bottomLadderObject.AddComponent<SpriteRenderer>();

            topRenderer = top;

            top.sprite = topSprite;
            bottom.sprite = bottomSprite;

            boxCollider = GetComponent<BoxCollider2D>();
            polygonCollider = GetComponent<PolygonCollider2D>();
            originalColliderOffset = boxCollider.offset;
            originalColliderSize = boxCollider.size;
            
            interactableLadder = GetComponent<InteractableLadder>();

            meter = gameObject.AddComponent<ScreenMeter>();
        }

        // ----- On a machine that has joined someone else's game -----
        //
        // The host works out how far out he is, and sends it, in hundredths of a unit. This machine shows the ladder that long. Climbing it,
        // and what is in the way, are the host's.

        public ushort LengthForNetwork
        {
            get { return (ushort)Mathf.Clamp(Mathf.RoundToInt(extendLength * 100f), 0, ushort.MaxValue); }
        }

        // Called every frame, after he has been moved.
        public void ShowLength(float length)
        {
            if (!topLadderObject || !bottomLadderObject || !spriteRenderer)
            {
                // Not started.
                return;
            }

            extendLength = length;

            if (length > 0f)
            {
                spriteRenderer.enabled = false;
                bottomLadderObject.SetActive(true);
                topLadderObject.SetActive(true);
                bottomLadderObject.transform.position = transform.position;
                topLadderObject.transform.position = new Vector3(transform.position.x, transform.position.y + length, transform.position.z);

                var neededLadders = Mathf.FloorToInt(length / Mathf.Max(addLadderDistance, 0.01f));

                while (middleLadderObjects.Count < neededLadders)
                {
                    var newLadderMiddle = new GameObject("Middle Ladder " + (middleLadderObjects.Count + 1));
                    newLadderMiddle.transform.parent = transform;
                    newLadderMiddle.transform.localScale = Vector3.one;
                    newLadderMiddle.AddComponent<SpriteRenderer>().sprite = middleSprite;
                    middleLadderObjects.Add(newLadderMiddle);
                }

                var points = length / (neededLadders + 1);

                for (int i = 0; i < middleLadderObjects.Count; i++)
                {
                    var needed = i < neededLadders;

                    if (middleLadderObjects[i].activeSelf != needed)
                    {
                        middleLadderObjects[i].SetActive(needed);
                    }

                    if (needed)
                    {
                        middleLadderObjects[i].transform.position = new Vector3(
                            transform.position.x,
                            transform.position.y + points * (i + 1),
                            transform.position.z
                        );
                    }
                }
            }
            else
            {
                spriteRenderer.enabled = true;
                bottomLadderObject.SetActive(false);
                topLadderObject.SetActive(false);

                foreach (var middle in middleLadderObjects)
                {
                    if (middle.activeSelf)
                    {
                        middle.SetActive(false);
                    }
                }
            }

            ShowTopPicture();
            UpdateMeter();
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();

            // A double tap carries on by itself; otherwise it's up and down while the action is held.
            var state = autoState != 0 ? autoState : extendState;

            if (state == 1)
            {
                if (extendLength < maxLength)
                {
                    // Nothing in the way for a unit above the top of it, which isn't counting himself.
                    var blocked = Map.DistanceFromDirection(topLadderObject.transform.position, Vector2.up, 1.0f, false, rigidBody2D) != null;

                    if (!blocked)
                    {
                        extendLength = Mathf.Min(extendLength + extendSpeed * Time.deltaTime, maxLength);
                    }
                    else if (autoState == 1)
                    {
                        //Hit a tile and can't go any further.
                        autoState = 0;
                    }
                }
                else
                {
                    extendLength = maxLength;

                    if (autoState == 1)
                    {
                        autoState = 0;
                    }
                }
            }
            else if (state == -1)
            {
                extendLength -= extendSpeed * Time.deltaTime;
                if (extendLength <= 0)
                {
                    extendLength = 0;

                    if (autoState == -1)
                    {
                        autoState = 0;
                    }
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

                // One more rung every addLadderDistance. Those he has made are kept, and shown only as many as he needs.
                var neededLadders = Mathf.FloorToInt(extendLength / Mathf.Max(addLadderDistance, 0.01f));

                while (middleLadderObjects.Count < neededLadders)
                {
                    var newLadderMiddle = new GameObject("Middle Ladder " + (middleLadderObjects.Count + 1));
                    newLadderMiddle.transform.parent = transform;
                    newLadderMiddle.transform.localScale = Vector3.one;
                    var newSpriteRenderer = newLadderMiddle.AddComponent<SpriteRenderer>();
                    newSpriteRenderer.sprite = middleSprite;
                    middleLadderObjects.Add(newLadderMiddle);
                }

                var points = extendLength / (neededLadders + 1);

                for (int i = 0; i < middleLadderObjects.Count; i++)
                {
                    var needed = i < neededLadders;

                    if (middleLadderObjects[i].activeSelf != needed)
                    {
                        middleLadderObjects[i].SetActive(needed);
                    }

                    if (needed)
                    {
                        middleLadderObjects[i].transform.position = new Vector3(
                            transform.position.x,
                            transform.position.y + points * (i + 1),
                            transform.position.z
                        );
                    }
                }

                var size = extendLength / transform.localScale.y + originalColliderSize.y;
                var offset = extendLength / (2 * transform.localScale.y) + originalColliderOffset.y;

                extended = true;

                boxCollider.size = new Vector2(boxCollider.size.x, size);
                boxCollider.offset = new Vector2(boxCollider.offset.x, offset);

                boxCollider.enabled = true;
                polygonCollider.enabled = false;
            }
            else
            {
                spriteRenderer.enabled = true;
                canMove = true;
                inUse = false;
                bottomLadderObject.SetActive(false);
                topLadderObject.SetActive(false);

                foreach (var middle in middleLadderObjects)
                {
                    if (middle.activeSelf)
                    {
                        middle.SetActive(false);
                    }
                }

                boxCollider.offset = originalColliderOffset;
                boxCollider.size = originalColliderSize;
                boxCollider.enabled = false;
                polygonCollider.enabled = true;

                extended = false;
            }

            interactableLadder.climbable = extended;

            ShowTopPicture();
            UpdateMeter();
        }

        // Keeps the height bar up to date: shown while he is extended or the action is held, and for a moment after.
        private void UpdateMeter()
        {
            if (!meter)
            {
                return;
            }

            meter.SetLayout(heightMeter);

            var fraction = maxLength > 0f ? extendLength / maxLength : 0f;

            if (extendLength > 0f || actionHeld)
            {
                meterShowUntil = Time.time + heightMeter.showSeconds;
            }

            meter.Show(
                GetPlayerCamera(),
                fraction,
                heightMeter.show && Time.time < meterShowUntil,
                new Color(0.3f, 0.7f, 1f),
                extendLength.ToString("0.0"));
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            var yPress = directionValue.y;
            directionY = yPress;

            // Up and down only work the ladder while the action is held, so they can be pressed freely while walking.
            if (actionHeld && yPress < 0)
            {
                RetractLadder();
            }
            else if (actionHeld && yPress > 0)
            {
                ExtendLadder();
            }
            else
            {
                HoldLadder();
            }

            // After a double tap, up or down can be pressed at any point while the button is still down.
            if (doubleTapHeld)
            {
                StartAuto();
            }

            if (canMove)
            {
                Move(directionValue);
            }
        }

        // Called every frame with whether the button is held.
        public override void Action(bool active)
        {
            actionHeld = active;

            if (!active)
            {
                doubleTapHeld = false;
            }
        }

        // Called once as the button goes down. Pressing again soon after is a double tap, which, with up or down held,
        // keeps going until the ladder is fully extended or retracted. Any press stops a ladder that is going by itself.
        public override void FirstActionPress()
        {
            autoState = 0;

            doubleTapHeld = Time.time - lastPressTime <= doubleTapSeconds;

            // A third quick press starts counting afresh, rather than being a double tap with the second.
            lastPressTime = doubleTapHeld ? float.NegativeInfinity : Time.time;

            if (doubleTapHeld)
            {
                StartAuto();
            }
        }

        // The player has moved on to another character. The button they were holding will never be seen let go.
        // A ladder going by itself carries on.
        public override void ControlLost()
        {
            actionHeld = false;
            doubleTapHeld = false;
            extendState = 0;
        }

        private void StartAuto()
        {
            if (directionY > 0)
            {
                autoState = 1;
            }
            else if (directionY < 0)
            {
                autoState = -1;
            }
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