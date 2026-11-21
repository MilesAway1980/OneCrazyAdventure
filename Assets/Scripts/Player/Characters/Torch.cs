using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    // Burns, lights sconces he stands by, and warms others, until he's put out under liquid.
    public class Torch : Character
    {
        public Sprite unlitStand;
        public Sprite unlitJump;

        [Tooltip("How near he has to be to the middle of a sconce to light it, in units. It's a circle around him, so it's the same all ways.")]
        public float sconceReach = 0.5f;

        private List<Sconce> sconces;

        private LightSource lightSource;
        private HeatSource heatSource;

        protected override void Start()
        {
            base.Start();
            sconces = new List<Sconce>(FindObjectsByType<Sconce>(FindObjectsInactive.Exclude));

            lightSource = GetComponent<LightSource>();
            heatSource = GetComponent<HeatSource>();
        }

        // ----- On a machine that has joined someone else's game -----

        // Whether he's burning, which is all another machine needs to show him: he's put out in liquid.
        public bool IsBurning
        {
            get { return !inLiquid; }
        }

        // Shows him lit, or put out, as the host says: his light, and his unlit pictures. Called after the picture has been chosen
        // for the way he's facing and whether he's in the air.
        public void ShowLit(bool lit)
        {
            if (lightSource)
            {
                lightSource.SetLit(lit);
            }

            if (!lit && spriteRenderer)
            {
                spriteRenderer.sprite = isJumping ? unlitJump : unlitStand;
            }
        }

        protected override void Update()
        {
            base.Update();

            // Under liquid he's put out, and he burns again, and warms again, once he's out of it.
            var lit = !inLiquid;

            // Sconces that have been destroyed since are dropped from the list.
            for (int i = sconces.Count - 1; i >= 0; i--)
            {
                var sconce = sconces[i];

                if (!sconce)
                {
                    sconces.RemoveAt(i);
                }
                else if (lit && Vector2.Distance(transform.position, sconce.transform.position) < sconceReach)
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
            }

            if (lightSource)
            {
                lightSource.SetLit(lit);
            }

            if (heatSource)
            {
                heatSource.SetOn(lit);
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
