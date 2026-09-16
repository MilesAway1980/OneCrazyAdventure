using UnityEngine;

namespace Characters
{
    public class Balloon : Character
    {
        /*protected override void Start()
        {
            base.Start();
        }

        protected override void Update()
        {
            base.Update();
        }*/

        public override void DirectionPress(Vector2 directionValue)
        {
            //var yPress = directionValue.y;

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
                if (inLiquid)
                {
                    settingName = "floatliquid";
                }
                else
                {
                    settingName = "float";
                }
            }
            else
            {
                if (inLiquid)
                {
                    settingName = "liquid";
                }
                else
                {
                    settingName = "base";
                }
            }

            currentMovementSettings = GetMovementSetting(settingName, density);

            //Do not call the base function
        }
    }
}