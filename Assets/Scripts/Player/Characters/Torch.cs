using UnityEngine;

namespace Characters
{
    public class Torch : Character
    {
        public Sprite unlitStand;
        public Sprite unlitJump;

        private Sconce[] sconces;

        private BrightTileSource brightTileSource;
        private HeatSource heatSource;

        protected override void Start()
        {
            base.Start();
            sconces = FindObjectsByType<Sconce>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            brightTileSource = GetComponent<BrightTileSource>();
            heatSource = GetComponent<HeatSource>();
        }

        protected override void Update()
        {
            base.Update();
            foreach (var sconce in sconces)
            {
                if (Vector3.Distance(transform.position, sconce.transform.position) < 0.5f)
                {
                    sconce.Light();
                }
            }

            if (inLiquid)
            {
                if (isJumping)
                {
                    spriteRenderer.sprite = unlitJump;
                }
                else
                {
                    spriteRenderer.sprite = unlitStand;
                }

                brightTileSource.ShutOff();
            }
            else
            {
                brightTileSource.ResetIntensity();
            }
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
    }
}