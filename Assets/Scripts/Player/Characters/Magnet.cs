using UnityEngine;

namespace Characters
{
    // Holding the action switches on a magnetic field that pulls things toward him. He stays on if the player moves
    // to another character, as characters are meant to work together, and only switches off when the action is let go
    // while he is being played.
    public class Magnet : Character
    {
        [SerializeField]
        [Tooltip("The child object with the Point Effector 2D that pulls things toward him. It's switched on while the ability is held.")]
        private GameObject magneticField;

        [Tooltip("How hard the field pulls things toward him. It's the strength of the field's Point Effector 2D, which is set from this.")]
        public float pullStrength = 500f;

        private PointEffector2D effector;

        // ----- Animation -----
        //
        // His field on is his Action (standing) and AirAction (in the air): the field lines flickering round him. Walking with it on is his own
        // state, ActionWalk, which is his walk with the lines. A set without an ActionWalk clip shows his ordinary walk, as before.
        public const string ActionWalkState = "ActionWalk";

        protected override string AnimationState(string commonState)
        {
            if (commonState == CharacterAnimationSet.Action && WalkingForAnimation && HasAnimationClip(ActionWalkState))
            {
                return ActionWalkState;
            }

            return commonState;
        }

        protected override void Start()
        {
            base.Start();

            if (!magneticField)
            {
                Debug.LogError($"{name}: the Magnet has no magnetic field assigned, so it can't pull anything.", this);
                return;
            }

            effector = magneticField.GetComponent<PointEffector2D>();

            if (!effector)
            {
                Debug.LogError($"{name}: the magnetic field has no Point Effector 2D, so it can't pull anything.", this);
            }

            magneticField.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();

            // A negative force pulls toward the effector. Every frame, so it can be tuned while playing.
            if (effector && !Mathf.Approximately(effector.forceMagnitude, -pullStrength))
            {
                effector.forceMagnitude = -pullStrength;
            }
        }

        // On a machine that has joined someone else's game: the field is shown while the host says he's using it.
        public void ShowField(bool on)
        {
            if (magneticField && magneticField.activeSelf != on)
            {
                magneticField.SetActive(on);
            }
        }

        // Called every frame with whether the button is held.
        public override void Action(bool active)
        {
            performingAction = active;

            if (magneticField && magneticField.activeSelf != active)
            {
                magneticField.SetActive(active);
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
