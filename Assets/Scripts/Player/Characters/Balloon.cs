using UnityEngine;

namespace Characters
{
    public class Balloon : Character
    {
        // ----- Animation -----
        //
        // Holding the action blows him up: the clip for Action grows him from his ordinary size to his full one, and goes on by itself into Inflated, the
        // full size, held for as long as he is. It's the same clip in the air, so that it isn't started over as he lifts off. Letting go, he shrinks back
        // for a moment, which is his own state, Deflate (the same clip the other way round). While someone has hold of the end of his string, or is
        // carrying him, he's Held: the same clips, with the eyes squeezed shut, > and <, and no mouth. He also blinks, standing. His squashing and
        // stretching as he moves, and squishing when he hits something, is ShapeFlex, and not clips. See Docs/Animation.md.
        public const string InflatedState = "Inflated";
        public const string DeflateState = "Deflate";
        public const string HeldVariant = "Held";

        // How long the Deflate clip takes: its six pictures, at 14 a second.
        private const float DeflateSeconds = 0.43f;
        private float deflateUntil;

        private GrabPoint[] grabPoints;
        private CarryableObject carryable;

        [Header("Debugging")]
        [Tooltip("Writes what he and whoever is hanging from his string are doing, every physics step while someone is, to Logs/BalloonRope.csv in the " +
                 "project: where each is, how fast, how far the string reaches and how tight it is, and what his settling did. It's started over each time " +
                 "the game starts.")]
        public bool logRope;

        private System.IO.StreamWriter ropeLog;
        private int ropeLogStep;
        private Vector2 ropeLogLastBalloon;
        private Vector2 ropeLogLastHanger;

        [Header("String")]
        [Tooltip("How long his string is, in units. It's made when he starts.")]
        public float stringLength = 7f;

        [Tooltip("How far apart the points of the string are, in units. Fewer, further apart, makes a stiffer string; more, closer together, a more flexible " +
                 "one, which hangs and piles up more like a real string and costs a little more to work out. It's made when he starts.")]
        public float segmentSpacing = 0.4f;

        [Tooltip("The colour of the line that's drawn along the string.")]
        public Color stringColor = new Color(0.95f, 0.95f, 0.95f);

        [Tooltip("How thick the line is, in units.")]
        public float stringWidth = 0.06f;

        [Header("What hangs from him")]
        [Tooltip("The mass, in units, that he can just hold up while he's floating, by what's on his string: a character hanging from it, or something he's " +
                 "carrying. Anything lighter and he rises, more slowly the heavier it is, until at this much he hovers. Anything heavier and he sinks, " +
                 "faster the heavier it is. 0 means nothing can be lifted at all.")]
        public float liftCapacity = 20f;

        [Tooltip("How fast, in units a second, the heaviest loads fall with him. What he can't hold up, he slows: a light enough one he slows to his own " +
                 "slow fall, and a very heavy one hardly at all.")]
        public float heavyFallSpeed = 12f;

        [Tooltip("How quickly, in units a second a second, a load that's too much for him pulls him down to the speed it should.")]
        public float loadAcceleration = 15f;

        [Tooltip("How quickly, each second, he settles to the way what hangs from his string is moving sideways as the string comes tight, so that he doesn't " +
                 "swing about it like a pendulum. Higher settles faster.")]
        public float settleRate = 6f;

        [Tooltip("How high, in units, what's on his string is off the ground before he starts to move as he does lifting it, and not as he does while they're standing " +
                 "and he's pulling or being pulled along. Below this he moves with them on the ground.")]
        public float groundedHeight = 0.1f;

        [Tooltip("How high, in units, what's on his string is off the ground before he moves entirely as he does lifting it. Between the two heights the one " +
                 "gives way to the other a bit at a time, so his speed never changes all at once as they leave the ground.")]
        public float liftedHeight = 2f;

        private BalloonString rope;
        private readonly System.Collections.Generic.List<Rigidbody2D> heldBodies = new System.Collections.Generic.List<Rigidbody2D>();

        // His string, made if it hasn't been: a rope worked out by script, with a line drawn along it (the pieces of the old one, in the scene, are taken away
        // if they're still there). Done by whichever of him and his network script gets there first, so that every machine has it.
        public BalloonString EnsureString()
        {
            if (!rope)
            {
                rope = GetComponent<BalloonString>();

                if (!rope)
                {
                    rope = gameObject.AddComponent<BalloonString>();
                    rope.Setup(this);
                }
            }

            return rope;
        }

        // How much hangs from the string and weighs on him, in mass: a character or a crate hanging from it, as far as the string is taut
        // enough to be holding it up, since a load lying on the ground on a slack string weighs nothing on him. Also how much of a pull it is, in the
        // same way, as a force.
        private float LoadMass(out float weightForce)
        {
            weightForce = 0f;

            if (!rope || grabPoints == null)
            {
                return 0f;
            }

            var mass = 0f;

            GetHeldBodies();

            foreach (var body in heldBodies)
            {
                mass += body.mass;
                weightForce += body.mass * body.gravityScale * Mathf.Abs(Physics2D.gravity.y);
            }

            weightForce *= rope.Hang;
            return mass * rope.Hang;
        }

        private float LoadMass()
        {
            return LoadMass(out _);
        }

        // Fills in the bodies of what hangs from his string.
        private void GetHeldBodies()
        {
            heldBodies.Clear();

            foreach (var point in grabPoints)
            {
                if (point)
                {
                    point.GetHeldBodies(heldBodies);
                }
            }
        }

        // The load as a fraction of what he can hold up: 0 is none, 1 is as much as he can hover with.
        private float LoadFraction()
        {
            var mass = LoadMass();

            if (mass <= 0f)
            {
                return 0f;
            }

            return liftCapacity > 0f ? mass / liftCapacity : 100f;
        }

        // Floating, with a load on the string, the speed he and what's on it settle at: up at his usual speed less the load's share of it, none at all at his
        // capacity, and, over it, down, faster the heavier the load, not faster than a very heavy load falls.
        private float TargetSpeed(float fraction, float usualSpeed)
        {
            return fraction < 1f ? usualSpeed * (1f - fraction) : -heavyFallSpeed * (1f - 1f / fraction);
        }

        // Not floating, he falls slowly, and a load makes it faster, more the heavier it is, and a very heavy one hardly slower than it would fall.
        private float LoadedFallSpeed(float ownSpeed, float fraction)
        {
            return ownSpeed + (Mathf.Max(heavyFallSpeed, ownSpeed) - ownSpeed) * (fraction / (1f + fraction));
        }

        protected override float TerminalRiseSpeed(float fromMovement)
        {
            var fraction = LoadFraction();

            return fraction <= 0f ? fromMovement : (fraction < 1f ? fromMovement * (1f - fraction) : 0f);
        }

        protected override float TerminalFallSpeed(float fromMovement)
        {
            var fraction = LoadFraction();

            if (fraction <= 0f)
            {
                return fromMovement;
            }

            // Floating, a load that falls is slowed by his lift, a little at a time, as it's pulled; it isn't stopped dead. So there's no limit set here, just
            // what pulls him and what holds him up. Not floating, he slows it as much as he does himself, more the lighter it is.
            return performingAction ? 1000f : Mathf.Max(fromMovement, LoadedFallSpeed(fromMovement, fraction));
        }

        // How much of the load he bears, in mass: up to his capacity of what's hanging from the string, ramped, not at once. He's a light body, and what hangs
        // from his string may be many times as heavy, which the physics doesn't manage well: so what he bears is added to his mass, and his lift is made to
        // hold it up. Then the two are about as heavy as each other, and the string pulls on him no harder than he's holding up.
        private float borne;
        private float borneInMass;

        protected override float BorneMass
        {
            get
            {
                borneInMass = borne;
                return borne;
            }
        }

        // His lift, with the load he bears: the buoyancy that's worked out is for him alone, so what he bears is added to it as the lift needed to hold up that
        // much, and spread over the mass he has now. Floating only: when he isn't, he bears nothing, and is pulled about as the light thing he is.
        protected override float ShapeBuoyancyWeight(float weight)
        {
            var hangingMass = 0f;

            if (performingAction && rope && grabPoints != null)
            {
                hangingMass = HeldMass() * rope.Hang;
            }

            var target = Mathf.Min(hangingMass, Mathf.Max(0f, liftCapacity));
            borne = Mathf.MoveTowards(borne, target, Mathf.Max(liftCapacity, 10f) / 0.4f * Time.fixedDeltaTime);

            if (weight >= 0f || borne <= 0f)
            {
                return weight;
            }

            // His own mass, without what he bears (which is in his mass by now).
            var own = Mathf.Max(0.01f, rigidBody2D.mass - borneInMass);

            return (own * weight - borne) / (own + borne);
        }

        // The mass of what's hanging from the string.
        private float HeldMass()
        {
            var mass = 0f;

            GetHeldBodies();

            foreach (var body in heldBodies)
            {
                mass += body.mass;
            }

            return mass;
        }

        // What he and the load settle at while he's floating with it on the string, a bit at a time. A load that falls and brings the string tight is slowed,
        // its fall getting slower and slower until it stops, and then, if he's strong enough for it, it's lifted, slowly. At his capacity he can only hold it
        // where it is.
        protected override void ShapeVerticalSpeed()
        {
            var before = rigidBody2D.linearVelocity;

            SettleToLoad();

            if (logRope)
            {
                WriteRopeLog(before);
            }

            if (!performingAction)
            {
                return;
            }

            var mass = LoadMass();

            if (mass <= 0f)
            {
                return;
            }

            var fraction = liftCapacity > 0f ? mass / liftCapacity : 100f;
            var target = TargetSpeed(fraction, currentMovementSettings.maxFallSpeed);
            var velocity = rigidBody2D.linearVelocity;

            rigidBody2D.linearVelocity = new Vector2(velocity.x, Mathf.MoveTowards(velocity.y, target, loadAcceleration * Time.fixedDeltaTime));
        }

        // One line of the log: where he and who's hanging are, and how fast, and how far they moved this step, the string, and what settling did to his speed.
        private void WriteRopeLog(Vector2 velocityBefore)
        {
            if (!rope || !rope.HasHanger)
            {
                return;
            }

            if (ropeLog == null)
            {
                var path = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "BalloonRope.csv");
                ropeLog = new System.IO.StreamWriter(path, false) { AutoFlush = true };
                ropeLog.WriteLine("step,time,floating,balloonX,balloonY,balloonMoveX,balloonMoveY,balloonVxBefore,balloonVyBefore,balloonVxAfter,balloonVyAfter," +
                                  "hangerX,hangerY,hangerMoveX,hangerMoveY,hangerVx,hangerVy,reach,length,stretch,tautness,approach,tension,relativeSpeed,loadFraction,hangerWalk,balloonWalk");
                ropeLogLastBalloon = rigidBody2D.position;
                ropeLogLastHanger = rope.HangerPosition;
            }

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var balloon = rigidBody2D.position;
            var after = rigidBody2D.linearVelocity;
            var hanger = rope.HangerPosition;

            ropeLog.WriteLine(string.Join(",", new[]
            {
                (ropeLogStep++).ToString(inv), Time.time.ToString("0.000", inv), performingAction ? "1" : "0",
                balloon.x.ToString("0.0000", inv), balloon.y.ToString("0.0000", inv),
                (balloon.x - ropeLogLastBalloon.x).ToString("0.0000", inv), (balloon.y - ropeLogLastBalloon.y).ToString("0.0000", inv),
                velocityBefore.x.ToString("0.000", inv), velocityBefore.y.ToString("0.000", inv), after.x.ToString("0.000", inv), after.y.ToString("0.000", inv),
                hanger.x.ToString("0.0000", inv), hanger.y.ToString("0.0000", inv),
                (hanger.x - ropeLogLastHanger.x).ToString("0.0000", inv), (hanger.y - ropeLogLastHanger.y).ToString("0.0000", inv),
                rope.HangerVelocity.x.ToString("0.000", inv), rope.HangerVelocity.y.ToString("0.000", inv),
                rope.Reach.ToString("0.0000", inv), rope.Length.ToString("0.0000", inv), (rope.Reach - rope.Length).ToString("0.0000", inv),
                rope.Tautness.ToString("0.000", inv), rope.Approach.ToString("0.000", inv), rope.Tension.ToString("0.0", inv),
                (after.x - rope.HangerVelocity.x).ToString("0.000", inv), LoadFraction().ToString("0.000", inv),
                rope.HangerWalk.ToString("0.000", inv), WalkSpeedNow.ToString("0.000", inv)
            }));

            ropeLogLastBalloon = balloon;
            ropeLogLastHanger = hanger;
        }

        void OnDisable()
        {
            if (ropeLog != null)
            {
                ropeLog.Dispose();
                ropeLog = null;
            }
        }

        // As the string comes tight with someone hanging from it, whatever sideways speed the string has given him, beyond how he's walking and how they're
        // moving, is settled away, a bit at a time, more the nearer to tight it is. He's light and they may be much heavier, so the string brings him round to
        // above them, and any speed it leaves him with would swing him about them, as a pendulum, for a long time. It's only what the string gave him: his own
        // walking is left alone, so he can go where he wants, and what they're doing is what he's measured from, so being dragged along by someone running is
        // left alone as well. And what's left is never more than his top speed.
        private void SettleToLoad()
        {
            if (!rope || !rope.HasHanger || rope.Approach <= 0f)
            {
                return;
            }

            var velocity = rigidBody2D.linearVelocity;
            var settle = 1f - Mathf.Exp(-settleRate * rope.Approach * Time.fixedDeltaTime);
            var fastest = Mathf.Max(0.1f, currentMovementSettings.maxMoveSpeed);

            // Standing on the ground, they can pull him along, by walking, and he can pull them. He goes at whichever of his own walking and theirs is the
            // faster (not both added together, or each would pull the other up to a speed neither had), and whatever the string has given him beyond that is
            // settled away. Theirs is how they're walking, and not how fast they're going: that's partly his own doing, through the string, and if he went at
            // it he'd be pulled along by his own pulling, faster and faster.
            var theirs = rope.HangerWalk;
            var intended = Mathf.Abs(theirs) > Mathf.Abs(WalkSpeedNow) ? theirs : WalkSpeedNow;
            var onGround = intended + Mathf.Clamp(Mathf.Lerp(velocity.x - intended, 0f, settle), -fastest, fastest);

            // With who's hanging off the ground, he and they move together as he steers: it's his walking, and nothing else, that takes them anywhere, so
            // whatever else they've given each other, swinging or being pulled, is settled away, and the two never go faster than he does on his own.
            var settled = Mathf.Lerp(velocity.x - WalkSpeedNow, 0f, settle);
            var lifted = Mathf.Clamp(WalkSpeedNow + settled, -fastest, fastest);

            // From one to the other as they're lifted, by how high they are, so there's no moment at which his speed changes: it's the same lift all the way up.
            var liftedBy = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(groundedHeight, Mathf.Max(groundedHeight + 0.01f, liftedHeight), rope.HangerHeight));

            rigidBody2D.linearVelocity = new Vector2(Mathf.Lerp(onGround, lifted, liftedBy), velocity.y);
        }

        // Interact lets go of whatever is hanging from his string, if anything is. If nothing is, and a character or a crate is within reach of the end of it,
        // he takes hold of the nearer with it (a character can let go with Interact too). Only one thing at a time. Otherwise it's as usual.
        public override void Interact()
        {
            var letGo = false;

            if (grabPoints != null)
            {
                foreach (var point in grabPoints)
                {
                    if (point && point.ReleaseAll())
                    {
                        letGo = true;
                    }
                }
            }

            if (letGo)
            {
                StartInteractAnimation();
                return;
            }

            if (TakeHoldOfSomething())
            {
                StartInteractAnimation();
                return;
            }

            base.Interact();
        }

        // Takes hold of the nearest character or crate within reach of the end of his string, which hangs from it as if a character had taken hold of it himself.
        // A crate isn't carried by him: it hangs, as a character does, with the string going to it. Says whether he took hold of anything.
        private bool TakeHoldOfSomething()
        {
            if (grabPoints == null)
            {
                return false;
            }

            foreach (var point in grabPoints)
            {
                if (!point || point.IsHeld)
                {
                    continue;
                }

                var where = (Vector2)point.transform.position;
                var someone = NearestOther(this, where, point.reach);
                var thing = NearestCarryable(this, where, point.reach, out var thingDistance);
                var someoneDistance = someone ? someone.DistanceFromBody(where) : float.MaxValue;

                if (someone && (!thing || someoneDistance <= thingDistance) && point.Interact(someone))
                {
                    return true;
                }

                if (thing && point.HoldThing(thing))
                {
                    return true;
                }
            }

            return false;
        }

        // As soft as they come: ShapeFlex stretches him as he goes and squishes him when he hits something.
        protected override float DefaultSoftness
        {
            get { return 1f; }
        }

        protected override string AnimationState(string commonState)
        {
            // Inflated, he's one clip whether he's on the ground or in the air.
            if (commonState == CharacterAnimationSet.AirAction)
            {
                return CharacterAnimationSet.Action;
            }

            if (Time.time < deflateUntil && commonState != CharacterAnimationSet.Action)
            {
                return DeflateState;
            }

            return commonState;
        }

        protected override string AnimationVariant()
        {
            var ordinary = base.AnimationVariant();

            return ordinary.Length == 0 && performingAction && IsHeldByAnother ? HeldVariant : ordinary;
        }

        // Whether someone is hanging from his string or carrying him. It's only a strain when he's inflated and trying to lift them, which is his Action.
        private bool IsHeldByAnother
        {
            get
            {
                if (carryable && carryable.IsCarried)
                {
                    return true;
                }

                if (grabPoints != null)
                {
                    foreach (var point in grabPoints)
                    {
                        if (point && point.IsHeld)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        protected override void Start()
        {
            // His string hangs from him, so taking hold of the end of it would tie him in a loop.
            ableToGrabObjects = false;

            // And what he takes hold of with it isn't carried, but hangs from it: so there's nothing else he picks up.
            ableToCarryObjects = false;

            // His string, which is a rope worked out by script, with the end of it somewhere to take hold of with Interact and hang from.
            EnsureString();

            grabPoints = new[] { EnsureString().Grab };
            carryable = GetComponent<CarryableObject>();

            base.Start();
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
            if (active)
            {
                deflateUntil = 0f;
            }
            else if (performingAction)
            {
                deflateUntil = Time.time + DeflateSeconds;
            }

            performingAction = active;
        }

        // Held, he lets air in to float; otherwise he moves as anyone does. What the liquid does to him is looked after.
        protected override string GetMovementMode()
        {
            return performingAction ? "float" : "base";
        }
    }
}
