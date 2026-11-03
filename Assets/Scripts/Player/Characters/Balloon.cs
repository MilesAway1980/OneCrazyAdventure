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

        protected override void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            string settingName;
            if (performingAction)
            {
                settingName = isInLiquid ? "floatliquid" : "float";
            }
            else
            {
                settingName = isInLiquid ? "liquid" : "base";
            }

            currentMovementSettings = GetMovementSetting(settingName, density);

            //Do not call the base function
        }
    }
}
