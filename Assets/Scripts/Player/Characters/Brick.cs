using UnityEngine;

namespace Characters
{
    public class Brick : Character
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void Start()
        {
            base.Start();
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();
        }
        
        public override void Action(bool active)
        {
            performingAction = active;
        }
        
        /*protected override void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            string settingName;

            if (isInLiquid)
            {
                if (performingAction)
                {
                    settingName = "bouncyliquid";
                }
                else
                {
                    if (charging)
                    {
                        settingName = "chargingliquid";
                    }
                    else
                    {
                        settingName = "liquid";
                    }
                }
            }
            else
            {
                if (performingAction)
                {
                    settingName = "bouncy";
                }
                else
                {
                    if (charging)
                    {
                        settingName = "charging";
                    }
                    else
                    {
                        settingName = "base";
                    }
                }
            }

            currentMovementSettings = GetMovementSetting(settingName, density);

            //Do not call the base function
        }*/
        
        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }
    }
}