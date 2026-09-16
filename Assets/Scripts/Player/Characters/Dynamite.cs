using System.Collections.Generic;
using System.Linq;
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
        public bool charging;
        public float explodingTime;
        public Sprite explodedSprite;
        public Sprite explodedJumpSprite;

        public GameObject explosionPiece;
        public float numExplosionPieces;

        private List<GameObject> explodedPieces;
        private List<DamageableTile> explodedTiles;

        public int pieceCount;

        protected override void Start()
        {
            explodedPieces = new List<GameObject>();
            explodedTiles = new List<DamageableTile>();
            explosionPiece.SetActive(false);
            base.Start();
        }

        protected override void Update()
        {
            base.Update();

            if (performingAction && !exploded)
            {
                charging = true;
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
        
        protected override void GetCurrentMovementSetting(bool isInLiquid, float density)
        {
            string settingName;

            /*if (isInLiquid)
            {
                if (performingAction)
                {
                    settingName = "bouncyliquid";
                }
                else
                {
                    if (charging)
                    {
                        settingName = "chargingliquid";
                    }
                    else
                    {
                        settingName = "liquid";
                    }
                }
            }
            else
            {
                if (performingAction)
                {
                    settingName = "bouncy";
                }
                else
                {
                    if (charging)
                    {
                        settingName = "charging";
                    }
                    else
                    {
                        settingName = "base";
                    }
                }
            }*/

            settingName = "base";

            currentMovementSettings = GetMovementSetting(settingName, density);

            //Do not call the base function
        }

        private void Explode()
        {
            exploded = true;
            explodingTime = Time.time;
            
            for (int i = 0; i < numExplosionPieces; i++)
            {
                var piece = Instantiate(explosionPiece);
                piece.SetActive(true);
                var angle = Random.Range(0, 360);
                var vector = GameMath.GetMagnitudeFromAngle(angle, 0.25f);
                piece.transform.position = new Vector3(
                    transform.position.x + vector.x,
                    transform.position.y + vector.y,
                    transform.position.z
                );
                
                var rb = piece.GetComponent<Rigidbody2D>();
                rb.AddForce(
                    vector * (explosionForce * 15),
                    ForceMode2D.Impulse
                );

                explodedPieces.Add(piece);
            }

            var allObjects = FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList();

            var dynamiteTiles = DamageableTile.GetDynamiteTiles();

            foreach (var dynamiteTile in dynamiteTiles)
            {
                var dTile = dynamiteTile.Key;
                var position = dynamiteTile.Value;
                
                var distance = Vector2.Distance(transform.position, position);
                if (distance > explosionDistance)
                {
                    continue;
                }

                Map.DestroyTile(position);
                dTile.GetRigidbody2D().bodyType = RigidbodyType2D.Dynamic;
                dTile.EnableSpriteRenderer(true);
                allObjects.Add(dTile.GetRigidbody2D());

                explodedTiles.Add(dTile);
            }

            foreach (var rb in allObjects)
            {
                if (rb.bodyType != RigidbodyType2D.Static)
                {
                    if (rb.gameObject == gameObject)
                    {
                        continue;
                    }
                    
                    var distance = Vector2.Distance(rb.position, transform.position);

                    if (distance > explosionDistance)
                    {
                        continue;
                    }

                    var angle = GameMath.GetAngle(transform.position, rb.transform.position);
                    var force = Mathf.Abs((explosionDistance - distance) * explosionForce);
                    var dir = GameMath.GetMagnitudeFromAngle(angle);

                    dir = new Vector2(dir.x * force, dir.y * force);

                    rb.AddForce(dir, ForceMode2D.Impulse);
                }
            }
        }

        private void CalculateExplosionPieces()
        {
            pieceCount = explodedPieces.Count;
            if (explodedPieces.Count > 0)
            {
                foreach (var piece in explodedPieces)
                {
                    if (piece)
                    {
                        var explosionShrinkRate = 20000;
                        var newExplosionSize = explosionShrinkRate * Time.deltaTime;
                        if (newExplosionSize > 0.95f)
                        {
                            newExplosionSize = 0.95f;
                        }

                        var newSize = piece.transform.localScale * newExplosionSize;
                        piece.transform.localScale = newSize;

                        if (piece.transform.localScale.x < 0.001f)
                        {
                            Destroy(piece.gameObject);
                        }
                    }
                }
                
                explodedPieces = explodedPieces.Where(p => p.gameObject).ToList();
            }

            if (explodedTiles.Count > 0)
            {
                foreach (var explodedTile in explodedTiles)
                {
                    var distance = Vector2.Distance(transform.position, explodedTile.transform.position);
                    if (distance > 25)
                    {
                        explodedTile.EnableSpriteRenderer(false);
                        explodedTile.GetRigidbody2D().bodyType = RigidbodyType2D.Kinematic;
                        explodedTile.transform.position = new Vector3(
                            explodedTile.position.x,
                            explodedTile.position.y,
                            explodedTile.transform.position.z
                        );
                        
                        explodedTile.GetRigidbody2D().linearVelocity = Vector2.zero;
                        explodedTile.SetDestroyed();
                    }
                }
            }
        }
    }
}