using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    public class Dynamite : Character
    {
        [Tooltip("How fast, each second, his charge builds up while the ability button is held.")]
        public float chargeValueIncreaseSpeed;
        [Tooltip("The most charge he can build up.")]
        public float maximumChargeValue;
        [Tooltip("How much charge he has now. For watching, not for setting.")]
        public float chargeValue;
        [Tooltip("How hard the blast pushes things away and damages tiles. The nearer to him, the more of it they get.")]
        public float explosionForce;
        [Tooltip("How far, in units, the blast reaches.")]
        public float explosionDistance;
        [Tooltip("How long, in seconds, after he has exploded until he's put back together.")]
        public float explosionResetTimeSeconds;

        [Tooltip("Whether he has blown up and is waiting to be put back together. For watching, not for setting.")]
        public bool exploded;
        [Tooltip("When he blew up, in game time. For watching, not for setting.")]
        public float explodingTime;
        [Tooltip("The picture shown while he's blown up.")]
        public Sprite explodedSprite;

        [Tooltip("The piece that flies out when he blows up. A copy of it is thrown out for each of the pieces.")]
        public GameObject explosionPiece;
        [Tooltip("How many pieces fly out when he blows up.")]
        public int numExplosionPieces;

        [Header("Charge Meter")]
        [Tooltip("A bar in the upper right of the view of the player using him, showing how close the fuse is to going off. It appears while he charges, drains when the action is let go, and fades just after an explosion.")]
        public MeterLayout chargeMeter = new MeterLayout { showSeconds = 0.5f };

        // The pieces shrink by this much each frame at 60 frames per second, and by the same amount per second at any other rate.
        private const float PieceShrinkPerFrame = 0.95f;
        private const float PieceGoneScale = 0.001f;

        // ----- Animation -----
        //
        // He loves going off. As the fuse burns (his charge) he's in one of three states, each a grin that gets wilder and a spark that works down the
        // fuse: Charge1, Charge2 and Charge3, by how far the charge has got. Going off is Boom, the blast, for a moment. Then, for as long as he's blown
        // up, he's himself still, standing, walking, in the air, interacting and carrying, but with the variant Exploded (ExplodedCarrying when carrying):
        // his blown-up picture, with smoke drifting off it. He can still move, so each of those has its own pictures. And when he's put back together
        // it's Restore, a puff of smoke and a few twinkles. See Docs/Animation.md.
        public const string Charge1State = "Charge1";
        public const string Charge2State = "Charge2";
        public const string Charge3State = "Charge3";
        public const string BoomState = "Boom";
        public const string RestoreState = "Restore";
        public const string ExplodedVariant = "Exploded";
        public const string ExplodedCarryingVariant = "ExplodedCarrying";

        // How long the Boom clip takes: its five pictures, at 14 a second. And the Restore clip: its four, at ten.
        private const float BoomSeconds = 0.36f;
        private const float RestoreSeconds = 0.4f;

        private float restoreUntil;
        private bool wasExploded;

        protected override string AnimationState(string commonState)
        {
            if (exploded)
            {
                // The blast, and then whatever he'd be doing, in his blown-up pictures (the variant).
                return Time.time - explodingTime < BoomSeconds ? BoomState : commonState;
            }

            if (commonState == CharacterAnimationSet.Action || commonState == CharacterAnimationSet.AirAction)
            {
                var fraction = ChargeFraction;

                return fraction < 0.34f ? Charge1State : (fraction < 0.67f ? Charge2State : Charge3State);
            }

            if (Time.time < restoreUntil)
            {
                return RestoreState;
            }

            return commonState;
        }

        protected override string AnimationVariant()
        {
            var ordinary = base.AnimationVariant();

            if (!exploded)
            {
                return ordinary;
            }

            return ordinary == CharacterAnimationSet.Carrying ? ExplodedCarryingVariant : ExplodedVariant;
        }

        private List<GameObject> explodedPieces;

        private ScreenMeter meter;
        private float meterShowUntil;

        // The charge the bar shows. It holds the last value while the bar fades out after an explosion.
        private float displayedCharge;

        protected override void Start()
        {
            explodedPieces = new List<GameObject>();

            if (explosionPiece)
            {
                explosionPiece.SetActive(false);
            }
            else
            {
                Debug.LogError($"{name}: the Dynamite needs an explosion piece, or there is nothing to show when he goes off.", this);
            }

            base.Start();

            meter = gameObject.AddComponent<ScreenMeter>();
        }

        protected override void Update()
        {
            base.Update();

            if (performingAction && !exploded)
            {
                chargeValue += chargeValueIncreaseSpeed * Time.deltaTime;
                if (chargeValue > maximumChargeValue)
                {
                    Explode();
                }
            }
            else
            {
                if (exploded)
                {
                    chargeValue = 0;
                }
                else
                {
                    chargeValue -= chargeValueIncreaseSpeed * Time.deltaTime;
                    if (chargeValue < 0)
                    {
                        chargeValue = 0;
                    }
                }
            }

            if (Time.time - explodingTime >= explosionResetTimeSeconds)
            {
                exploded = false;
            }

            // Put back together: his Restore clip shows for a moment.
            if (wasExploded && !exploded)
            {
                restoreUntil = Time.time + RestoreSeconds;
            }

            wasExploded = exploded;

            CalculateExplosionPieces();

            // His blown-up picture, unless his clips are drawing him, which they do.
            if (exploded && !ShowingAnimation)
            {
                spriteRenderer.sprite = explodedSprite;
            }

            UpdateMeter();
        }

        // Keeps the charge bar up to date: shown while the fuse is burning, and for a moment after.
        private void UpdateMeter()
        {
            if (!meter)
            {
                return;
            }

            meter.SetLayout(chargeMeter);

            if (chargeValue > 0f)
            {
                displayedCharge = maximumChargeValue > 0f ? Mathf.Clamp01(chargeValue / maximumChargeValue) : 0f;
                meterShowUntil = Time.time + chargeMeter.showSeconds;
            }

            meter.Show(
                GetPlayerCamera(),
                displayedCharge,
                chargeMeter.show && Time.time < meterShowUntil,
                Color.Lerp(Color.yellow, Color.red, displayedCharge));
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
            if (exploded)
            {
                performingAction = false;
                return;
            }

            performingAction = active;
        }

        // The pieces that fly out of him as he goes off, which only show it: they push nothing.
        private void SpawnPieces()
        {
            if (!explosionPiece)
            {
                return;
            }

            for (int i = 0; i < numExplosionPieces; i++)
            {
                var piece = Instantiate(explosionPiece);
                piece.SetActive(true);
                var vector = GameMath.GetMagnitudeFromAngle(Random.Range(0f, 360f), 0.25f);
                piece.transform.position = transform.position + (Vector3)vector;

                var rb = piece.GetComponent<Rigidbody2D>();
                rb.AddForce(
                    vector * (explosionForce * 15),
                    ForceMode2D.Impulse
                );

                explodedPieces.Add(piece);
            }
        }

        // ----- On a machine that has joined someone else's game -----
        //
        // The host works out when he goes off, what the blast pushes and which tiles it breaks, and sends when he goes off and how far the fuse
        // is burned. This machine shows it: his exploded picture, the pieces flying out, and the fuse bar.

        // How far the fuse has burned, from 0 to 1.
        public float ChargeFraction
        {
            get { return maximumChargeValue > 0f ? Mathf.Clamp01(chargeValue / maximumChargeValue) : 0f; }
        }

        // He has just gone off, as the host says.
        public void ShowExplosion()
        {
            SpawnPieces();
        }

        // Called every frame, after his picture has been chosen for walking and jumping.
        public void ShowState(bool isExploded, float chargeFraction)
        {
            exploded = isExploded;
            chargeValue = chargeFraction * maximumChargeValue;

            CalculateExplosionPieces();

            if (isExploded && spriteRenderer && !ShowingAnimation)
            {
                spriteRenderer.sprite = explodedSprite;
            }

            UpdateMeter();
        }

        private void Explode()
        {
            exploded = true;
            explodingTime = Time.time;

            // Everything the blast can push, found before his own pieces exist so they aren't pushed a second time.
            var bodies = new HashSet<Rigidbody2D>(FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude));

            SpawnPieces();

            // The tiles the blast reaches, with the same push as everything else: strongest where he is, and fading to
            // nothing at the edge of the blast. Each decides for itself what it does with it.
            DamageableTile.Blast(transform.position, explosionDistance, explosionForce);

            foreach (var body in bodies)
            {
                if (body.bodyType == RigidbodyType2D.Static || body.gameObject == gameObject)
                {
                    continue;
                }

                var offset = body.position - (Vector2)transform.position;
                var distance = offset.magnitude;

                if (distance > explosionDistance)
                {
                    continue;
                }

                // Pushed away from him, or straight up if it's exactly where he is and there's no away.
                var direction = distance > 0.0001f ? offset / distance : Vector2.up;
                var force = (explosionDistance - distance) * explosionForce;

                Character.Push(body, direction * force, ForceMode2D.Impulse);
            }
        }

        private void CalculateExplosionPieces()
        {
            var shrink = Mathf.Pow(PieceShrinkPerFrame, Time.deltaTime * 60f);

            for (int i = explodedPieces.Count - 1; i >= 0; i--)
            {
                var piece = explodedPieces[i];

                if (!piece)
                {
                    explodedPieces.RemoveAt(i);
                    continue;
                }

                piece.transform.localScale *= shrink;

                if (piece.transform.localScale.x < PieceGoneScale)
                {
                    Destroy(piece);
                    explodedPieces.RemoveAt(i);
                }
            }
        }
    }
}
