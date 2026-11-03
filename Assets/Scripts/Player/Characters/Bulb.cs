using UnityEngine;
using UnityEngine.Serialization;

namespace Characters
{
    // Glows normally, and brighter while the action is held. He stays as bright as he was left if the player moves to
    // another character, as characters are meant to work together.
    public class Bulb : Character
    {
        [Tooltip("The bulb's light. Its unlit look is the normal glow, and its lit look is the brighter glow while the action is held.")]
        [FormerlySerializedAs("useBrightness")]
        [FormerlySerializedAs("brightTileSource")]
        public LightTileSource lightTileSource;

        protected override void Start()
        {
            if (!lightTileSource)
            {
                lightTileSource = GetComponent<LightTileSource>();
            }

            if (lightTileSource)
            {
                lightTileSource.SetLit(false);
            }
            else
            {
                Debug.LogWarning($"{name}: the Bulb has no LightTileSource, so it has no light to brighten.", this);
            }

            base.Start();
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }

        // Called every frame with whether the button is held, so the light is only told when that changes.
        public override void Action(bool active)
        {
            if (active == performingAction)
            {
                return;
            }

            performingAction = active;

            if (lightTileSource)
            {
                lightTileSource.SetLit(active);
            }
        }
    }
}
