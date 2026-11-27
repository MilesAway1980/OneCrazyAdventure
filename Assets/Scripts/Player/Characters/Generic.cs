using UnityEngine;

namespace Characters
{
    public class Generic : Character
    {
        // How soft he is when his profile doesn't say, for ShapeFlex. A hint of give.
        protected override float DefaultSoftness
        {
            get { return 0.15f; }
        }

        /*
        protected override void Start()
        {
            base.Start();
        }

        protected override void Update()
        {
            base.Update();
        }
        */

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
    }
}