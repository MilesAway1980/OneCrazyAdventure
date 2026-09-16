using UnityEngine;

namespace Characters
{
    public class Bulb : Character
    {
        public BrightTileSource normalBrightness;
        public BrightTileSource useBrightness;

        protected override void Start()
        {
            useBrightness.ShutOff();
            base.Start();
        }

        /*protected override void Update()
        {
            base.Update();
        }*/

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
            //useBrightness.enabled = active;
            //normalBrightness.enabled = !active;

            if (active)
            {
                useBrightness.ResetIntensity();
                normalBrightness.ShutOff();
            }
            else
            {
                useBrightness.ShutOff();
                normalBrightness.ResetIntensity();
            }
        }
    }
}