using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    // Burns, lights sconces he stands by, and warms others, until he's put out under liquid.
    public class Torch : Character
    {
        [Tooltip("The picture shown while he's standing or walking and has been put out.")]
        public Sprite unlitStand;
        [Tooltip("The picture shown while he's in the air and has been put out.")]
        public Sprite unlitJump;

        [Tooltip("How near he has to be to the middle of a sconce to light it, in units. It's a circle around him, so it's the same all ways.")]
        public float sconceReach = 0.5f;

        // ----- Animation -----
        //
        // Put out under liquid, he's Snuffed: the same clips, drawn charred, with no flame. Carrying something while he is, he's SnuffedCarrying.
        // His own unlit pictures above are for when he has no clips. See Docs/Animation.md.
        public const string SnuffedVariant = "Snuffed";
        public const string SnuffedCarryingVariant = "SnuffedCarrying";

        // Coming back out of liquid, he lights up in stages: a small flame flickering on his charred head, in and out, before the big one. While the
        // small flame is showing he's Flickering (or FlickeringCarrying).
        public const string FlickeringVariant = "Flickering";
        public const string FlickeringCarryingVariant = "FlickeringCarrying";

        [Header("Lighting up again")]
        [Tooltip("When he comes out of liquid, how long each stage of lighting up lasts, in seconds: a small flame, then out, then a small flame, then out, " +
                 "and so on, ending on a small flame, and then he's burning fully. Put the numbers in odd or even as you like. Empty: he's lit as soon " +
                 "as he's out.")]
        public float[] relightSeconds = { 0.12f, 0.08f, 0.14f, 0.06f, 0.18f };

        [Tooltip("How bright his light is while the small flame shows, from 0 (out) to 1 (his full light).")]
        [Range(0f, 1f)]
        public float smallFlameLight = 0.4f;

        // How long his light takes to go out, and to change between the small flame and out. He's put out at once.
        private const float QuickFadeSeconds = 0.05f;

        // How much he's burning: 0 is out, 1 is fully lit, and the small flame is in between (Small Flame Light).
        public float Flame { get; private set; } = 1f;

        private bool wasInLiquid;
        private bool relighting;
        private float relightTimer;

        // Put out the moment he's in liquid, and, once he's out of it, the stages of lighting up.
        private void UpdateFlame()
        {
            if (inLiquid)
            {
                wasInLiquid = true;
                relighting = false;
                Flame = 0f;
                return;
            }

            if (wasInLiquid)
            {
                wasInLiquid = false;
                relighting = relightSeconds != null && relightSeconds.Length > 0;
                relightTimer = 0f;
            }

            if (!relighting)
            {
                Flame = 1f;
                return;
            }

            relightTimer += Time.deltaTime;

            var stageEnds = 0f;

            for (var i = 0; i < relightSeconds.Length; i++)
            {
                stageEnds += Mathf.Max(relightSeconds[i], 0f);

                if (relightTimer < stageEnds)
                {
                    // The even stages are the small flame, and the odd ones he's out.
                    Flame = i % 2 == 0 ? smallFlameLight : 0f;
                    return;
                }
            }

            relighting = false;
            Flame = 1f;
        }

        protected override string AnimationVariant()
        {
            var ordinary = base.AnimationVariant();
            var carrying = ordinary == CharacterAnimationSet.Carrying;

            if (Flame >= 1f)
            {
                return ordinary;
            }

            if (Flame > 0f)
            {
                return carrying ? FlickeringCarryingVariant : FlickeringVariant;
            }

            return carrying ? SnuffedCarryingVariant : SnuffedVariant;
        }

        // His light for how much he's burning.
        private void ShowLight(float flame)
        {
            if (!lightSource)
            {
                return;
            }

            if (flame >= 1f)
            {
                lightSource.ClearLevel();
                lightSource.SetLit(true);
            }
            else if (flame > 0f)
            {
                lightSource.SetLevel(flame, QuickFadeSeconds);
            }
            else
            {
                lightSource.ClearLevel();
                lightSource.SetLit(false, QuickFadeSeconds);
            }
        }

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

        // Shows him as the host says he's burning (see Flame), which is all another machine needs: his light, and his unlit pictures. Called after the
        // picture has been chosen for the way he's facing and whether he's in the air.
        public void ShowLit(float flame)
        {
            ShowLight(flame);

            if (flame <= 0f && spriteRenderer && !ShowingAnimation)
            {
                spriteRenderer.sprite = isJumping ? unlitJump : unlitStand;
            }
        }

        protected override void Update()
        {
            // Before the animation is chosen, as it depends on it.
            UpdateFlame();

            base.Update();

            // Put out under liquid, and lit again, with a few flickers, once he's out of it. He only lights sconces and warms things once he's
            // burning fully.
            var lit = Flame >= 1f;

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

            // His unlit pictures, unless his clips are drawing him, which they do charred too.
            if (Flame <= 0f && !ShowingAnimation)
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

            ShowLight(Flame);

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
