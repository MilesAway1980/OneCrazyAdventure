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

        // How fast he is walking, and which way: it ramps up while the player steers and down when they let go, and the walking
        // step puts it into his speed. Anything that sets it to a speed of its own (a drill coasting, a brick going solid) is
        // listened to: the number is its own from then on.
        protected float moveSpeed;
        protected bool canMove;

        // The walking numbers (moveSpeedIncrease and moveSpeedDecay) are how much speed is gained or lost in a step this long,
        // however long the steps really are.
        private const float WalkStep = 0.02f;

        // Speed beyond what walking puts in, such as a shove, a moving platform or a slide, is only kept if it's this much.
        // Less than that is friction and the physics settling, which walking takes over from.
        private const float ExternalSpeedMargin = 1f;

        // Which way the player is steering (-1, 0 or 1), and the frame they last said so. The steering is read a frame at a time
        // but the walking is worked out a physics step at a time, so it's kept until the next frame says otherwise. If nothing
        // says so for a frame, nobody is steering him any more.
        private float walkInput;
        private int walkInputFrame = -10;

        // What the walking put into his speed in the last step, and what moveSpeed was left at, to tell what's since been
        // added by something else.
        private float walkApplied;
        private float lastMoveSpeed;

        // What he's standing on, from what he's touching (see SupportTracker), and, riding something that moves such as a platform,
        // how much of his sideways speed is its, so that his own can be told from it.
        private readonly SupportTracker support = new SupportTracker();
        private bool rideWasRiding;
        private bool rideMeasured;
        private float rideSpeed;

        // Whether a kinematic body, such as a platform or a bridge standing up, is touching him on that side.
        private bool wallOnLeft;
        private bool wallOnRight;

        // He has to be this close to the ground, if the ground that carried him goes, to be taken to be standing on other ground.
        private const float StillOnGround = 0.3f;

        //public float currentSpeed;
        public ParticleSystem footDust;

        [Header("Movement")]
        [Tooltip("How this character moves: what it's made of and how much it weighs. Make one with Tools > Movement, or Create > Movement > Profile.")]
        public MovementProfile movementProfile;

        protected string currentMovementSettingName;

        // How he moves just now, worked out from his profile for what he's doing and what he's in. It's the one object, refilled,
        // so it's only good until the next time it's asked for.
        protected ResolvedMovement currentMovementSettings;
        private readonly ResolvedMovement resolvedMovement = new ResolvedMovement();

        // The liquid his middle is in, or none in the air.
        protected Fluid currentFluid;

        // How much of him is in a liquid, from 0 to 1, which gives him buoyancy and lets him kick through it.
        private float submerged;
        private static readonly Collider2D[] bodyColliders = new Collider2D[8];
        private const int SubmersionSamples = 5;
        private const float SwimmingSubmersion = 0.3f;

        // Slows him, as being frozen does: 1 is not at all, and 0 stops him.
        private float speedScale = 1f;
        private float jumpScale = 1f;
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

#if UNITY_EDITOR
        // In the editor, when something is changed in the Inspector: a Carry Attachment Point that isn't Kinematic is made so, and
        // said so, straight away. (When playing, Start does it.) It's done a moment later, as changing other objects while this is
        // called isn't allowed to be relied on.
        protected virtual void OnValidate()
        {
            if (Application.isPlaying || !carryAttachmentPoint || carryAttachmentPoint.bodyType == RigidbodyType2D.Kinematic)
            {
                return;
            }

            var character = this;
            var point = carryAttachmentPoint;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (!character || !point || point.bodyType == RigidbodyType2D.Kinematic)
                {
                    return;
                }

                UnityEditor.Undo.RecordObject(point, "Make Carry Attachment Point Kinematic");
                point.bodyType = RigidbodyType2D.Kinematic;
                UnityEditor.EditorUtility.SetDirty(point);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(point);

                Debug.LogWarning($"{character.name}'s Carry Attachment Point was not Kinematic, so it has been made Kinematic. " +
                                 "What it carries would have been left behind otherwise.", point);
            };
        }
#endif

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

            timeOfLastJump = Time.time;
            canMove = true;

            characterNum = characterCount++;
            var footDustObj = GameHelper.ChildObjectsWithTag(gameObject, "FootDust").FirstOrDefault();
            footDust = footDustObj?.GetComponent<ParticleSystem>();

            if (!movementProfile)
            {
                Debug.LogError($"{gameObject.name} has no Movement Profile, so it's been given a plain one. " +
                               "Make one with Tools > Movement > Create Profiles For Characters, or Create > Movement > Profile.", this);
                movementProfile = ScriptableObject.CreateInstance<MovementProfile>();
            }

            if (movementProfile.applyMass)
            {
                rigidBody2D.useAutoMass = false;
                rigidBody2D.mass = movementProfile.mass;
            }

            currentMovementSettings = GetMovementSetting("base", null);

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

            // The carry point has to move with the character, and only a kinematic body does when it's a child of one that moves. A
            // dynamic one is a body of its own: it falls and is left behind, and whatever is joined to it is left with it.
            if (carryAttachmentPoint && carryAttachmentPoint.bodyType != RigidbodyType2D.Kinematic)
            {
                Debug.LogWarning($"{gameObject.name}'s Carry Attachment Point was not Kinematic, so what it carried would be left behind. " +
                                 "It has been made Kinematic. Set its Body Type to Kinematic to stop this message.", carryAttachmentPoint);
                carryAttachmentPoint.bodyType = RigidbodyType2D.Kinematic;
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
            // Only shown, so the host does the moving.
            if (ShownOnly)
            {
                return;
            }

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

            ApplyBuoyancy();

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

            // Standing on anything solid, whatever layer it's on, is standing on the ground: a platform, a crate, a bridge, another
            // character. The detectors above only look for the Ground layer, which they'd otherwise never see. What's under him can
            // also be moving, and if so carries him (below).
            var ride = FindSupport(out var riding, out var measured, out var standing);

            if (standing)
            {
                distFromGround = 0f;
            }

            if (
                (isFalling && rigidBody2D.linearVelocity.y > 0.1f) ||
                (isFalling && distFromGround < 1.0f)
            )
            {
                FallDamage(maxFallSpeed);
                maxFallSpeed = 0;
            }

            // In a liquid he can kick off from nothing, which is how he swims up, and how something that floats climbs out. On
            // something that's going up he isn't rising unless he's rising faster than it is.
            canJump =
                ((distFromGround < 0.2f && rigidBody2D.linearVelocity.y - ride.y < 0.01f) || submerged >= SwimmingSubmersion) &&
                Time.time - timeOfLastJump > 0.25f;

            isJumping =
                distFromGround >= 1.0f &&
                rigidBody2D.linearVelocity.y != 0;

            isFalling = distFromGround >= 1.0f &&
                rigidBody2D.linearVelocity.y < 0;

            if (rigidBody2D.linearVelocity.y < 0)
            {
                maxFallSpeed = -rigidBody2D.linearVelocity.y;
            }
            
            // What he's standing on carries him, if it moves. His own speed, the walking, is worked out without it, and it's added
            // back after, so he can walk about on a platform as he would on the ground, and jump from it.
            if (riding && measured && !rideMeasured)
            {
                // The first step that the ground's speed is known. His speed is the ground's or his own, whichever makes him the
                // slower on it: landing on a platform from the ground he's carried along, and coming back down on one that has been
                // carrying him he isn't carried twice.
                var speed = rigidBody2D.linearVelocity.x;
                rideSpeed = Mathf.Abs(speed) < Mathf.Abs(speed - ride.x) ? 0f : ride.x;
            }
            else if (!riding && rideWasRiding)
            {
                // Got off. In the air he keeps the speed he was carried at; on other ground he doesn't.
                if (distFromGround < StillOnGround)
                {
                    rigidBody2D.linearVelocityX -= rideSpeed;
                }

                rideSpeed = 0f;
            }

            rideWasRiding = riding;
            rideMeasured = riding && measured;
            rigidBody2D.linearVelocityX -= rideSpeed;

            ApplyWalking();
            lastMoveSpeed = moveSpeed;

            rigidBody2D.linearVelocityX += ride.x;
            rideSpeed = ride.x;

            // The extra pull is on his own weight, not on what a liquid holds him up by. It's left off while any of him is in a
            // liquid, or something floating at the surface would weigh as much as when it falls, and settle too deep.
            var pull = submerged > 0f ? 0f : Physics2D.gravity.y * currentMovementSettings.gravity;

            float fallVelocity;
            if (rigidBody2D.linearVelocity.y <= 0)
            {
                //Falling
                //Physics2D.gravity.y is a negative number (-9.81)
                fallVelocity = pull * (currentMovementSettings.fallAcceleration - 1) * Time.deltaTime;
            }
            else
            {
                //Rising
                if (holdingJump)
                {
                    fallVelocity = pull * (currentMovementSettings.heldJumpDecay - 1) * Time.deltaTime;
                }
                else
                {
                    fallVelocity = pull * (currentMovementSettings.releasedJumpDecay - 1) * Time.deltaTime;
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

        // The fastest he can walk. A character that can only walk some of the way, or faster, says so here.
        protected virtual float MaxWalkSpeed
        {
            get { return Mathf.Max(0f, currentMovementSettings.maxMoveSpeed) * speedScale; }
        }

        // What he's standing on: whether it is anything solid on a flat enough surface under him, whatever layer it's on (standing),
        // and, if some of it is a kinematic body such as a platform, how fast that is moving (the result), which is its speed at the
        // place he's over.
        //
        // riding is whether he's on a kinematic body. measured is whether he has been for long enough, since the last step, to say
        // how fast it moves: the first step on it can't.
        private Vector2 FindSupport(out bool riding, out bool measured, out bool standing)
        {
            support.Update(rigidBody2D, transform);

            riding = support.riding;
            measured = support.measured;
            standing = support.standing;
            wallOnLeft = support.wallOnLeft;
            wallOnRight = support.wallOnRight;

            return support.velocity;
        }

        // How much of him is in a liquid, and which. Sampled up his body, so it changes smoothly as he goes in.
        private float SampleSubmersion(out Fluid fluid)
        {
            fluid = null;

            var count = rigidBody2D.GetAttachedColliders(bodyColliders);
            var bounds = new Bounds();
            var found = false;

            for (var i = 0; i < count; i++)
            {
                var body = bodyColliders[i];

                if (body.isTrigger || !body.enabled)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(body.bounds);
                }
                else
                {
                    bounds = body.bounds;
                    found = true;
                }
            }

            if (!found)
            {
                fluid = Map.FluidAt(transform.position);
                return fluid ? 1f : 0f;
            }

            var inLiquidCount = 0;

            for (var i = 0; i < SubmersionSamples; i++)
            {
                var y = Mathf.Lerp(bounds.min.y, bounds.max.y, (i + 0.5f) / SubmersionSamples);
                var here = Map.FluidAt(new Vector3(bounds.center.x, y, transform.position.z));

                if (here)
                {
                    inLiquidCount++;
                    fluid = here;
                }
            }

            return inLiquidCount / (float)SubmersionSamples;
        }

        // What the air or a liquid does to him, every physics step: holds him up by as much of him as is in it, against how dense
        // he is, and slows him by how thick it is. Something lighter than a liquid is pushed up until only as much of it is under
        // as it weighs, and settles there, bobbing, at the surface; something heavier sinks.
        private void ApplyBuoyancy()
        {
            var t = MovementTuning.Current;
            var settings = currentMovementSettings;

            submerged = SampleSubmersion(out var fluid);

            var fluidDensity = fluid ? fluid.density : t.airDensity;
            var around = fluidDensity * submerged + t.airDensity * (1f - submerged);
            var lift = Mathf.Min(around / Mathf.Max(0.0001f, settings.density), t.maxBuoyancyRatio);

            rigidBody2D.gravityScale = settings.gravity * (1f - lift);

            var thickness = fluid ? fluid.viscosity * submerged : 0f;
            rigidBody2D.linearDamping = settings.linearDampening + thickness * t.fluidDamping;
            rigidBody2D.angularDamping = settings.angularDampening;
        }

        // One physics step of walking: moveSpeed ramps toward where the player is steering, and his sideways speed follows it.
        //
        // Walking is added to whatever speed he already has rather than replacing it, so a shove, a moving platform or a slide
        // in the way he's going isn't cut back to walking pace. On the ground that other speed wears off, at the rate he can
        // stop himself; in the air it's kept. Speed against the way he's walking is still taken over by it, which is how he
        // pushes against things in his way.
        private void ApplyWalking()
        {
            var settings = currentMovementSettings;

            if (settings == null)
            {
                return;
            }

            // Something else has set moveSpeed since the last step, so what walking put in no longer means anything.
            if (moveSpeed != lastMoveSpeed)
            {
                walkApplied = 0f;
            }

            var before = moveSpeed;
            var scale = Time.fixedDeltaTime / WalkStep;
            var input = Time.frameCount - walkInputFrame <= 1 ? walkInput : 0f;

            if (input != 0f)
            {
                // Turning round has to stop him as well as get him going again.
                var rate = settings.moveSpeedIncrease;
                if (moveSpeed * input < 0f)
                {
                    rate += settings.moveSpeedDecay;
                }

                moveSpeed += input * rate * scale;
            }
            else
            {
                moveSpeed = Mathf.MoveTowards(moveSpeed, 0f, settings.moveSpeedDecay * scale);
            }

            var fastest = MaxWalkSpeed;
            moveSpeed = Mathf.Clamp(moveSpeed, -fastest, fastest);

            // Speed that isn't walking: beyond what was put in last step, in the same way. If moveSpeed was just set by something
            // else, it's what that said he's walking at that counts.
            var seen = rigidBody2D.linearVelocity.x;
            var reference = walkApplied != 0f ? walkApplied : before;
            var external = 0f;

            if (Mathf.Abs(seen) > Mathf.Abs(reference) && (reference == 0f || (seen > 0f) == (reference > 0f)))
            {
                external = seen - reference;

                if (Mathf.Abs(external) < ExternalSpeedMargin)
                {
                    external = 0f;
                }
            }

            // On the ground, speed that isn't walking wears off as friction would take it, as fast as he can stop himself, so a
            // nudge from landing, or a shove, doesn't stay with him and turn into a burst of speed when he next runs. Nothing
            // else takes it off: there may be no friction, and the air doesn't slow him. In the air it's kept, so a jump carries on.
            var onGround = distFromGround < 0.2f;
            var brake = onGround ? settings.moveSpeedDecay * scale : 0f;

            external = Mathf.MoveTowards(external, 0f, brake);

            if (moveSpeed == 0f)
            {
                if (before != 0f)
                {
                    // Walking has just run down to nothing: that part of his speed stops, and any other stays.
                    rigidBody2D.linearVelocityX = external;
                }
                else if (onGround && seen != 0f)
                {
                    // Not walking at all: whatever speed is left slows to nothing, as it would with friction.
                    rigidBody2D.linearVelocityX = Mathf.MoveTowards(seen, 0f, brake);
                }

                walkApplied = 0f;
                return;
            }

            var direction = moveSpeed > 0f ? Vector2.right : Vector2.left;

            // What he's carrying is held out in front of him, so it isn't a wall either.
            var carried = objectCarrying ? objectCarrying.GetComponent<Rigidbody2D>() : null;

            var distance = float.PositiveInfinity;
            foreach (var wallDetector in wallDetectors)
            {
                // Pushable things aren't walls: walking into them is how they get pushed.
                var dist = Map.DistanceFromDirection(wallDetector.transform.position, direction, 5, true, rigidBody2D, carried);
                if (dist != null && dist < distance)
                {
                    distance = (float)dist;
                }
            }

            // A wall ahead by the detectors, or one he's touching that they can't see.
            if (distance <= 0.75f || (moveSpeed > 0f ? wallOnRight : wallOnLeft))
            {
                moveSpeed = 0f;
                walkApplied = 0f;
                return;
            }

            rigidBody2D.linearVelocityX = moveSpeed + external;
            walkApplied = moveSpeed;
        }

        protected virtual void Update()
        {
            // Only shown: the picture is chosen from what the host sends.
            if (ShownOnly)
            {
                return;
            }

            RememberSafePosition();

            SetSprite();

            // Which liquid his middle is in, if any, and how he moves in it doing what he's doing.
            currentFluid = Map.FluidAt(transform.position);
            inLiquid = currentFluid != null;
            currentMovementSettings = GetMovementSetting(GetMovementMode(), currentFluid);

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

            // So that what is shown for a character that is only being shown, which goes by whether he's in the air, can tell.
            isJumping = airborne;

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
                        currentMovementSettings.jumpForce * jumpValue * jumpScale);

                    timeOfLastJump = Time.time;
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
            
            // Which way he's being steered. Only that is noted here: this is called a frame at a time, and the speeding up and
            // slowing down is worked out in the physics step, so it doesn't depend on how fast the frames come. A character
            // that can't move isn't steered either way.
            var xPress = canMove ? direction.x : 0f;

            walkInput = xPress < 0f ? -1f : xPress > 0f ? 1f : 0f;
            walkInputFrame = Time.frameCount;

            if (xPress < 0f)
            {
                facing = CharacterFacing.Left;
            }
            else if (xPress > 0f)
            {
                facing = CharacterFacing.Right;
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
                // It goes through walls while it's held, so it's only put down where there's room for it.
                if (!objectCarrying.CanPutDown())
                {
                    var text = GameText.Current.noRoomToPutDown;

                    if (!ConnectionPlayer.Broadcast(text, 2.5f))
                    {
                        GameMessages.Show(text, 2.5f);
                    }

                    return;
                }

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

        // How he moves doing something (a mode of his profile, or "base" for nothing in particular) in a liquid, or in the air if
        // there isn't one. It's worked out fresh every time, so changes to the profile or the tuning show straight away while
        // playing, and the result is the same object each time.
        public ResolvedMovement GetMovementSetting(string mode, Fluid fluid)
        {
            currentMovementSettingName = mode;
            movementProfile.Resolve(mode, fluid, MovementTuning.Current, resolvedMovement);
            return resolvedMovement;
        }

        // The mode of his profile he is moving in just now, such as "base" or "float".
        public string MovementModeName
        {
            get { return currentMovementSettingName; }
        }

        // What he is doing, as far as how he moves goes: the name of one of the modes in his profile. Characters that move
        // differently while they do something say so here; whether he's in a liquid is looked after without them.
        protected virtual string GetMovementMode()
        {
            return "base";
        }

        // Slows him down: 1 is not at all, and 0 stops him. Being frozen does this.
        public void SetMovementScale(float speed, float jump)
        {
            speedScale = Mathf.Clamp01(speed);
            jumpScale = Mathf.Clamp01(jump);
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
            ShownOnly = shownOnly;

            GetParking().SetShownOnly(shownOnly);
        }

        // Whether the character is only being shown: this machine has joined someone else's game, and the host does everything that the
        // character does. A character with parts that carry on working there has to leave the simulating to the host while this is set.
        protected bool ShownOnly { get; private set; }

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

        // What they are carrying now, or nothing. A button counts it as part of their weight.
        public CarryableObject CarriedObject
        {
            get { return objectCarrying; }
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