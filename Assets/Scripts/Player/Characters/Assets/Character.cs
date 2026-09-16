using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Characters
{

    public enum CharacterFacing
    {
        Left,
        Right
    }

    public class Character : MonoBehaviour
    {
        public Sprite walkSprite;
        public Sprite jumpSprite;
        public Sprite actionSprite;
        public Sprite interactSprite;
        public Sprite jumpUseSprite;
        
        public bool ableToClimbObjects = true;
        public bool ableToCarryObjects = true;

        public Rigidbody2D carryAttachmentPoint; 

        private bool holdingJump;
        //Eventually make protected.  public for testing
        public bool canJump;
        public bool isJumping;
        public bool alreadyJumping;
        public bool isFalling;
        private float maxFallSpeed;
        
        public bool inLiquid;

        private float distFromGround;

        protected float moveSpeed;
        protected bool canMove;

        //public float currentSpeed;
        public ParticleSystem footDust;

        protected string currentMovementSettingName;

        protected MovementSettings currentMovementSettings;
        protected SpriteRenderer spriteRenderer;
        protected Rigidbody2D rigidBody2D;
        protected bool performingAction;
        protected bool inUse;

        private CharacterFacing facing = CharacterFacing.Right;
        private float timeOfLastJump;

        private GameObject floorDetectorContainer;
        private GameObject wallDetectorContainer;
        private List<Detector> floorDetectors;
        private List<Detector> wallDetectors;
        private Vector3 originalFloorDetectorPosition;
        private Vector3 originalWallDetectorPosition;

        private Dictionary<string, MovementSettings> movementSettings;
        private MovementSettings overrideMovementSettings;
        private DamageableEntity hitPoints;

        private int characterNum;

        private bool climbing;
        private InteractableLadder interactingLadder;
        private CarryableObject objectCarrying;
        
        private static int characterCount;
        private static List<Character> characters;
        private static List<InteractableObject> interactableObjects;
        private static List<CarryableObject> carryableObjects;

        protected virtual void Start()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            rigidBody2D = GetComponent<Rigidbody2D>();

            var detectors = GetComponentsInChildren<Detector>();
            floorDetectors = detectors
                .Where(d => d.type == DetectorType.Floor)
                .ToList();

            wallDetectors = detectors
                .Where(d => d.type == DetectorType.Wall)
                .ToList();

            floorDetectorContainer = floorDetectors.First().transform.parent.gameObject;
            wallDetectorContainer = wallDetectors.First().transform.parent.gameObject;
            originalFloorDetectorPosition = floorDetectorContainer.transform.localPosition;
            originalWallDetectorPosition = wallDetectorContainer.transform.localPosition;

            timeOfLastJump = Time.realtimeSinceStartup;
            canMove = true;

            characterNum = characterCount++;
            var footDustObj = GameHelper.ChildObjectsWithTag(gameObject, "FootDust").FirstOrDefault();
            footDust = footDustObj?.GetComponent<ParticleSystem>();

            var allMovements = GetComponentsInChildren<MovementSettings>();
            movementSettings = new Dictionary<string, MovementSettings>();
            var baseMovementSettings = allMovements.FirstOrDefault(m => m.identifier.ToLower() == "base");
            if (!baseMovementSettings)
            {
                print("ERROR: No base movement settings found on character " + gameObject.name);
            }
            /*else
            {
                baseMovementSettings.Initialize("base");
            }*/

            foreach (var ms in allMovements)
            {
                var settingsName = ms.identifier.ToLower();
                if (movementSettings.ContainsKey(settingsName))
                {
                    print("ERROR on character " + gameObject.name + " has a duplicate movement setting: " +
                          settingsName);
                    continue;
                }

                /*if (settingsName != "base")
                {
                    ms.Initialize(settingsName, baseMovementSettings);
                }*/

                movementSettings.Add(ms.identifier.ToLower(), ms);
            }

            currentMovementSettings = baseMovementSettings;
            if (characters == null)
            {
                characters = new List<Character>();
            }

            if (interactableObjects == null)
            {
                interactableObjects =
                    FindObjectsByType<InteractableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .ToList();
            }

            if (carryableObjects == null)
            {
                carryableObjects =
                    FindObjectsByType<CarryableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .ToList();
            }
            
            hitPoints = GetComponent<DamageableEntity>();
            if (!hitPoints)
            {
                print($"ERROR:  Character {gameObject.name} has no DamageableEntity component!");
            }
            else
            {
                if (hitPoints.GetMaxHitPoints() == 0)
                {
                    print($"ERROR:  Character {gameObject.name} has a max hit points of 0!");
                }
            }

            characters.Add(this);
        }

        protected virtual void FixedUpdate()
        {
            if (Mathf.Abs(rigidBody2D.linearVelocityY) < 0.0001f)
            {
                rigidBody2D.linearVelocityY = 0;
            }

            if (Mathf.Abs(rigidBody2D.linearVelocityX) < 0.0001f)
            {
                rigidBody2D.linearVelocityX = 0;
            }

            if (climbing)
            {
                Climb();
                rigidBody2D.gravityScale = 0;
                return;
            }

            //currentSpeed = rigidBody2D.linearVelocity.x;
            //print(rigidBody2D.linearVelocity);   

            distFromGround = float.MaxValue;

            foreach (var detector in floorDetectors)
            {
                //var dist = currentMap.DistanceFromGround(detector.transform.position);
                var dist = Map.DistanceFromDirection(detector.transform.position, Vector2.down);
                if (dist != null)
                {
                    if (dist < distFromGround)
                    {
                        distFromGround = (float)dist;
                    }
                }
            }

            if (
                (isFalling && rigidBody2D.linearVelocity.y > 0.1f) ||
                (isFalling && distFromGround < 1.0f)
            )
            {
                FallDamage(maxFallSpeed);
                maxFallSpeed = 0;
            }

            canJump =
                distFromGround < 0.2f &&
                rigidBody2D.linearVelocity.y < 0.01f &&
                Time.realtimeSinceStartup - timeOfLastJump > 0.25f;

            isJumping =
                distFromGround >= 1.0f &&
                rigidBody2D.linearVelocity.y != 0;

            isFalling = distFromGround >= 1.0f &&
                rigidBody2D.linearVelocity.y < 0;

            if (rigidBody2D.linearVelocity.y < 0)
            {
                maxFallSpeed = -rigidBody2D.linearVelocity.y;
            }
            
            if (moveSpeed != 0)
            {
                var direction = moveSpeed > 0 ? Vector2.right : Vector2.left;

                var distance = float.PositiveInfinity;
                foreach (var wallDetector in wallDetectors)
                {
                    var dist = Map.DistanceFromDirection(wallDetector.transform.position, direction, 5);
                    if (dist != null && dist < distance)
                    {
                        distance = (float)dist;
                    }
                }

                var isWall = distance <= 0.75f;

                if (isWall == false)
                {
                    var newVelocity = rigidBody2D.linearVelocity.x + moveSpeed;
                    if (moveSpeed < 0 && newVelocity < moveSpeed)
                    {
                        newVelocity = moveSpeed;
                    }
                    else if (moveSpeed > 0 && newVelocity > moveSpeed)
                    {
                        newVelocity = moveSpeed;
                    }

                    rigidBody2D.linearVelocityX = newVelocity;
                }
                else
                {
                    moveSpeed = 0;
                }
            }

            if (moveSpeed > 0)
            {
                moveSpeed -= currentMovementSettings.moveSpeedDecay;
                if (moveSpeed < 0)
                {
                    moveSpeed = 0;
                    rigidBody2D.linearVelocity = new Vector2(0, rigidBody2D.linearVelocity.y);
                }
            }
            else if (moveSpeed < 0)
            {
                moveSpeed += currentMovementSettings.moveSpeedDecay;
                if (moveSpeed > 0)
                {
                    moveSpeed = 0;
                    rigidBody2D.linearVelocity = new Vector2(0, rigidBody2D.linearVelocity.y);
                }
            }

            float fallVelocity;
            if (rigidBody2D.linearVelocity.y <= 0)
            {
                //Falling
                //Physics2D.gravity.y is a negative number (-9.81)
                fallVelocity = Physics2D.gravity.y * rigidBody2D.gravityScale *
                               (currentMovementSettings.fallAcceleration - 1) * Time.deltaTime;
            }
            else
            {
                //Rising
                if (holdingJump)
                {
                    fallVelocity = Physics2D.gravity.y * rigidBody2D.gravityScale *
                                   (currentMovementSettings.heldJumpDecay - 1) * Time.deltaTime;
                }
                else
                {
                    fallVelocity = Physics2D.gravity.y * rigidBody2D.gravityScale *
                                   (currentMovementSettings.releasedJumpDecay - 1) * Time.deltaTime;
                }
            }

            rigidBody2D.linearVelocity += new Vector2(0, fallVelocity);

            //print(-currentMovementSettings.maxFallSpeed + "   " + rigidBody2D.linearVelocity.y);
            if (rigidBody2D.linearVelocity.y < -currentMovementSettings.maxFallSpeed)
            {
                rigidBody2D.linearVelocity =
                    new Vector2(rigidBody2D.linearVelocity.x, -currentMovementSettings.maxFallSpeed);
            }
        }

        protected virtual void Update()
        {
            GetTouchingTiles();
            SetSprite();

            var (isInLiquid, density) = Map.IsInLiquid(transform.position);
            GetCurrentMovementSetting(isInLiquid, density);

            inLiquid = isInLiquid;

            if (footDust)
            {
                var module = footDust.emission;
                if (canJump)
                {
                    module.rateOverTime = Mathf.Abs(rigidBody2D.linearVelocity.x) * 3.0f;
                }
                else
                {
                    module.rateOverTime = 0;
                }
            }
        }

        public void GetTouchingTiles()
        {
            var down = new List<(Vector2Int, float)>();
            var up = new List<(Vector2Int, float)>();
            var right = new List<(Vector2Int, float)>();
            var left = new List<(Vector2Int, float)>();

            foreach (var floorDetector in floorDetectors)
            {
                var upTile = Map.GetTilePositionInDirection(floorDetector.transform.position, Vector2.up);
                if (upTile.position != null && upTile.distance != null)
                {
                    up.Add(
                        ((Vector2Int)upTile.position, (float)upTile.distance)
                    );
                }

                var downTile = Map.GetTilePositionInDirection(floorDetector.transform.position, Vector2.down);
                if (downTile.position != null && downTile.distance != null)
                {
                    down.Add(
                        ((Vector2Int)downTile.position, (float)downTile.distance)
                    );
                }
            }

            foreach (var wallDetector in wallDetectors)
            {
                var leftTile = Map.GetTilePositionInDirection(wallDetector.transform.position, Vector2.left);
                if (leftTile.position != null && leftTile.distance != null)
                {
                    left.Add(
                        ((Vector2Int)leftTile.position, (float)leftTile.distance)
                    );
                }

                var rightTile = Map.GetTilePositionInDirection(wallDetector.transform.position, Vector2.right);
                if (rightTile.position != null && rightTile.distance != null)
                {
                    right.Add(
                        ((Vector2Int)rightTile.position, (float)rightTile.distance)
                    );
                }
            }

            up = up.Distinct().ToList();
            down = down.Distinct().ToList();
            left = left.Distinct().ToList();
            right = right.Distinct().ToList();

            CheckTouchingTiles(up, down, left, right);
            CheckCollapseTiles(up, down, left, right);
        }

        private void SetSprite()
        {
            if (facing == CharacterFacing.Left)
            {
                spriteRenderer.flipX = true;
            }
            else
            {
                spriteRenderer.flipX = false;
            }
            
            isJumping = distFromGround >= 0.2f;

            if (isJumping)
            {
                if (IsPerformingAction())
                {
                    spriteRenderer.sprite = jumpUseSprite;
                }
                else
                {
                    spriteRenderer.sprite = jumpSprite;
                }
            }
            else
            {
                if (IsPerformingAction())
                {
                    if (actionSprite)
                    {
                        spriteRenderer.sprite = actionSprite;
                    }
                    else
                    {
                        spriteRenderer.sprite = null;
                    }
                }
                else
                {
                    if (walkSprite)
                    {
                        spriteRenderer.sprite = walkSprite;
                    }
                }
            }
        }

        public bool CanJump()
        {
            return canJump;
        }

        public bool IsPerformingAction()
        {
            return performingAction || inUse;
        }

        public bool CanMove()
        {
            return canMove;
        }

        public void Jump(bool active, float jumpValue = 1.0f)
        {
            if (active && canMove)
            {
                if (
                    (canJump && !alreadyJumping) ||
                    climbing
                )
                {
                    climbing = false;
                    holdingJump = true;

                    rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x,
                        currentMovementSettings.jumpForce * jumpValue);

                    timeOfLastJump = Time.realtimeSinceStartup;
                    alreadyJumping = true;
                }
            }
            else
            {
                holdingJump = false;
                alreadyJumping = !canJump && isJumping;
            }
        }

        protected void Move(Vector2 direction)
        {
            if (climbing)
            {
                var speed = currentMovementSettings.climbSpeed * Time.deltaTime;

                transform.position = new Vector3(
                    transform.position.x + direction.x * speed,
                    transform.position.y + direction.y * speed,
                    transform.position.z
                );
                return;
            }
            
            var xPress = direction.x;

            if (xPress < 0)
            {
                facing = CharacterFacing.Left;
                moveSpeed -= currentMovementSettings.moveSpeedIncrease;
                if (moveSpeed <= -currentMovementSettings.maxMoveSpeed)
                {
                    moveSpeed = -currentMovementSettings.maxMoveSpeed;
                }
            }
            else if (xPress > 0 && canMove)
            {
                facing = CharacterFacing.Right;
                moveSpeed += currentMovementSettings.moveSpeedIncrease;
                if (moveSpeed >= currentMovementSettings.maxMoveSpeed)
                {
                    moveSpeed = currentMovementSettings.maxMoveSpeed;
                }
            }
        }

        public virtual void DirectionPress(Vector2 directionValue)
        {

        }

        public virtual void Action(bool active)
        {

        }

        public virtual void FirstActionPress()
        {

        }

        public virtual void Interact()
        {

            if (climbing)
            {
                climbing = false;
                return;
            }

            if (objectCarrying)
            {
                objectCarrying.Drop();
                objectCarrying = null;
                return;
            }

            foreach (var co in carryableObjects)
            {
                var carry = co.Carry(this);
                if (carry)
                {
                    objectCarrying = carry;
                    //Can only carry one object.  If they pick something up, they can't interact
                    return;
                }
            }

            foreach (var io in interactableObjects)
            {
                var interacting = io.Interact(this);
                if (interacting)
                {
                    //Can only interact with one object
                    return;
                }
            }
        }

        public virtual void CheckTouchingTiles(
            List<(Vector2Int, float)> up,
            List<(Vector2Int, float)> down,
            List<(Vector2Int, float)> left,
            List<(Vector2Int, float)> right
        )
        {

        }

        protected void CheckCollapseTiles(
            List<(Vector2Int, float)> up,
            List<(Vector2Int, float)> down,
            List<(Vector2Int, float)> left,
            List<(Vector2Int, float)> right
        )
        {
            //TODO Add another feature to the editor to make it so that the tiles can support a limited weight.
            //Maybe a character of a mass of 20 can cross but not a mass of 50
            foreach (var d in down)
            {
                float collapseDistance = 0.25f;

                //var distance = Vector2.Distance(transform.position, new Vector2(d.x + 0.5f, d.y + 0.5f));
                var distance = d.Item2;

                //print(distance);

                if (distance < collapseDistance)
                {
                    //TileMonitor.CollapseTouch(d.Item1);
                }
            }
        }

        public int GetCharacterNum()
        {
            return characterNum;
        }

        public MovementSettings GetMovementSetting(string settingsName, float density)
        {
            var lower = settingsName.ToLower();
            MovementSettings settings;
            if (!movementSettings.TryGetValue(lower, out settings))
            {
                print($"No movement settings called \"{settingsName}:{lower}\" on character {name}");

                if (!movementSettings.TryGetValue("base", out settings))
                {
                    print($"No movement settings called \"base\" on character {name}");
                }
            }

            currentMovementSettingName = settingsName;

            if (settings)
            {
                rigidBody2D.gravityScale = settings.gravity;
                rigidBody2D.angularDamping = settings.angularDampening;
                rigidBody2D.linearDamping = settings.linearDampening * density;
            }

            return settings;
        }

        protected virtual void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            string settingName;
            if (isInLiquid)
            {
                settingName = "liquid";
            }
            else
            {
                settingName = "base";
            }

            currentMovementSettings = GetMovementSetting(settingName, density);
        }

        public void SetMovementSettingsOverride(MovementSettings overrideSettings)
        {
            overrideMovementSettings = overrideSettings;

            //TODO Figure out how to blend movement settings.  Such as being underwater AND frozen
        }

        public static List<Character> GetCharacters(bool enabledOnly)
        {
            if (enabledOnly)
            {
                return characters
                        .Where(c => c.gameObject.activeSelf)
                        .ToList();                
            }

            return characters;
        }

        protected void ShiftFloorDetectors(Vector2 shift)
        {
            floorDetectorContainer.transform.localPosition = new Vector3(
                originalFloorDetectorPosition.x + shift.x,
                originalFloorDetectorPosition.y + shift.y,
                originalFloorDetectorPosition.z
            );
        }

        public void SetClimb(InteractableLadder ladder)
        {
            if (climbing || !ableToClimbObjects)
            {
                //Can't climb again
                return;
            }
            
            rigidBody2D.linearVelocity = Vector2.zero;
            moveSpeed = 0;

            climbing = true;
            interactingLadder = ladder;
        }

        private void Climb()
        {
            rigidBody2D.linearVelocity = Vector2.zero;
            if (interactingLadder.climbable == false)
            {
                climbing = false;
            }

            var bounds = interactingLadder.GetBounds();
            if (!bounds.Contains(transform.position))
            {
                climbing = false;
            }
        }

        public Rigidbody2D GetCarryPoint()
        {
            return carryAttachmentPoint;
        }

        private void FallDamage(float fallSpeed)
        {
            float minFallSpeed = 50;
            if (fallSpeed <= minFallSpeed)
            {
                return;
            }
            
            fallSpeed -= minFallSpeed;

            float damage = (Mathf.Pow(fallSpeed, 2) * rigidBody2D.mass) * 0.001f;
            print(gameObject.name + "  " + damage);
            hitPoints.Damage(damage);
        }
    }
}