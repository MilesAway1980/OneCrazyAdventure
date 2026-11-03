using UnityEngine;

namespace Characters
{
    // Held open, he floats down slowly, but only in the air: in liquid he can't float and moves as he does shut. He
    // stays open if the player moves to another character, as characters are meant to work together.
    public class Umbrella : Character
    {
        protected override void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            string settingName;
            if (isInLiquid)
            {
                settingName = "liquid";
            }
            else
            {
                settingName = performingAction ? "float" : "base";
            }

            currentMovementSettings = GetMovementSetting(settingName, density);

            //Do not call the base function
        }

        public override void Action(bool active)
        {
            performingAction = active;
        }

        // Opening him in the air stops his fall, so he catches the air from where he is. How fast he is moving
        // sideways, or up, is left alone, so he can get a running start and then float.
        public override void FirstActionPress()
        {
            if (!inLiquid && rigidBody2D.linearVelocity.y < 0f)
            {
                rigidBody2D.linearVelocityY = 0f;
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
