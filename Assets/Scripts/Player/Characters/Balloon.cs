using UnityEngine;

namespace Characters
{
    public class Balloon : Character
    {
        protected override void Start()
        {
            // His string hangs from him, so taking hold of the end of it would tie him in a loop.
            ableToGrabObjects = false;

            base.Start();
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }

        public override void Action(bool active)
        {
            performingAction = active;
        }

        // Held, he lets air in to float; otherwise he moves as anyone does. What the liquid does to him is looked after.
        protected override string GetMovementMode()
        {
            return performingAction ? "float" : "base";
        }
    }
}
