using UnityEngine;

namespace Characters
{
    public class Pebble : Character
    {
        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }
    }
}
