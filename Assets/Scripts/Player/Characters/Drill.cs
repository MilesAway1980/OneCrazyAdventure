using UnityEngine;

namespace Characters
{
    // He has a drill on his head, which normally points straight up. Jump up into a tile that can be dug and hold the action
    // button, and he drills up through it for as long as it is held. To drill down instead, jump, push down and hold the
    // action button: he flips upside down, and drills into whatever he lands on.
    //
    // To drill sideways, press the action button while he's running. He turns on his side, toward the way he's running, and
    // coasts on at the speed he had, slowing to a stop the way he does when the stick is let go, with no control from the
    // stick, until he meets a tile that can be dug and drills into it if the button is held. If the button is let go, he
    // gets back up. Only his picture turns on its side, on its own object, so the rest of him stays upright.
    //
    // He gets through a tile in its hardness divided by his power, in seconds, and he works his way through it as he goes:
    // the tip of the drill is just touching a tile at 0%, halfway through it at 50% and at the far side of it at 100%, when
    // it is gone, and he can go on into the next. If he lets go partway, he backs out of the tile again.
    //
    // To set him up, make a character like the others (with the floor and wall detectors and the movement settings), and
    // give him a collider that is centred on him, so that he flips about his middle, with the SpriteRenderer on a child
    // object, centred on him too, so that it can be turned on its own.
    public class Drill : Character
    {
        [Tooltip("How much he can drill through in a second. A tile with a hardness equal to this takes a second, and one twice as hard takes two.")]
        public float drillPower = 5f;
        [Tooltip("How fast he backs out of a tile when he lets go partway, as a multiple of how fast he drilled into it.")]
        public float backOutSpeed = 1.5f;
        [Tooltip("How fast, in units per second, he centres himself on the column or row of tiles he is drilling into.")]
        public float alignSpeed = 4f;
        [Range(0f, 1f)]
        [Tooltip("How fast he has to be going, as a fraction of his top running speed, for the action button to turn him on his side instead of drilling up.")]
        public float runSpeedNeeded = 0.8f;

        [Min(0.01f)]
        [Tooltip("How much speed he loses each physics step (50 a second) coasting on his side. His normal decay is 1 a step, " +
                 "which stops him from full speed in a fifth of a second. Smaller glides further: at a top speed of 10, " +
                 "0.2 takes a second and carries him about 5 units, and 0.1 takes two seconds and about 10.")]
        public float sideCoastDecay = 0.2f;

        private enum Mode
        {
            Free,       // Walking about, and ready to drill
            Drilling,   // In a tile, going deeper while the button is held, and out of it when it isn't
            Settling    // Through a tile, and still for a moment while the physics takes the tile's solid part away
        }

        // How close, in units, the tip of the drill has to be to a tile to be touching it.
        private const float TipTolerance = 0.06f;

        // How long, in seconds, he holds still after getting through a tile, for the physics to take its solid part away.
        private const float SettleSeconds = 0.12f;

        // How far down the stick has to be pushed to count as down.
        private const float DownThreshold = -0.5f;

        private Mode mode;
        private bool actionHeld;
        private bool downHeld;
        private float stickX;

        // Which way the drill points: up, down, or left or right when he's on his side.
        private Vector2Int pointing = Vector2Int.up;

        // How fast he was going when he turned on his side, which is how fast he sets off again if he turns round.
        private float chargeSpeed;

        // How fast he is coasting along on his side, which way he is going being its sign, until he slows to a stop.
        private float coastSpeed;

        private Collider2D bodyCollider;
        private RigidbodyType2D bodyTypeBefore;

        // The tile he is in, and the way he is drilling, how many tiles long the block it is part of is, how far through it
        // he is, from 0 to 1, where he is when the tip of the drill is just touching the face of it, and where across the
        // line of tiles he is going to be.
        private DamageableTile tile;
        private Vector2 direction;
        private int rows = 1;
        private float depth;
        private Vector2 startPosition;
        private float crossTarget;

        // Where he holds still while settling, and until when.
        private Vector2 holdPosition;
        private float settleUntil;

        // The picture of him, which is what turns when he goes on his side, and where it sits when he's upright.
        private Transform pictureTransform;
        private Vector3 pictureRestPosition;
        private Quaternion pictureRestRotation;

        private bool OnSide
        {
            get { return pointing.x != 0; }
        }

        protected override void Start()
        {
            base.Start();

            bodyCollider = GetComponent<Collider2D>();

            if (spriteRenderer)
            {
                pictureTransform = spriteRenderer.transform;
                pictureRestPosition = pictureTransform.localPosition;
                pictureRestRotation = pictureTransform.localRotation;

                if (pictureTransform == transform)
                {
                    Debug.LogWarning("Drill: his picture is on the same object as his collider, so it can't be turned on its " +
                                     "own. Put the SpriteRenderer on a child object.", this);
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (mode == Mode.Free)
            {
                // To drill down he has to be in the air, with down and the action held. He turns back over when it's let go.
                if (pointing == Vector2Int.up && actionHeld && downHeld && isJumping)
                {
                    SetPointing(Vector2Int.down);
                }
                else if (pointing == Vector2Int.down && !actionHeld)
                {
                    SetPointing(Vector2Int.up);
                }
                else if (OnSide && !actionHeld)
                {
                    // On his side with the button let go, he gets up. Only the picture is on its side, so there's nothing
                    // that needs room for him to do it.
                    SetPointing(Vector2Int.up);
                }
            }

            // He can only walk and jump standing up. Drilling, upside down or on his side, his feet aren't on the ground, so
            // the stick does nothing. On his side with the button let go he just coasts to a stop, until he's up again.
            canMove = mode == Mode.Free && pointing == Vector2Int.up;
            performingAction = mode != Mode.Free || pointing != Vector2Int.up;
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            switch (mode)
            {
                case Mode.Free:
                    Coast();

                    LookToDrill();
                    break;

                case Mode.Drilling:
                    Advance();
                    break;

                case Mode.Settling:
                    Settle();
                    break;
            }
        }

        // On his side he can't be steered, but he goes on at the speed he had and slows to a stop by sideCoastDecay a step.
        // The base slows a character by holding a speed and slowing it, and its slowing is too quick for a running dive, so
        // he's slowed here instead, by his own hand, at a rate that can be set, which friction doesn't get in the way of.
        private void Coast()
        {
            if (!OnSide)
            {
                return;
            }

            moveSpeed = 0f;

            if (coastSpeed != 0f)
            {
                coastSpeed = Mathf.MoveTowards(coastSpeed, 0f, sideCoastDecay);
                rigidBody2D.linearVelocityX = coastSpeed;
            }
        }

        // Taken out of the game partway through a tile, he's let go of so that nothing is left with him.
        void OnDisable()
        {
            if (mode != Mode.Free)
            {
                Release();
            }

            if (pointing != Vector2Int.up)
            {
                SetPointing(Vector2Int.up);
            }
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            downHeld = directionValue.y < DownThreshold;
            stickX = directionValue.x;

            if (canMove)
            {
                Move(directionValue);
            }
        }

        public override void Action(bool active)
        {
            if (active && !actionHeld && mode == Mode.Free && !(downHeld && isJumping))
            {
                TurnOnSideIfRunning();
            }

            actionHeld = active;
        }

        // Pressing the action button while running turns him on his side, toward the way he is running. If he is already on
        // his side, the stick can't move him, but pressing the action button with it held the other way turns him round and
        // he charges off that way, which is how he gets out of a tunnel that is a dead end. So does running the other way.
        private void TurnOnSideIfRunning()
        {
            if (pointing == Vector2Int.down)
            {
                return;
            }

            if (OnSide && Mathf.Abs(stickX) > -DownThreshold && (stickX > 0f ? 1 : -1) != pointing.x)
            {
                SetPointing(new Vector2Int(-pointing.x, 0));
                coastSpeed = pointing.x * chargeSpeed;
                return;
            }

            var speed = rigidBody2D.linearVelocity.x;

            if (Mathf.Abs(speed) < runSpeedNeeded * currentMovementSettings.maxMoveSpeed)
            {
                return;
            }

            var way = speed > 0f ? 1 : -1;

            if (pointing.x == way)
            {
                return;
            }

            chargeSpeed = Mathf.Min(Mathf.Abs(speed), currentMovementSettings.maxMoveSpeed);

            SetPointing(new Vector2Int(way, 0));
            coastSpeed = way * chargeSpeed;
        }

        // The player has moved on to another character, so the buttons they were holding will never be seen let go. He lets
        // go of them himself, which backs him out of a tile he's in, and gets him up or turns him back over.
        public override void ControlLost()
        {
            actionHeld = false;
            downHeld = false;
            stickX = 0f;
        }

        // Turns him so that the drill points another way. Upside down turns the whole of him over, collider and all. On his
        // side, only the picture is turned a quarter: he stays upright for the physics, so his feet and wall detectors stay
        // where they were and nothing about how he moves or where he stands changes.
        private void SetPointing(Vector2Int value)
        {
            // Getting up, he's handed back to the base with the speed he has left, so it slows him as it does anyone.
            if (OnSide && value.x == 0)
            {
                moveSpeed = coastSpeed;
                coastSpeed = 0f;
            }

            pointing = value;

            var scale = transform.localScale;
            scale.y = Mathf.Abs(scale.y) * (value.y < 0 ? -1f : 1f);
            transform.localScale = scale;

            TurnPicture(-90f * value.x);
        }

        // Turns the picture on his side by an angle, about the middle of him, leaving the rest of him as he is.
        private void TurnPicture(float angle)
        {
            if (!pictureTransform || pictureTransform == transform)
            {
                return;
            }

            var turn = Quaternion.Euler(0f, 0f, angle);

            pictureTransform.localRotation = turn * pictureRestRotation;
            pictureTransform.localPosition = turn * pictureRestPosition;
        }

        // The tip of the drill: the edge of him the way he points, in the middle of it. On his side, the picture is turned
        // but the collider isn't, so the drill reaches as far out sideways as it does upward: half his height from the
        // middle of him, which is further than his collider goes.
        private Vector2 TipPosition()
        {
            var bounds = bodyCollider.bounds;

            if (OnSide)
            {
                return new Vector2(bounds.center.x + pointing.x * bounds.size.y / 2f, bounds.center.y);
            }

            return new Vector2(bounds.center.x, pointing.y < 0 ? bounds.min.y : bounds.max.y);
        }

        // The tile that the tip of the drill is touching the face of, if it is one that can be dug the way he is going.
        private bool TryFindTile(Vector2 way, out DamageableTile found, out Vector2Int cell)
        {
            // The space just past the tip, if the tip is no further than the tolerance from it.
            var past = TipPosition() + way * TipTolerance;

            cell = new Vector2Int(Mathf.FloorToInt(past.x), Mathf.FloorToInt(past.y));
            found = DamageableTile.At(cell);

            return found && found.CanBeDugFrom(way);
        }

        // With the action held, he starts into a tile the tip of the drill has come to.
        private void LookToDrill()
        {
            if (!actionHeld)
            {
                return;
            }

            var way = (Vector2)pointing;

            if (TryFindTile(way, out var found, out var cell))
            {
                StartDrilling(found, cell, way);
            }
        }

        // He goes into the tile, with no collision with it as it's the tile he's going through. He is moved through it by
        // hand, so the physics doesn't push him out of it.
        private void StartDrilling(DamageableTile target, Vector2Int cell, Vector2 way)
        {
            if (mode == Mode.Free)
            {
                bodyTypeBefore = rigidBody2D.bodyType;
                rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
            }

            rigidBody2D.linearVelocity = Vector2.zero;
            moveSpeed = 0f;
            coastSpeed = 0f;

            mode = Mode.Drilling;
            tile = target;
            direction = way;
            rows = target.DigBlockLength(way);
            depth = 0f;

            // Where he is when the tip is exactly on the face of the tile: where he is now, moved by the little the tip is
            // off it, along the way he's going. The face is the near side of the tile, which is its far side, in numbers,
            // when going the negative way.
            var vertical = way.y != 0f;
            var faceCell = vertical ? cell.y : cell.x;
            var faceAlong = (vertical ? way.y : way.x) * (((vertical ? way.y : way.x) > 0f) ? faceCell : faceCell + 1);
            var tipAlong = Vector2.Dot(way, TipPosition());

            startPosition = rigidBody2D.position + way * (faceAlong - tipAlong);
            crossTarget = (vertical ? cell.x : cell.y) + 0.5f;
        }

        // Drilling: deeper into the tile while the button is held, and back out of it when it isn't. Through it, it's gone.
        private void Advance()
        {
            rigidBody2D.linearVelocity = Vector2.zero;

            // It may have gone some other way, or stopped being one that can be dug.
            if (!tile || !tile.CanBeDugFrom(direction))
            {
                StartSettling();
                return;
            }

            // How much of the tile he gets through in a second: the harder it is, the less.
            var speed = drillPower / tile.Hardness;

            depth += (actionHeld ? speed : -speed * backOutSpeed) * Time.fixedDeltaTime;

            if (depth >= 1f)
            {
                depth = 1f;
                PlaceInTile();

                tile.BreakByDig(direction, rows);
                StartSettling();
                return;
            }

            if (depth <= 0f)
            {
                depth = 0f;

                // Out the way he came, with the button let go: he's free of it.
                if (!actionHeld)
                {
                    PlaceInTile();
                    Release();
                    return;
                }
            }

            tile.ShowDig(depth, direction, rows);
            PlaceInTile();
        }

        // Where he should be for how far through the block he is: the tip of the drill is that far into it, and he is lined
        // up with the tile.
        private Vector2 PositionInTile()
        {
            return startPosition + direction * (depth * rows);
        }

        // Puts him there, moving across toward the middle of the line of tiles as he goes.
        private void PlaceInTile()
        {
            var target = PositionInTile();
            var current = rigidBody2D.position;
            var step = alignSpeed * Time.fixedDeltaTime;

            if (direction.y != 0f)
            {
                target.x = Mathf.MoveTowards(current.x, crossTarget, step);
            }
            else
            {
                target.y = Mathf.MoveTowards(current.y, crossTarget, step);
            }

            rigidBody2D.MovePosition(target);
        }

        // Through a tile, or out of one that has gone. The tip of the drill is where the far side of it was, and he holds
        // still there for a moment.
        private void StartSettling()
        {
            var position = PositionInTile();
            var current = rigidBody2D.position;

            // Across the line he stays where he is.
            holdPosition = direction.y != 0f ? new Vector2(current.x, position.y) : new Vector2(position.x, current.y);
            settleUntil = Time.time + SettleSeconds;
            tile = null;
            mode = Mode.Settling;
        }

        // Still for a moment, and then on into the next tile if the button is still held and there is one, or free.
        private void Settle()
        {
            rigidBody2D.linearVelocity = Vector2.zero;
            rigidBody2D.MovePosition(holdPosition);

            if (Time.time < settleUntil)
            {
                return;
            }

            if (actionHeld && TryFindTile(direction, out var next, out var cell))
            {
                StartDrilling(next, cell, direction);
                return;
            }

            Release();
        }

        // Back to being an ordinary body, in whatever space is round him.
        private void Release()
        {
            rigidBody2D.bodyType = bodyTypeBefore;
            rigidBody2D.linearVelocity = Vector2.zero;

            mode = Mode.Free;
            tile = null;
            rows = 1;
            depth = 0f;
        }
    }
}
