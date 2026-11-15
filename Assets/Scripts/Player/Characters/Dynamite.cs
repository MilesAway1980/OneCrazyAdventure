using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    public class Dynamite : Character
    {
        public float chargeValueIncreaseSpeed;
        public float maximumChargeValue;
        public float chargeValue;
        public float explosionForce;
        public float explosionDistance;
        public float explosionResetTimeSeconds;

        public bool exploded;
        public float explodingTime;
        public Sprite explodedSprite;

        public GameObject explosionPiece;
        public int numExplosionPieces;

        [Header("Charge Meter")]
        [Tooltip("A bar in the upper right of the view of the player using him, showing how close the fuse is to going off. It appears while he charges, drains when the action is let go, and fades just after an explosion.")]
        public MeterLayout chargeMeter = new MeterLayout { showSeconds = 0.5f };

        // The pieces shrink by this much each frame at 60 frames per second, and by the same amount per second at any other rate.
        private const float PieceShrinkPerFrame = 0.95f;
        private const float PieceGoneScale = 0.001f;

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

            CalculateExplosionPieces();

            if (exploded)
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

        private void Explode()
        {
            exploded = true;
            explodingTime = Time.time;

            // Everything the blast can push, found before his own pieces exist so they aren't pushed a second time.
            var bodies = new HashSet<Rigidbody2D>(FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude));

            if (explosionPiece)
            {
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

                body.AddForce(direction * force, ForceMode2D.Impulse);
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
