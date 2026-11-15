using UnityEngine;

namespace Characters
{
    // Held open, he floats down slowly, but only in the air: in liquid he can't float and moves as he does shut. He
    // stays open if the player moves to another character, as characters are meant to work together.
    //
    // Open, he catches the wind of a BlowerFan far more than anything else does, and rides it up. While the wind is
    // lifting him, the extra pull his float settings give an upward speed, which is there to stop him jumping high while
    // open, is left off, so he can be carried up a long way, and for a moment after it stops.
    public class Umbrella : Character
    {
        [Header("Wind")]
        [Tooltip("How many times the force of a blower fan he feels while he is open. At 1 he is no different from anyone else.")]
        [Min(0f)]
        public float openWindMultiplier = 4f;

        [Tooltip("How many times the force of a blower fan he feels while he is shut.")]
        [Min(0f)]
        public float closedWindMultiplier = 1f;

        // How long, in seconds, he goes on being treated as riding the wind after it last pushed him up, so that he isn't
        // stopped dead at the edge of it.
        private const float UpdraftGraceSeconds = 0.3f;

        private WindCatcher windCatcher;

        // His float settings, but with nothing to pull an upward speed down, for while the wind is lifting him.
        private MovementSettings updraftSettings;

        // Whether he is held open in the air.
        private bool IsFloating
        {
            get { return performingAction && !inLiquid; }
        }

        // Whether the wind has been lifting him, just now.
        private bool InUpdraft
        {
            get
            {
                return windCatcher && Time.time - windCatcher.LastWindTime < UpdraftGraceSeconds && windCatcher.LastWind.y > 0f;
            }
        }

        protected override void Start()
        {
            base.Start();

            windCatcher = GetComponent<WindCatcher>();

            if (!windCatcher)
            {
                windCatcher = gameObject.AddComponent<WindCatcher>();
            }

            // Made after the base class has collected the movement settings, so this isn't one of them.
            foreach (var candidate in GetComponentsInChildren<MovementSettings>())
            {
                if (candidate.identifier.ToLower() == "float")
                {
                    updraftSettings = gameObject.AddComponent<MovementSettings>();
                    updraftSettings.CloneSettings(candidate);
                    updraftSettings.identifier = "Updraft";

                    // The pull on an upward speed is what stops him being carried up.
                    updraftSettings.heldJumpDecay = 1f;
                    updraftSettings.releasedJumpDecay = 1f;
                    break;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            // How much of the wind he catches changes with whether he is open.
            windCatcher.Multiplier = IsFloating ? openWindMultiplier : closedWindMultiplier;
        }

        protected override void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            if (!isInLiquid && performingAction && updraftSettings && InUpdraft)
            {
                currentMovementSettings = updraftSettings;

                // What picking a setting normally does as well, which the base class keeps to itself.
                rigidBody2D.gravityScale = updraftSettings.gravity;
                rigidBody2D.angularDamping = updraftSettings.angularDampening;
                rigidBody2D.linearDamping = updraftSettings.linearDampening * density;

                return;
            }

            string settingName;
            if (isInLiquid)
            {
                settingName = "liquid";
            }
            else
            {
                settingName = performingAction ? "float" : "base";
            }

            currentMovementSettings = GetMovementSetting(settingName, density);

            //Do not call the base function
        }

        public override void Action(bool active)
        {
            performingAction = active;
        }

        // Opening him in the air stops his fall, so he catches the air from where he is. How fast he is moving
        // sideways, or up, is left alone, so he can get a running start and then float.
        public override void FirstActionPress()
        {
            if (!inLiquid && rigidBody2D.linearVelocity.y < 0f)
            {
                rigidBody2D.linearVelocityY = 0f;
            }
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }
    }
}
