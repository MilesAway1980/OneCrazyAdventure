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

    [RequireComponent(typeof(Rigidbody2D), typeof(DamageableEntity))]
    public class Character : MonoBehaviour
    {
        [Tooltip("The picture shown while he's standing or walking.")]
        public Sprite walkSprite;
        [Tooltip("The picture shown while he's in the air.")]
        public Sprite jumpSprite;
        [Tooltip("The picture shown while he's using his ability on the ground.")]
        public Sprite actionSprite;
        [Tooltip("The picture for using something, such as a lever. Not used at the moment.")]
        public Sprite interactSprite;
        [Tooltip("The picture shown while he's using his ability in the air.")]
        public Sprite jumpUseSprite;
        
        [Tooltip("Whether he can climb up onto things he comes to, such as crates.")]
        public bool ableToClimbObjects = true;
        [Tooltip("Whether he can pick up and carry things, such as crates.")]
        public bool ableToCarryObjects = true;
        [Tooltip("Whether he can take hold of things, to pull or push them.")]
        public bool ableToGrabObjects = true;

        [Tooltip("The Kinematic body, such as an empty child object, that things he carries are attached to. Needed if he is able to carry things.")]
        public Rigidbody2D carryAttachmentPoint;

        [Min(0f)]
        [Tooltip("The heaviest thing he can pick up, as its mass. Something heavier doesn't move when he tries: he reaches for it, with his Interact " +
                 "animation, and that's all. 0 is no limit. It's the thing's own mass, before his Carry Strength takes any off it.")]
        public float maxCarryWeight;

        [Range(0f, 100f)]
        [Tooltip("How much of the weight of what he carries he doesn't feel, as a percentage. 0: he feels all of it, so carrying a crate as heavy as he is " +
                 "makes him twice as heavy. 50: half of its weight counts, so a crate of mass 200 counts as 100, and one of 70 as 35. 100: carrying " +
                 "doesn't weigh on him at all. What he feels is added to his own mass while he holds it, which counts on buttons and crumbling floors, and " +
                 "his walking speed and his jumping go down to his own mass over his mass with it.")]
        public float carryStrength;

        private bool holdingJump;
        //Eventually make protected.  public for testing
        [Tooltip("Whether he can jump at the moment. Worked out as the game runs: for watching, not for setting.")]
        public bool canJump;
        [Tooltip("Whether he's in a jump at the moment. Worked out as the game runs: for watching, not for setting.")]
        public bool isJumping;
        [Tooltip("Whether the jump he's in has already been started, so it isn't started again. For watching, not for setting.")]
        public bool alreadyJumping;
        [Tooltip("Whether he's falling at the moment. Worked out as the game runs: for watching, not for setting.")]
        public bool isFalling;
        private float maxFallSpeed;
        
        [Tooltip("Whether he's in a liquid at the moment. Worked out as the game runs: for watching, not for setting.")]
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

        // How much speed has to have been taken off a push in a step to count, so that rounding doesn't wear it away.
        private const float PushLossMargin = 0.02f;

        // Which way the player is steering (-1, 0 or 1), and the frame they last said so. The steering is read a frame at a time
        // but the walking is worked out a physics step at a time, so it's kept until the next frame says otherwise. If nothing
        // says so for a frame, nobody is steering him any more.
        // How far the player is steering him sideways, -1 (all the way left) to 1 (all the way right). The Move binding does the stick's dead zone;
        // this is only a little more, for the sticks that don't.
        private const float WalkDeadZone = 0.05f;
        private float walkInput;
        private int walkInputFrame = -10;

        // What the walking put into his speed in the last step, and what moveSpeed was left at, to tell what's since been
        // added by something else.
        private float walkApplied;
        private float lastMoveSpeed;

        // Sideways speed from being pushed by a force (a fan, a blast), which is kept as momentum rather than being overridden by
        // walking: walking is added to it. pushDelta is what pushes have added since the last step, and lastSetSpeed is the sideways
        // speed he was left with at the end of the last step, to tell what has been taken off a push since (by a wall, say).
        private float pushSpeed;
        private float pushDelta;
        private float lastSetSpeed;

        // Until when he has been brushing against something at his side, such as a wall, or the corner of a tile. A brush can throw him
        // sideways, and speed that comes from it isn't his to keep.
        private float brushedUntil;
        private float externalLastStep;
        private bool droppedLastStep;
        private const float BrushSeconds = 0.1f;

        [Tooltip("For finding a random speed boost: writes to the Console each time he picks up speed that isn't his walking or a push, saying whether it came from brushing something (and was dropped) or from nowhere that could be seen (and was kept).")]
        public bool logSpeedGains;

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
        [Tooltip("The dust kicked up from his feet as he walks. It's found by itself, from a child tagged FootDust, so it can be left empty.")]
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

        // How much of him is in a liquid, from 0 to 1, which gives him buoyancy and drag.
        private float submerged;
        private bool floatingAtSurface;

        // A kick in a liquid is a clean arc up to its top: until hopUntil, while he's rising, the liquid doesn't slow him, and his
        // weight is kickWeight, whether he's in the liquid or out of it, so he goes as high as the kick is worked out to.
        private float hopUntil;
        private float kickWeight = 1f;

        // What a kick's speed is multiplied by in the liquid he's in. The kick is worked out for the weight he has in the air, and
        // something denser than the liquid is held up by it, so it would rise higher than the number says; this brings it back. A
        // thicker liquid slows it further, which makes it floaty: the weight goes with the square of this, so the height stays.
        private float kickScale = 1f;

        // The liquid any of him is in, which is also what the kick is for when his middle is out of it but his feet are in.
        private Fluid sampledFluid;
        private readonly ResolvedMovement kickMovement = new ResolvedMovement();
        private static readonly Collider2D[] bodyColliders = new Collider2D[8];

        // Slows him, as being frozen does: 1 is not at all, and 0 stops him.
        private float speedScale = 1f;
        private float jumpScale = 1f;
        protected SpriteRenderer spriteRenderer;

        // His animations, if he has any: see CharacterAnimator.
        private CharacterAnimator characterAnimator;
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

        // Something that's added to the scene as it plays (the grab point on the Balloon's string) and so wasn't there when the characters looked for what
        // they can use with Interact. Told to them here.
        public static void RegisterInteractable(InteractableObject thing)
        {
            if (interactableObjects == null)
            {
                interactableObjects = FindObjectsByType<InteractableObject>(FindObjectsInactive.Exclude).ToList();
            }

            if (thing && !interactableObjects.Contains(thing))
            {
                interactableObjects.Add(thing);
            }
        }
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

        // Whether what he carries is held by a body that moves with him, as a Kinematic one inside him does, which is how most things are carried:
        // the Carry Attachment Point is made Kinematic, here and in the editor. A character whose carry point is meant to hang free instead (the end of
        // the Balloon's string, which things dangle from and which swings) says no, and it's left as it is, a body of its own.
        protected virtual bool CarryPointMovesWithHim
        {
            get { return true; }
        }

#if UNITY_EDITOR
        // In the editor, when something is changed in the Inspector: a Carry Attachment Point that isn't Kinematic is made so, and
        // said so, straight away. (When playing, Start does it.) It's done a moment later, as changing other objects while this is
        // called isn't allowed to be relied on.
        protected virtual void OnValidate()
        {
            if (Application.isPlaying || !carryAttachmentPoint || carryAttachmentPoint.bodyType == RigidbodyType2D.Kinematic || !CarryPointMovesWithHim)
            {
                return;
            }

            var character = this;
            var point = carryAttachmentPoint;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (!character || !point || point.bodyType == RigidbodyType2D.Kinematic || !character.CarryPointMovesWithHim)
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

        // How soft he is, from 0 (his picture never changes size) to 1 (as soft as a balloon): his Movement Profile's Softness, or, left at -1 there,
        // what his class says. See ShapeFlex.
        public float Softness
        {
            get
            {
                var set = movementProfile ? movementProfile.softness : -1f;
                return set >= 0f ? Mathf.Clamp01(set) : DefaultSoftness;
            }
        }

        // How soft a character of this class is, if his profile doesn't say. Most are hard.
        protected virtual float DefaultSoftness
        {
            get { return 0f; }
        }

        protected virtual void Start()
        {
            // Every character squashes and stretches as far as he's soft, which is not at all for most.
            if (!GetComponent<ShapeFlex>())
            {
                gameObject.AddComponent<ShapeFlex>();
            }

            // And is drawn in a place of his own among the characters, so they don't flicker over each other when they stand together.
            CharacterSorting.EnsureOn(this);

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            characterAnimator = GetComponent<CharacterAnimator>();
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

            // His own mass, which the weight of what he carries is added to and taken off.
            ownMass = rigidBody2D.mass;
            massWithLoad = ownMass;

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
            // A carry point that's meant to hang free (the end of the Balloon's string) is a Dynamic body, whatever the scene was saved with.
            if (carryAttachmentPoint && !CarryPointMovesWithHim && carryAttachmentPoint.bodyType == RigidbodyType2D.Kinematic)
            {
                carryAttachmentPoint.bodyType = RigidbodyType2D.Dynamic;
            }

            if (carryAttachmentPoint && carryAttachmentPoint.bodyType != RigidbodyType2D.Kinematic && CarryPointMovesWithHim)
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

            UpdateLoad();

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
                pushSpeed = 0f;
                pushDelta = 0f;
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

            // He can jump off something solid: the floor, or something standing on it. A liquid isn't, so in one he can't jump
            // unless his feet are on the bottom, and then it's a kick through the liquid. The exception is floating at the surface,
            // where he can hop out of it. On something that's going up he isn't rising unless he's rising faster than it is.
            canJump =
                ((distFromGround < 0.2f && rigidBody2D.linearVelocity.y - ride.y < 0.01f) || floatingAtSurface) &&
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
            // They're on how much he weighs, so a balloon, which is held up by the air, isn't pulled down by a fall or a jump let go of as if it
            // weighed all it does: with none of its weight it has none of the pull, and with some of it, it has that much of it.
            //
            // Held up by a rope that's pulled tight (the end of the Balloon's string) he has none of them either: it carries him, and a pull that made him fall
            // harder would only be a tug of war with it. Hanging with the rope slack, he falls as he does anywhere.
            var hanging = grabPoint && grabPoint.IsGrabbedBy(this) && grabPoint.ropeTaut;
            var pull = submerged > 0f || Kicking || hanging ? 0f : Physics2D.gravity.y * currentMovementSettings.gravity * Mathf.Clamp01(bodyWeight);

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
            var fastestFall = TerminalFallSpeed(currentMovementSettings.maxFallSpeed);

            if (rigidBody2D.linearVelocity.y < -fastestFall)
            {
                rigidBody2D.linearVelocity =
                    new Vector2(rigidBody2D.linearVelocity.x, -fastestFall);
            }

            // Lighter than what's round him, he rises, and no faster than the fastest he could fall.
            var fastestRise = TerminalRiseSpeed(currentMovementSettings.maxFallSpeed);

            if (bodyWeight < 0f && !Kicking && rigidBody2D.linearVelocity.y > fastestRise)
            {
                rigidBody2D.linearVelocity =
                    new Vector2(rigidBody2D.linearVelocity.x, fastestRise);
            }

            ShapeVerticalSpeed();

            // Last, with the speed he'll be going at for this step settled. What he's riding going up isn't him going up.
            ForgiveCeilingCorner(ride.y);
        }

        // ----- Corner forgiveness -----
        //
        // Jumping up so that just the edge of his head would clip the corner of a ceiling, he'd be caught or thrown off it: the physics turns
        // some of his upward speed into sideways speed, and the more he's cut off the less he goes up, so the shape of his head only changes
        // which of the two he gets. So, before the step, if what's above him would be hit, and he'd miss it by moving over a little, he's moved
        // over, and goes up at the speed he had. Anything more than that is a real ceiling and he bumps it.

        [Min(0f)]
        [Tooltip("How far, in world units, he's moved sideways to slip past the corner of a ceiling that his head would just clip going up, instead of being caught on it or thrown off it. About a quarter of his width is a good start. 0 turns it off, and anything he'd clip by more than this is a real bump.")]
        public float cornerForgiveness = 0.15f;

        // How many distances up to Corner Forgiveness are tried, nearest first.
        private const int CornerSteps = 6;

        // How far into something he has to be to count as being in it, so that resting against it doesn't (the ground he stands on is
        // always a little in him). The same as the other places that ask.
        private const float CornerSkin = 0.02f;

        // A little more than the step takes him is looked at, as gravity changes how far he goes as the step is made.
        private const float CornerLookAhead = 0.01f;

        // Rising slower than this, in units a second, there's nothing to forgive: a jump is many times this. Standing on something that
        // shifts a little, or going up with it, shouldn't be taken for a jump.
        private const float CornerMinRiseSpeed = 1f;

        private static readonly RaycastHit2D[] ceilingHits = new RaycastHit2D[8];

        // supportSpeed is how fast what he's standing on is going up, which is the platform's and not his own.
        private void ForgiveCeilingCorner(float supportSpeed)
        {
            // In someone else's game the host moves him.
            if (cornerForgiveness <= 0f || GameSession.IsClientOnly)
            {
                return;
            }

            var ownSpeed = rigidBody2D.linearVelocity.y - supportSpeed;

            if (ownSpeed < CornerMinRiseSpeed)
            {
                return;
            }

            var rise = rigidBody2D.linearVelocity.y * Time.fixedDeltaTime;

            // Only if something is above him in the way, found without moving him: putting him at places to ask is what the rest of this does,
            // and it isn't to be done to everyone who is going up for any reason.
            if (!CeilingAbove(rise + CornerLookAhead))
            {
                return;
            }

            var start = rigidBody2D.position;

            var up = new Vector2(0f, rise + CornerLookAhead);
            var moved = false;

            // Nothing is forgiven if he's already in something, or if nothing is above him.
            if (!InSolid(start) && InSolid(start + up))
            {
                // Over the way he's steering, or going, first, if both ways are clear.
                var preferred = moveSpeed != 0f ? Mathf.Sign(moveSpeed) : (rigidBody2D.linearVelocity.x < 0f ? -1f : 1f);
                var step = cornerForgiveness / CornerSteps;

                for (var i = 1; i <= CornerSteps && !moved; i++)
                {
                    for (var side = 0; side < 2 && !moved; side++)
                    {
                        var by = new Vector2((side == 0 ? preferred : -preferred) * step * i, 0f);

                        // Clear where he'd be, and clear where he'd go from there.
                        if (!InSolid(start + by) && !InSolid(start + by + up))
                        {
                            SetPosition(start + by);

                            // What he's carrying goes with him, a little at a time like he does, not sent across at once.
                            MoveCarried(by, false);
                            moved = true;
                        }
                    }
                }
            }

            if (!moved)
            {
                rigidBody2D.position = start;
            }
        }

        // Whether anything that would stop him is above, within a distance, found by looking up from where he is, so he isn't moved. A
        // one-way platform doesn't stop him going up.
        private bool CeilingAbove(float distance)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));

            var count = GetSolidColliders();

            for (var i = 0; i < count; i++)
            {
                var hits = solidColliders[i].Cast(Vector2.up, filter, ceilingHits, distance);

                for (var j = 0; j < hits; j++)
                {
                    var hit = ceilingHits[j];

                    if (hit.collider.attachedRigidbody == rigidBody2D || hit.normal.y > -0.05f)
                    {
                        continue;
                    }

                    if (hit.collider.usedByEffector)
                    {
                        var effector = hit.collider.GetComponent<PlatformEffector2D>();

                        if (effector && effector.useOneWay)
                        {
                            continue;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        // Whether he would be in something solid if he were at a place, other than the ground he's standing on and the like: he's put there
        // to ask, and the caller puts him back. One-way platforms don't count, as he goes up through them.
        private bool InSolid(Vector2 place)
        {
            rigidBody2D.position = place;

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));

            var count = GetSolidColliders();

            for (var i = 0; i < count; i++)
            {
                var collider = solidColliders[i];
                var overlapping = collider.Overlap(filter, groundOverlaps);

                for (var j = 0; j < overlapping; j++)
                {
                    var other = groundOverlaps[j];

                    if (other.attachedRigidbody == rigidBody2D)
                    {
                        continue;
                    }

                    if (other.usedByEffector)
                    {
                        var effector = other.GetComponent<PlatformEffector2D>();

                        if (effector && effector.useOneWay)
                        {
                            continue;
                        }
                    }

                    var distance = collider.Distance(other);

                    if (distance.isOverlapped && distance.distance < -CornerSkin)
                    {
                        return true;
                    }
                }
            }

            return false;
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
        private float SampleSubmersion(out Fluid fluid, out bool atSurface)
        {
            return Buoyancy.Sample(rigidBody2D, bodyColliders, out fluid, out atSurface);
        }

        // What the air or a liquid does to him, every physics step: holds him up by as much of him as is in it, against how dense
        // he is, and slows him by how thick it is. Something lighter than a liquid is pushed up until only as much of it is under
        // as it weighs, and settles there, bobbing, at the surface; something heavier sinks.
        private void ApplyBuoyancy()
        {
            var t = MovementTuning.Current;
            var settings = currentMovementSettings;

            submerged = SampleSubmersion(out var fluid, out var atSurface);

            // Floating at the surface: lighter than the liquid, so it holds him up, with the liquid ending about at the top of him.
            floatingAtSurface = atSurface && fluid != null && settings.density < fluid.density;

            var fluidDensity = fluid != null ? fluid.density : t.airDensity;
            var around = fluidDensity * submerged + t.airDensity * (1f - submerged);
            var lift = Mathf.Min(around / Mathf.Max(0.0001f, settings.density), t.maxBuoyancyRatio);

            var weight = ShapeBuoyancyWeight(Buoyancy.Weight(lift, fluidDensity, settings.density, t));
            bodyWeight = weight;
            rigidBody2D.gravityScale = settings.gravity * weight;


            // Only something denser than the liquid is brought back: a floater leaves it, and gets its weight back as it does.
            kickScale = fluid != null && settings.density > fluid.density ? Mathf.Sqrt(Mathf.Clamp(weight, 0.05f, 1f)) : 1f;

            // A thicker liquid makes the kick slower, and floatier, without making it any lower.
            if (fluid != null)
            {
                kickScale *= Buoyancy.KickPace(fluid, t);
            }
            sampledFluid = fluid;

            if (Kicking)
            {
                // Letting go of the jump button early cuts the kick short, as it does a jump in the air: gravity pulls Jump Cut
                // times as hard going up, so a tap is a short kick and holding is a full one.
                rigidBody2D.gravityScale = settings.gravity * kickWeight * (holdingJump ? 1f : t.jumpCut);
            }

            // The liquid slows him by how thick it is, and by how fast he's going down or up through it, so a dive is checked from the
            // surface instead of carrying on at full speed.
            var drag = fluid != null ? Buoyancy.Damping(fluid, submerged, Mathf.Abs(rigidBody2D.linearVelocity.y), t) : 0f;

            // A kick in a liquid isn't held back by it on the way up, or it would never get as high as it's worked out to, and
            // nothing could get out.
            if (Kicking)
            {
                drag = 0f;
            }

            rigidBody2D.linearDamping = settings.linearDampening + drag;
            rigidBody2D.angularDamping = settings.angularDampening;
        }

        // How much of his weight he has in what's round him, given what the buoyancy works out: 1 all of it, 0 none, below 0 he rises. A character whose lift is
        // taken up by something (a load on the Balloon's string) says so here.
        protected virtual float ShapeBuoyancyWeight(float weight)
        {
            return weight;
        }

        // The fastest he can fall, and the fastest he can rise when he's lighter than what's round him, in units a second, given what his movement says.
        // A character whose speed is limited by something else, such as a load, says so. And ShapeVerticalSpeed can push his speed to where it should be
        // after the movement is worked out.
        protected virtual float TerminalFallSpeed(float fromMovement)
        {
            return fromMovement;
        }

        protected virtual float TerminalRiseSpeed(float fromMovement)
        {
            return fromMovement;
        }

        protected virtual void ShapeVerticalSpeed()
        {
        }

        // How much of his weight he has in what's round him, which his gravity is multiplied by: 1 is all of it, 0 is none (he hangs there), and
        // below 0 he's lighter than what's round him and rises. The extra pulls on a jump and a fall go by it too.
        private float bodyWeight = 1f;

        // Whether a kick that was made in a liquid is still on its way up.
        private bool Kicking
        {
            get { return Time.time < hopUntil && rigidBody2D.linearVelocity.y > 0f; }
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
            var fastest = MaxWalkSpeed;

            if (input != 0f)
            {
                // How far the stick is pushed is how fast he's heading for, so a gentle push is a slow walk. A key or a button is all the way.
                var target = input * fastest;

                // Turning round has to stop him as well as get him going again, and easing off to a slower walk is slowing down.
                var rate = settings.moveSpeedIncrease;
                if (moveSpeed * input < 0f)
                {
                    rate += settings.moveSpeedDecay;
                }
                else if (Mathf.Abs(moveSpeed) > Mathf.Abs(target))
                {
                    rate = settings.moveSpeedDecay;
                }

                moveSpeed = Mathf.MoveTowards(moveSpeed, target, rate * scale);
            }
            else
            {
                moveSpeed = Mathf.MoveTowards(moveSpeed, 0f, settings.moveSpeedDecay * scale);
            }

            moveSpeed = Mathf.Clamp(moveSpeed, -fastest, fastest);

            // Speed that isn't walking: beyond what was put in last step, in the same way. If moveSpeed was just set by something
            // else, it's what that said he's walking at that counts.
            var seen = rigidBody2D.linearVelocity.x;

            // Something in the way (a wall, a crate that won't move) takes speed off a push, and a push is only what's left of it.
            // The liquid's drag does too.
            var shortfall = seen - lastSetSpeed;

            if (pushSpeed != 0f && Mathf.Abs(shortfall) > PushLossMargin && (shortfall > 0f) != (pushSpeed > 0f))
            {
                pushSpeed = pushSpeed > 0f ? Mathf.Max(0f, pushSpeed + shortfall) : Mathf.Min(0f, pushSpeed + shortfall);
            }

            // The push is part of the speed he already has, which walking and the other speed are added to.
            var pushSeen = pushSpeed;
            var reference = (walkApplied != 0f ? walkApplied : before) + pushSeen;
            var external = 0f;

            if (Mathf.Abs(seen) > Mathf.Abs(reference) && (reference == 0f || (seen > 0f) == (reference > 0f)))
            {
                external = seen - reference;

                if (Mathf.Abs(external) < ExternalSpeedMargin)
                {
                    external = 0f;
                }
            }

            // Speed picked up while brushing against something at his side isn't kept: it's the physics throwing him off a corner or
            // a join in the ground, not a shove, and kept in the air it would turn into a random burst of speed on landing.
            //
            // "Dropped" is how much of what he's going at sideways is that, which has to be taken off whichever way his speed is set below:
            // walking sets it from the parts that are his (so it goes by itself), but standing still it would otherwise just keep what he
            // has, bump and all.
            var dropped = 0f;

            if (external != 0f)
            {
                if (Time.time < brushedUntil)
                {
                    if (logSpeedGains && !droppedLastStep)
                    {
                        Debug.Log($"{name}: picked up {external:0.00} of sideways speed while brushing something, and dropped it " +
                                  $"(seen {seen:0.00}, walking {reference:0.00}, up/down {rigidBody2D.linearVelocity.y:0.00}).", this);
                    }

                    dropped = external;
                    external = 0f;
                }
                else if (logSpeedGains && externalLastStep == 0f)
                {
                    Debug.Log($"{name}: picked up {external:0.00} of sideways speed with nothing brushing him, and kept it " +
                              $"(seen {seen:0.00}, walking {reference:0.00}, on the ground {distFromGround < 0.2f}, up/down {rigidBody2D.linearVelocity.y:0.00}).", this);
                }
            }

            // On the ground, speed that isn't walking wears off as friction would take it, at the same rate for everyone (Ground
            // Friction in the tuning), whatever he weighs or how well he can stop himself. So a nudge from landing, or a shove, doesn't
            // stay with him and turn into a burst of speed when he next runs, and a force such as a fan's, which moves something
            // light further than something heavy, isn't cancelled by how well he happens to stop. Nothing else takes it off: there
            // may be no friction material, and the air doesn't slow him. In the air it's kept, so a jump carries on.
            var onGround = distFromGround < 0.2f;
            var brake = onGround ? MovementTuning.Current.groundFriction * Time.fixedDeltaTime : 0f;

            external = Mathf.MoveTowards(external, 0f, brake);
            externalLastStep = external;
            droppedLastStep = dropped != 0f;

            // What pushed him since last step, and the same friction on the ground. A push that is no stronger than the friction
            // doesn't move him at all. It's kept in the air, and so is momentum, so it has to be beaten before he can turn round.
            pushSpeed += pushDelta;
            pushDelta = 0f;
            pushSpeed = Mathf.MoveTowards(pushSpeed, 0f, brake);

            if (moveSpeed == 0f)
            {
                if (before != 0f)
                {
                    // Walking has just run down to nothing: that part of his speed stops, and any other stays.
                    rigidBody2D.linearVelocityX = external + pushSpeed;
                }
                else if (onGround)
                {
                    // Not walking at all: whatever speed is left slows to nothing, as it would with friction.
                    rigidBody2D.linearVelocityX = Mathf.MoveTowards(seen - pushSeen - dropped, 0f, brake) + pushSpeed;
                }
                else
                {
                    // In the air he keeps what he has, less any bump from brushing a corner on the way, which isn't his to keep.
                    rigidBody2D.linearVelocityX = seen - dropped + (pushSpeed - pushSeen);
                }

                walkApplied = 0f;
                lastSetSpeed = rigidBody2D.linearVelocityX;
                return;
            }

            var direction = moveSpeed > 0f ? Vector2.right : Vector2.left;

            // What he's carrying is held out in front of him, so it isn't a wall either.
            var carried = objectCarrying ? objectCarrying.GetComponent<Rigidbody2D>() : null;

            var nearWall = false;
            foreach (var wallDetector in wallDetectors)
            {
                // Pushable things aren't walls: walking into them is how they get pushed.
                var detectorPosition = wallDetector.transform.position;
                var dist = Map.DistanceFromDirection(detectorPosition, direction, 5, true, rigidBody2D, carried);

                if (dist != null)
                {
                    // A wall is that near, or, for a character wider than that, right at his edge: the detector sits in the middle of him, so
                    // for one whose edge is further out than WallDistance it would otherwise never see a wall before he's in it.
                    var edge = SideReach(moveSpeed > 0f, detectorPosition.x);

                    if (dist <= Mathf.Max(WallDistance, edge + WallSkin))
                    {
                        nearWall = true;
                    }
                }
            }

            // A wall ahead by the detectors, or one he's touching that they can't see. His walking stops, and so does the speed it
            // had given him: if that were left, it would turn into other speed, kept through the air, and when he was clear of the
            // wall and walking again it would be added to his walking, and he'd be going twice as fast. Speed that wasn't his
            // walking stays.
            if (nearWall || (moveSpeed > 0f ? wallOnRight : wallOnLeft))
            {
                moveSpeed = 0f;
                walkApplied = 0f;
                rigidBody2D.linearVelocityX = external + pushSpeed;
                lastSetSpeed = rigidBody2D.linearVelocityX;
                return;
            }

            rigidBody2D.linearVelocityX = moveSpeed + external + pushSpeed;
            walkApplied = moveSpeed;
            lastSetSpeed = rigidBody2D.linearVelocityX;
        }

        // How near a wall has to be, from a wall detector, for walking toward it to stop, and how far past his own edge that is at least.
        private const float WallDistance = 0.75f;
        private const float WallSkin = 0.05f;

        private static readonly Collider2D[] ownColliders = new Collider2D[8];

        // How near his body has to be to an area, in units, to count as being in it: touching, the way two colliders do.
        private const float AreaTouchMargin = 0.02f;

        // Whether his body is in an area such as a lever's trigger, or touching it, and not just his middle: a wide character can be right up
        // against something with his middle well outside it.
        public bool IsInArea(Collider2D area)
        {
            if (!area)
            {
                return false;
            }

            var count = rigidBody2D.GetAttachedColliders(ownColliders);

            for (var i = 0; i < count; i++)
            {
                if (ownColliders[i].isTrigger)
                {
                    continue;
                }

                var gap = area.Distance(ownColliders[i]);

                if (gap.isOverlapped || gap.distance <= AreaTouchMargin)
                {
                    return true;
                }
            }

            // Nothing solid on him to go by: his middle.
            return count == 0 && area.bounds.Contains(transform.position);
        }

        // How far a point is from his body, in units: from the nearest part of him, and 0 if it's in him. His middle if he has nothing solid to go by.
        public float DistanceFromBody(Vector2 point)
        {
            var count = rigidBody2D.GetAttachedColliders(ownColliders);
            var nearest = float.PositiveInfinity;

            for (var i = 0; i < count; i++)
            {
                if (ownColliders[i].isTrigger)
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, Vector2.Distance(ownColliders[i].ClosestPoint(point), point));
            }

            return float.IsPositiveInfinity(nearest) ? Vector2.Distance(transform.position, point) : nearest;
        }

        // How far his solid body reaches to the right or the left of a place, in units: the distance from a wall detector to his edge.
        private float SideReach(bool toRight, float fromX)
        {
            var count = rigidBody2D.GetAttachedColliders(ownColliders);
            var reach = 0f;

            for (var i = 0; i < count; i++)
            {
                if (ownColliders[i].isTrigger)
                {
                    continue;
                }

                var bounds = ownColliders[i].bounds;
                reach = Mathf.Max(reach, toRight ? bounds.max.x - fromX : fromX - bounds.min.x);
            }

            return reach;
        }

        // Notes that he is touching something at his side, not under him, so what that does to his speed isn't mistaken for a shove.
        void OnCollisionEnter2D(Collision2D collision)
        {
            NoteBrush(collision);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            NoteBrush(collision);
        }

        private void NoteBrush(Collision2D collision)
        {
            for (var i = 0; i < collision.contactCount; i++)
            {
                if (Mathf.Abs(collision.GetContact(i).normal.y) < SupportTracker.FlatEnough)
                {
                    brushedUntil = Time.time + BrushSeconds;
                    return;
                }
            }
        }

        // Pushes him with a force (a fan's, which is applied every physics step) or an impulse (a blast). Up and down is left to the
        // physics, as gravity and the liquids act on it. Sideways, it changes his push speed, so that it's kept as momentum and he
        // has to beat it to walk against it: walking into a strong fan gets harder the nearer he gets, until he can't, and a push to
        // the left has to be overcome before he can go right. It's the same push, a force over his mass, whatever he's walking.
        // Something that isn't a character is just pushed.
        public static void Push(Rigidbody2D body, Vector2 amount, ForceMode2D mode)
        {
            var character = body ? body.GetComponent<Character>() : null;

            if (character)
            {
                character.ApplyPush(amount, mode);
            }
            else if (body)
            {
                body.AddForce(amount, mode);
            }
        }

        private void ApplyPush(Vector2 amount, ForceMode2D mode)
        {
            // In someone else's game, the host moves everything, and it's shown here by where it's sent.
            if (GameSession.IsClientOnly)
            {
                return;
            }

            var mass = Mathf.Max(0.0001f, rigidBody2D.mass);

            pushDelta += mode == ForceMode2D.Impulse ? amount.x / mass : amount.x / mass * Time.fixedDeltaTime;
            rigidBody2D.AddForce(new Vector2(0f, amount.y), mode);
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
                if (canJump && distFromGround < 0.2f)
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

            var before = rigidBody2D.position;

            SetPosition(position);
            lastSafePosition = position;

            rigidBody2D.linearVelocity = Vector2.zero;
            rigidBody2D.angularVelocity = 0f;
            moveSpeed = 0f;
            pushSpeed = 0f;
            pushDelta = 0f;

            MoveCarried(position - before);

            // On every other machine too, and straight there, rather than sliding across from where they were.
            var sync = GetComponent<Mirror.NetworkTransformBase>();
            if (sync && sync.isServer)
            {
                sync.ServerTeleport(transform.position, transform.rotation);
            }
        }

        // Whether he is on his way from one place to another, such as through a teleporter that takes time. He can't be seen, can't be touched,
        // doesn't do anything and can't be steered, but he's still in the game, and still the character of whoever controls him, and
        // the cameras follow him as he goes.
        public bool InTransit { get; private set; }

        public void SetInTransit(bool inTransit)
        {
            InTransit = inTransit;

            GetParking().SetTransit(inTransit);
        }

        // Moves him on his way, at the place he is now, without anything else being done: for a teleporter taking him across. What he is
        // carrying goes with him.
        public void MoveInTransit(Vector2 position)
        {
            var by = position - (Vector2)transform.position;

            transform.position = new Vector3(position.x, position.y, transform.position.z);

            if (rigidBody2D)
            {
                rigidBody2D.position = position;
            }

            MoveCarried(by, false);
        }

        // What he's carrying goes with him, the same distance, or the joint that holds it would drag it across the world after him. It's
        // sent to the other machines at once, rather than slid across, unless he's on his way somewhere and it's moved a bit at a time.
        private void MoveCarried(Vector2 by, bool sendAtOnce = true)
        {
            if (!objectCarrying)
            {
                return;
            }

            var held = objectCarrying.GetComponent<Rigidbody2D>();

            if (!held)
            {
                return;
            }

            held.position += by;
            held.transform.position += new Vector3(by.x, by.y, 0f);
            held.linearVelocity = Vector2.zero;
            held.angularVelocity = 0f;

            var sync = held.GetComponent<Mirror.NetworkTransformBase>();
            if (sendAtOnce && sync && sync.isServer)
            {
                sync.ServerTeleport(held.transform.position, held.transform.rotation);
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

            var performing = IsPerformingAction();

            ChooseAnimation(isJumping, performing);
            ShowSprite(facing == CharacterFacing.Left, isJumping, performing, AnimationClip, AnimationSpeed);
        }

        // ----- Animation -----
        //
        // For each character the game simulates, it works out what he is doing (Idle, Walk, Rise, Fall, Action, AirAction, or something
        // one of his own scripts says, such as Roll) and which of his clips shows that. The number of the clip, and how fast he is walking,
        // are what's sent to the other machines. See Docs/Animation.md.

        // Walking slower than this is standing still.
        private const float WalkAnimationMinSpeed = 0.1f;

        // The clip to show, as its number in his Animation Set, or None if he has none for what he's doing.
        public int AnimationClip { get; private set; } = CharacterAnimationSet.None;

        // Whether a clip drew him the last time he was shown, as opposed to his ordinary pictures. A character's own script that sets
        // his picture by hand (the Torch's unlit ones) leaves it alone while this is true.
        protected bool ShowingAnimation { get; private set; }

        // Whether he's walking, as far as his animation goes: he's moving faster than a crawl.
        protected bool WalkingForAnimation
        {
            get { return Mathf.Abs(moveSpeed) > WalkAnimationMinSpeed; }
        }

        // Whether his set has a clip for this state, for how he looks right now (the variant).
        protected bool HasAnimationClip(string state)
        {
            return characterAnimator && characterAnimator.Find(state, AnimationVariant()) != CharacterAnimationSet.None;
        }

        // How fast he's walking, as a fraction of the fastest he does, for a clip that follows his speed.
        public float AnimationSpeed { get; private set; } = 1f;

        // What he is doing, by name. A character's own script is given the ordinary one and gives back the one it wants: a bowling ball
        // that is rolling says Roll, and dynamite that has gone off says Explode.
        protected virtual string AnimationState(string commonState)
        {
            return commonState;
        }

        // How he looks while he does it, when that's different, such as Snuffed. Empty for the ordinary. While he's carrying something it's
        // Carrying, unless a character says otherwise.
        protected virtual string AnimationVariant()
        {
            return objectCarrying ? CharacterAnimationSet.Carrying : string.Empty;
        }

        // When his Interact clip stops showing. Pulling a lever, picking something up and putting it down start it.
        private float interactAnimationUntil;

        // Whether he's working something that has to be held, such as a lever. His Interact clip shows for as long as he is.
        private bool operating;

        public void SetOperating(bool value)
        {
            operating = value;
        }

        // Starts his Interact clip, if he has one, for as long as it takes to play through.
        protected void StartInteractAnimation()
        {
            if (!characterAnimator)
            {
                return;
            }

            var clip = characterAnimator.Find(CharacterAnimationSet.Interact, AnimationVariant());
            interactAnimationUntil = clip == CharacterAnimationSet.None ? 0f : Time.time + characterAnimator.ClipSeconds(clip);
        }

        private void ChooseAnimation(bool airborne, bool performing)
        {
            if (!characterAnimator)
            {
                AnimationClip = CharacterAnimationSet.None;
                return;
            }

            // Using his action, when he has a clip for it. A character with no action of his own (Zipper, Die, Paperclip, Pencil) has none, and
            // goes on being whatever he would have been, instead of showing nothing until the button is let go.
            if (performing)
            {
                AnimationClip = characterAnimator.Find(AnimationState(airborne ? CharacterAnimationSet.AirAction : CharacterAnimationSet.Action),
                                                       AnimationVariant());
            }
            else
            {
                AnimationClip = CharacterAnimationSet.None;
            }

            if (AnimationClip == CharacterAnimationSet.None)
            {
                AnimationClip = characterAnimator.Find(AnimationState(OrdinaryAnimationState(airborne)), AnimationVariant());
            }

            var fastest = MaxWalkSpeed;
            AnimationSpeed = fastest > 0.01f ? Mathf.Clamp(Mathf.Abs(moveSpeed) / fastest, 0.3f, 1.5f) : 1f;
        }

        // What he is doing when it isn't his action: Rise or Fall in the air, otherwise Interact, Walk or Idle.
        private string OrdinaryAnimationState(bool airborne)
        {
            string common;

            if (airborne)
            {
                common = rigidBody2D.linearVelocity.y > 0f ? CharacterAnimationSet.Rise : CharacterAnimationSet.Fall;
            }
            else if (operating || Time.time < interactAnimationUntil)
            {
                common = CharacterAnimationSet.Interact;
            }
            else if (WalkingForAnimation)
            {
                common = CharacterAnimationSet.Walk;
            }
            else
            {
                common = CharacterAnimationSet.Idle;
            }

            return common;
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

        // Shows the character's picture for those three things, or his clip if he has one for what he's doing (and how fast he's walking,
        // for a clip that follows it). A character the game is simulating works them out for itself every frame. One that is only being
        // shown, on a machine that joined someone else's game, is given them.
        public void ShowSprite(bool facingLeft, bool airborne, bool performing, int clip, float walkSpeed)
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

            // His own clip, if he has one for this. It draws him, and the pictures below are for when it doesn't.
            ShowingAnimation = characterAnimator && characterAnimator.Show(clip, walkSpeed);

            if (ShowingAnimation)
            {
                return;
            }

            // A character with no picture for his action shows his ordinary one, not nothing.
            if (airborne)
            {
                spriteRenderer.sprite = performing && jumpUseSprite ? jumpUseSprite : jumpSprite;
            }
            else if (performing && actionSprite)
            {
                spriteRenderer.sprite = actionSprite;
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

                    // With any of him in a liquid, off the bottom or hopping from the surface, it's a kick: worked out for the
                    // weight he has in the liquid, and then a clean arc to its top, with no drag and the same weight all the way,
                    // even once he's out of the liquid. If his middle is out but his feet are in, it's the kick he would have in
                    // the liquid, unless the jump in the air is better.
                    var force = currentMovementSettings.jumpForce;
                    var apex = currentMovementSettings.apexTime;
                    var kick = 1f;
                    var kicked = false;

                    if (submerged > 0f)
                    {
                        if (currentFluid != null)
                        {
                            kick = kickScale;
                            kicked = true;
                        }
                        else if (sampledFluid != null && movementProfile)
                        {
                            movementProfile.Resolve(GetMovementMode(), sampledFluid, MovementTuning.Current, kickMovement);
                            ApplyLoad(kickMovement);

                            if (kickMovement.jumpHeight > currentMovementSettings.jumpHeight)
                            {
                                force = kickMovement.jumpForce;
                                apex = kickMovement.apexTime;
                                kick = kickScale;
                                kicked = true;
                            }
                        }
                    }

                    if (kicked)
                    {
                        kickWeight = kick * kick;
                        hopUntil = Time.time + apex / Mathf.Max(0.05f, kick);
                    }
                    else
                    {
                        hopUntil = 0f;
                    }

                    rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, force * kick * jumpValue * jumpScale);

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

            // How far it's pushed, from -1 to 1, with a touch of play so a stick that doesn't quite rest at the middle doesn't walk him off.
            walkInput = Mathf.Abs(xPress) < WalkDeadZone ? 0f : Mathf.Clamp(xPress, -1f, 1f);
            walkInputFrame = Time.frameCount;

            if (walkInput < 0f)
            {
                facing = CharacterFacing.Left;
            }
            else if (walkInput > 0f)
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
            InteractHeld = held;
        }

        // Whether the Interact button is being held. A lever that has to be held to work it goes by this. (A character that has its own
        // InteractHold calls this one too.)
        public bool InteractHeld { get; private set; }

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
                UpdateLoad();
                StartInteractAnimation();
                return;
            }

            var tooHeavy = false;

            foreach (var co in carryableObjects)
            {
                var carry = co.Carry(this);
                if (carry)
                {
                    objectCarrying = carry;
                    UpdateLoad();
                    StartInteractAnimation();
                    //Can only carry one object.  If they pick something up, they can't interact
                    return;
                }

                tooHeavy |= co.TooHeavyFor(this);
            }

            // Something within reach that's more than he can lift, and nothing lighter to pick up instead: he reaches for it, and it stays where it is.
            if (tooHeavy)
            {
                StartInteractAnimation();
                return;
            }

            foreach (var io in interactableObjects)
            {
                var interacting = io.Interact(this);
                if (interacting)
                {
                    StartInteractAnimation();
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
            ApplyLoad(resolvedMovement);
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

        // How far he is above what's under him, in units, from his feet: 0 standing on something, and a large number if there's nothing below. Whatever is
        // lifting him, it's how far he's been lifted.
        public float HeightAboveGround
        {
            get { return Mathf.Min(distFromGround, 100f); }
        }

        // How fast he's walking, in units a second, and which way: positive is right. What he's trying to do, not how fast he's actually going, which a
        // wall or a rope can hold back. The Balloon's string goes by it to be pulled along.
        public float WalkSpeedNow
        {
            get { return moveSpeed; }
        }

        // The nearest other character in the game with some of his body within reach of a point, or nothing. For the Balloon, taking hold of someone with
        // the end of his string.
        public static Character NearestOther(Character except, Vector2 point, float reach)
        {
            Character nearest = null;
            var nearestDistance = reach;

            if (characters == null)
            {
                return null;
            }

            foreach (var other in characters)
            {
                if (!other || other == except || !other.isActiveAndEnabled || !other.InGame || !other.ableToGrabObjects)
                {
                    continue;
                }

                var distance = other.DistanceFromBody(point);

                if (distance <= nearestDistance)
                {
                    nearest = other;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        // The nearest thing that can be carried with some of it within reach of a point, or nothing, and how far it is. Not what someone is carrying or what
        // hangs from a rope already, and not a character (they're looked for with NearestOther). For the Balloon, taking hold of it with the end of his string.
        public static CarryableObject NearestCarryable(Character except, Vector2 point, float reach, out float distance)
        {
            CarryableObject nearest = null;
            distance = reach;

            if (carryableObjects == null)
            {
                return null;
            }

            foreach (var thing in carryableObjects)
            {
                if (!thing || !thing.isActiveAndEnabled || thing.IsCarried || thing.IsHung || thing.GetComponent<Character>() ||
                    (except && thing.gameObject == except.gameObject))
                {
                    continue;
                }

                var away = thing.DistanceFrom(point);

                if (away <= distance)
                {
                    nearest = thing;
                    distance = away;
                }
            }

            return nearest;
        }

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

        // What they are carrying now, or nothing.
        public CarryableObject CarriedObject
        {
            get { return objectCarrying; }
        }

        // ----- The weight of what he carries -----
        //
        // While he holds something, its mass is added to his own, less what his Carry Strength takes off it: a Carry Strength of 50 and a crate of
        // 200 add 100. It's his actual mass, so it counts everywhere his mass does: on a button, on a floor that crumbles, when he's pushed. And
        // his walking speed and his jumping go down by his own mass over his mass with the load (30 of 70, so a speed of 1.5 is 0.6428).

        // His mass without what he carries, and the mass he was given with it, to tell when something else sets his mass (a Brick going solid).
        private float ownMass;
        private float massWithLoad;

        // His own mass over his mass with what he carries: 1 with nothing, and less the more he feels.
        private float loadFactor = 1f;

        // How much of what he is carrying counts, after his Carry Strength.
        public float CarriedMassFelt
        {
            get
            {
                return objectCarrying ? objectCarrying.Mass * (1f - Mathf.Clamp01(carryStrength / 100f)) : 0f;
            }
        }

        // Mass he takes on as he holds something up that isn't carried: as heavy as that much as far as the physics goes, so that he's not a light body with a
        // very heavy one hanging from him. It's not carried weight, so it doesn't slow him.
        protected virtual float BorneMass
        {
            get { return 0f; }
        }

        private void UpdateLoad()
        {
            // Not for a body whose mass is worked out from its shape, which can't be set.
            if (!rigidBody2D || rigidBody2D.useAutoMass)
            {
                return;
            }

            // Something else has set his mass since: that's his own mass now.
            if (!Mathf.Approximately(rigidBody2D.mass, massWithLoad))
            {
                ownMass = rigidBody2D.mass;
            }

            massWithLoad = ownMass + CarriedMassFelt + BorneMass;

            // What he bears for something else (the Balloon holding up what hangs from his string) is mass for the physics only: it doesn't slow his walking.
            loadFactor = ownMass / Mathf.Max(0.0001f, ownMass + CarriedMassFelt);

            if (!Mathf.Approximately(rigidBody2D.mass, massWithLoad))
            {
                rigidBody2D.mass = massWithLoad;
            }
        }

        // Slows his walking and lowers his jumping by the weight he's carrying. His jump height is cut by the same fraction as his speed, so
        // the speed he leaves the ground at is cut by its square root.
        private void ApplyLoad(ResolvedMovement movement)
        {
            if (loadFactor >= 0.9999f)
            {
                return;
            }

            var launch = Mathf.Sqrt(loadFactor);

            movement.maxMoveSpeed *= loadFactor;
            movement.moveSpeedIncrease *= loadFactor;
            movement.moveSpeedDecay *= loadFactor;
            movement.jumpHeight *= loadFactor;
            movement.jumpForce *= launch;
            movement.apexTime *= launch;
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