using UnityEngine;

namespace Characters
{
    public class Generic : Character
    {
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