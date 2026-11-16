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
        public bool ableToGrabObjects = true;

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
        private GrabPoint grabPoint;

        private static int characterCount;
        private static List<Character> characters;
        private static List<InteractableObject> interactableObjects;
        private static List<CarryableObject> carryableObjects;

        // The last place he was that no destroyable tile has ever been, which is always somewhere he can be put back.
        private Vector2 lastSafePosition;

        [Tooltip("How fast, in units per second, he is pushed back toward the last safe place while a tile that came back on him is still around him.")]
        public float safePushSpeed = 6f;

        // How far into a tile's space he has to reach to count as being in it, so resting against it doesn't.
        private const float CellMargin = 0.02f;

        // How far into something solid he has to be before it counts as being inside it, as touching is not.
        private const float PenetrationTolerance = 0.02f;

        // The solid colliders of whichever character is being looked at, and what they overlap.
        private static readonly Collider2D[] solidColliders = new Collider2D[16];
        private static readonly Collider2D[] groundOverlaps = new Collider2D[16];

        // While a tile that came back on him is still around him, he is carried toward his last safe place. It doesn't
        // stop for the ground, so what he is pushed through can't push him out somewhere else.
        private bool returningToSafety;
        private RigidbodyType2D bodyTypeBeforeReturn;

        // The spaces of the tiles that came back on him during that, which may not have a collider yet.
        private readonly List<Vector2Int> blockingCells = new List<Vector2Int>();

        protected virtual void Start()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            rigidBody2D = GetComponent<Rigidbody2D>();

            CanTriggerFloorCollapse.EnsureOn(gameObject);

            lastSafePosition = rigidBody2D.position;

            // Every character starts this, but it's taken off first so that it's never on twice.
            Map.TileCreating -= MakeRoomForTile;
            Map.TileCreating += MakeRoomForTile;

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
                    FindObjectsByType<InteractableObject>(FindObjectsInactive.Exclude)
                        .ToList();
            }

            if (carryableObjects == null)
            {
                carryableObjects =
                    FindObjectsByType<CarryableObject>(FindObjectsInactive.Exclude)
                        .ToList();
            }
            
            // A character that can carry things needs somewhere to hold them. One that can't needs nothing.
            if (ableToCarryObjects && !carryAttachmentPoint)
            {
                Debug.LogWarning($"{gameObject.name} is able to carry objects, but has no Carry Attachment Point to hold them by. " +
                                 "Give it one, or untick Able To Carry Objects.", this);
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
            if (returningToSafety)
            {
                PushTowardSafePosition();
                return;
            }

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
                var dist = Map.DistanceFromDirection(detector.transform.position, Vector2.down, 20, false, rigidBody2D);
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
                    // Pushable things aren't walls: walking into them is how they get pushed.
                    var dist = Map.DistanceFromDirection(wallDetector.transform.position, direction, 5, true, rigidBody2D);
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
            RememberSafePosition();

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

        // A tile that can be destroyed may come back while he is where it was, which would leave him stuck in it or
        // pushed through the ground and out into somewhere he can't normally get to. So he keeps hold of the last
        // place he was that has never been a destroyable tile's space, and is pushed back toward it when one returns on
        // him, just as far as it takes to be out of it.
        private void RememberSafePosition()
        {
            if (!returningToSafety && !InDestroyableTileSpace())
            {
                lastSafePosition = rigidBody2D.position;
            }
        }

        // Called just before a tile comes back, so that whoever is where it is going is dealt with before it has a
        // collider that would push them out somewhere else.
        // Whether any character is in the space of a cell, by the very test that decides a returning tile has to make room
        // for them, so that a tile which waits for the space to be clear waits for exactly the characters it would push.
        public static bool AnyCharacterInCell(Vector2Int cell)
        {
            if (characters == null)
            {
                return false;
            }

            foreach (var character in characters)
            {
                if (character && character.isActiveAndEnabled && character.ReachesIntoCell(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static void MakeRoomForTile(Vector2Int cell)
        {
            if (characters == null)
            {
                return;
            }

            foreach (var character in characters)
            {
                if (character && character.isActiveAndEnabled && character.ReachesIntoCell(cell))
                {
                    character.StartReturningToSafety(cell);
                }
            }
        }

        private void StartReturningToSafety(Vector2Int cell)
        {
            if (!blockingCells.Contains(cell))
            {
                blockingCells.Add(cell);
            }

            if (returningToSafety)
            {
                return;
            }

            returningToSafety = true;

            // Nothing pushes a kinematic body out of the ground, so only this moves him until he's clear.
            bodyTypeBeforeReturn = rigidBody2D.bodyType;
            rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
            rigidBody2D.linearVelocity = Vector2.zero;
            rigidBody2D.angularVelocity = 0f;
            moveSpeed = 0;
        }

        // A step toward the last safe place, for as long as he is inside a tile that came back or anything solid. If he
        // is stuck for real, that's all the way. Stood anywhere else, it's as short a way as takes him out.
        private void PushTowardSafePosition()
        {
            if (!IsInsideSomething())
            {
                FinishReturningToSafety();
                return;
            }

            var toSafe = lastSafePosition - rigidBody2D.position;
            var step = safePushSpeed * Time.fixedDeltaTime;

            if (toSafe.magnitude <= step)
            {
                SetPosition(lastSafePosition);
                FinishReturningToSafety();
                return;
            }

            SetPosition(rigidBody2D.position + toSafe.normalized * step);
        }

        private void FinishReturningToSafety()
        {
            returningToSafety = false;
            blockingCells.Clear();

            rigidBody2D.bodyType = bodyTypeBeforeReturn;
            rigidBody2D.linearVelocity = Vector2.zero;
            rigidBody2D.angularVelocity = 0f;
            moveSpeed = 0;
        }

        // Puts him at a place straight away, at rest, and makes it the place he is put back to if he ever has to be: for being
        // warped to a hub, or coming back to life at one. A character who has never been in the game yet has nothing to move
        // but his transform, which is where he begins.
        public void Teleport(Vector2 position)
        {
            if (!rigidBody2D)
            {
                transform.position = new Vector3(position.x, position.y, transform.position.z);
                return;
            }

            SetPosition(position);
            lastSafePosition = position;

            rigidBody2D.linearVelocity = Vector2.zero;
            rigidBody2D.angularVelocity = 0f;
            moveSpeed = 0f;

            // On every other machine too, and straight there, rather than sliding across from where they were.
            var sync = GetComponent<Mirror.NetworkTransformBase>();
            if (sync && sync.isServer)
            {
                sync.ServerTeleport(transform.position, transform.rotation);
            }
        }

        protected void SetPosition(Vector2 position)
        {
            rigidBody2D.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        // Whether he is in the space of a tile that came back on him, in the solid part of the ground, or across the
        // edge of anything solid on the ground layer.
        private bool IsInsideSomething()
        {
            foreach (var cell in blockingCells)
            {
                if (ReachesIntoCell(cell))
                {
                    return true;
                }
            }

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(LayerMask.GetMask("Ground"));

            var count = GetSolidColliders();

            // The ground's colliders are only its outline, so being deep in it touches no edge. The tiles say instead.
            for (int i = 0; i < count; i++)
            {
                if (Map.IsInsideSolid(solidColliders[i].bounds.center))
                {
                    return true;
                }
            }

            for (int i = 0; i < count; i++)
            {
                var collider = solidColliders[i];
                var overlapping = collider.Overlap(filter, groundOverlaps);

                for (int j = 0; j < overlapping; j++)
                {
                    var other = groundOverlaps[j];

                    if (other.attachedRigidbody != rigidBody2D && collider.Distance(other).distance < -PenetrationTolerance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // His colliders that are solid right now. They change with the shape he takes, such as rolling or stretching.
        private int GetSolidColliders()
        {
            var attached = rigidBody2D.GetAttachedColliders(solidColliders);
            var solid = 0;

            for (int i = 0; i < attached; i++)
            {
                var collider = solidColliders[i];

                if (collider.isActiveAndEnabled && !collider.isTrigger)
                {
                    solidColliders[solid++] = collider;
                }
            }

            return solid;
        }

        private bool ReachesIntoCell(Vector2Int cell)
        {
            var count = GetSolidColliders();

            for (int i = 0; i < count; i++)
            {
                var bounds = solidColliders[i].bounds;

                if (bounds.min.x < cell.x + 1 - CellMargin && bounds.max.x > cell.x + CellMargin &&
                    bounds.min.y < cell.y + 1 - CellMargin && bounds.max.y > cell.y + CellMargin)
                {
                    return true;
                }
            }

            return false;
        }

        private bool InDestroyableTileSpace()
        {
            var count = GetSolidColliders();

            for (int i = 0; i < count; i++)
            {
                var bounds = solidColliders[i].bounds;

                var minX = Mathf.FloorToInt(bounds.min.x + CellMargin);
                var maxX = Mathf.FloorToInt(bounds.max.x - CellMargin);
                var minY = Mathf.FloorToInt(bounds.min.y + CellMargin);
                var maxY = Mathf.FloorToInt(bounds.max.y - CellMargin);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        if (DamageableTile.IsDestroyableTileSpace(new Vector2Int(x, y)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void SetSprite()
        {
            isJumping = distFromGround >= 0.2f;

            ShowSprite(facing == CharacterFacing.Left, isJumping, IsPerformingAction());
        }

        // The three things that decide which picture a character is shown with: which way they face, whether they're in the air,
        // and whether they're using their action. They're what is sent to the other machines in a networked game.
        public bool FacingLeft
        {
            get { return facing == CharacterFacing.Left; }
        }

        public bool Airborne
        {
            get { return isJumping; }
        }

        // Shows the character's picture for those three things. A character the game is simulating works them out for itself every
        // frame. One that is only being shown, on a machine that joined someone else's game, is given them.
        public void ShowSprite(bool facingLeft, bool airborne, bool performing)
        {
            if (!spriteRenderer)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

                if (!spriteRenderer)
                {
                    return;
                }
            }

            spriteRenderer.flipX = facingLeft;

            if (airborne)
            {
                spriteRenderer.sprite = performing ? jumpUseSprite : jumpSprite;
            }
            else if (performing)
            {
                spriteRenderer.sprite = actionSprite ? actionSprite : null;
            }
            else if (walkSprite)
            {
                spriteRenderer.sprite = walkSprite;
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

        public virtual void Jump(bool active, float jumpValue = 1.0f)
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

        // A player has just stopped controlling this character. Buttons that were held are no longer being read, so
        // anything waiting for them to be let go should give up.
        public virtual void ControlLost()
        {

        }

        // A player has just taken over this character.
        public virtual void ControlGained()
        {

        }

        private Player lastPlayer;

        // The camera showing the view of the player using this character, for a meter to appear in. After they move
        // on to another character it's still their view, so they can keep an eye on this one. Before anyone has used
        // it, or when views are joined together, it's the main camera.
        protected Camera GetPlayerCamera()
        {
            if (PlayerManager.players != null)
            {
                foreach (var player in PlayerManager.players)
                {
                    if (player && player.character == this)
                    {
                        lastPlayer = player;
                        break;
                    }
                }
            }

            return CameraManager.GetCameraForPlayer(lastPlayer);
        }

        // Called every frame with whether the Interact button is held, for a character that does something with it held down,
        // such as in combination with another button. Interact() below is called once as it goes down.
        public virtual void InteractHold(bool held)
        {
        }

        public virtual void Interact()
        {
            print ($"INTERACT {carryableObjects.Count} | {interactableObjects.Count}");
            if (climbing)
            {
                climbing = false;
                return;
            }

            if (grabPoint)
            {
                var wasHolding = grabPoint.IsGrabbedBy(this);
                grabPoint.Release(this);
                grabPoint = null;

                if (wasHolding)
                {
                    return;
                }
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
            // Nobody has started yet, so there aren't any.
            if (characters == null)
            {
                return new List<Character>();
            }

            if (enabledOnly)
            {
                return characters
                        .Where(c => c.InGame)
                        .ToList();
            }

            return characters;
        }

        // Whether the character is in the game. One that isn't is put away: still there, but hidden, solid to nothing, and doing
        // nothing, until they're brought back. They are never made inactive, since a character has to exist all the time to be
        // part of a game played over a network.
        public bool InGame { get; private set; } = true;

        // Puts the character into the game, or puts them away. This changes at once; they're hidden or shown by the end of the
        // frame.
        public void SetInGame(bool inGame)
        {
            InGame = inGame;

            GetParking().SetParked(!inGame);
        }

        // For a machine that has joined someone else's game: the character is only shown, where the host says they are, doing
        // what the host says. Nothing of them is simulated here. Undone if the machine goes back to playing by itself.
        public void SetShownOnly(bool shownOnly)
        {
            GetParking().SetShownOnly(shownOnly);
        }

        private CharacterParking GetParking()
        {
            var parking = GetComponent<CharacterParking>();

            if (!parking)
            {
                parking = gameObject.AddComponent<CharacterParking>();
            }

            return parking;
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

        public void SetGrab(GrabPoint point)
        {
            grabPoint = point;
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