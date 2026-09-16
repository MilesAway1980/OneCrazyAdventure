using UnityEngine;

namespace Characters
{
    public class Umbrella : Character
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        //private MovementSettings baseMovementSettings;
        private MovementSettings floatMovementSettings;

        protected override void Start()
        {
            base.Start();
        }

        public float floatGravity;

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();
        }

        protected override void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            string settingName;
            if (performingAction)
            {
                if (inLiquid)
                {
                    settingName = "liquidfloat";
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

            //baseMovementSettings = GetMovementSetting(settingName, density);
            currentMovementSettings = GetMovementSetting(settingName, density);

            //Do not call the base function
        }

        public override void Action(bool active)
        {
            performingAction = active;
        }

        public override void FirstActionPress()
        {
            rigidBody2D.linearVelocity = new Vector2Int(0, 0);
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