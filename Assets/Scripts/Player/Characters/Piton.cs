using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    // Holding the action button shows an arrow, which the stick points, and letting go throws his head that way, in an
    // arc, with the rope paying out behind it. The head sticks to the first thing it touches, and the rope can then be
    // crossed. If the rope has all been paid out first, it stops the head, which swings and falls on the end of it until
    // it touches something. He can't move until he has pulled his head back in, which he does by tapping the action button
    // twice. The head stays out if the player moves to another character, as that's how the rope is crossed.
    public class Piton : Character
    {
        [Tooltip("The most links the rope can have. Once it has this many it can't pay out any more, and the rope stops the head.")]
        public int maxLinks = 6;
        [Tooltip("The body that is left once the head is thrown. Walking about he uses the one sprite with both together.")]
        public Sprite feet;
        public Sprite head;

        [Tooltip("Where the head comes off and the rope is tied on. His centre if it's empty.")]
        public Transform neck;

        [Tooltip("How fast, in units per second, the head leaves him when he throws as hard as he can, with the stick pushed all the way out. Gravity then bends its path into an arc.")]
        public float throwSpeed = 12f;
        [Tooltip("The same for the softest throw, with the stick only just pushed out.")]
        public float softThrowSpeed = 4f;
        [Tooltip("How long the arrow that shows which way and how hard he'll throw is, in units, for the hardest throw.")]
        public float aimArrowLength = 1.5f;
        [Tooltip("The same for the softest throw.")]
        public float aimArrowShortLength = 0.5f;
        public Color aimArrowColor = new Color(1f, 0.9f, 0.3f);
        [Tooltip("How heavy the head is when thrown. The more it is, the more it falls on the way.")]
        public float headGravityScale = 2f;
        [Tooltip("How fast, in units per second, the head comes back.")]
        public float retractSpeed = 25f;
        [Tooltip("How quickly the second tap of the action button has to follow the first to pull the head back in.")]
        public float doubleTapSeconds = 0.35f;
        [Range(0f, 100f)]
        [Tooltip("BROKEN, leave it at 0. How much of his normal movement the rest of him has after his head is thrown, as a " +
                 "percentage: 0 is none at all, and 100 is as much as ever. It's how fast he walks and how high he jumps. " +
                 "Moving once the rope is out makes its joints jitter and jump around, especially when it's draped over terrain " +
                 "and he walks backwards.")]
        public float movementAfterThrow = 0f;

        [Header("Rope")]
        [Tooltip("What each link of the rope is made from: a sprite with its pivot in the middle, and a collider, like the bridge builder's link. " +
                 "Links are laid out at its length, and the last is shortened or stretched to reach the head.")]
        public GameObject linkTemplate;
        [Tooltip("How much longer the rope is than the gap, so that it hangs in a curve and sways. 0 is a straight, tight " +
                 "rope, and 0.05 is 5% longer.")]
        public float ropeSlack = 0.05f;
        [Tooltip("The colour of the line drawn through the links, which keeps them looking joined where the rope stretches and leaves gaps.")]
        public Color ropeLineColor = new Color(0.55f, 0.4f, 0.25f);
        [Tooltip("How thick that line is, in units.")]
        public float ropeLineWidth = 0.06f;

        [Tooltip("The weight of a link, and how much it resists moving, so that the rope is a firm bridge once the head is stuck.")]
        public float linkMass = 40f;
        public float linkLinearDamping = 15f;
        public float linkGravityScale = 0.1f;

        [Tooltip("The same for the rope while the head is thrown and hasn't stuck yet. It's light and loose, so it hangs and falls over things.")]
        public float looseLinkMass = 0.5f;
        public float looseLinkLinearDamping = 0.5f;
        public float looseLinkGravityScale = 1f;

        [Header("Carried rope")]
        [Tooltip("One end of the rope he carries, tied on his body. While his head is on, the rope hangs in a loop from here to the other point. It has the same line as the thrown rope.")]
        public Transform carriedRopeStart;
        [Tooltip("The other end of the rope he carries.")]
        public Transform carriedRopeEnd;
        [Tooltip("How many links the rope he carries is made of.")]
        public int carriedLinks = 8;
        [Tooltip("How much longer that rope is than the gap between its two points, so that it hangs in a loop. 0.5 is half as long again.")]
        public float carriedRopeSlack = 0.5f;

        [Tooltip("The weight of each link of the rope he carries, and how much they resist moving and turning. The more it is, the less they clump up and swing about.")]
        public float carriedLinkMass = 50f;
        public float carriedLinkLinearDamping = 200f;
        public float carriedLinkAngularDamping = 1f;
        public float carriedLinkGravityScale = 5f;

        private enum HeadState
        {
            Home,       // Part of him, and free to walk about
            Flying,     // Thrown, and not stuck to anything yet: flying, or swinging from the end of the rope
            Waiting,   // Stuck to something, with the rope out, until he is tapped back in
            Returning   // On the way back
        }

        // How far the stick has to be pushed to aim with it.
        private const float AimDeadZone = 0.25f;

        // A stick that's let go of springs back through every strength on its way to the middle, which isn't an aim. A
        // stick that is dropping faster than this, in strength per second, is ignored.
        private const float StickReleaseRate = 6f;

        // Where the arrow starts from his neck, and the size of the head of it, in units.
        private const float AimArrowStart = 0.6f;
        private const float AimArrowHead = 0.3f;
        private const float AimArrowWidth = 0.07f;

        private const float HeadSpin = -500f;

        // A last link shorter than this, as a part of a whole one, isn't added when the head sticks, as it's too short to
        // hinge. The head is just tied on to the end of the rope.
        private const float ShortestLastLink = 0.25f;

        // How much of a link's length the head has to be from the end of the rope before another link is added.
        private const float LinkAddFraction = 0.98f;

        // The layers the rope's links are on. While it's thrown it doesn't touch characters, and once the head has stuck,
        // it's something they can stand on.
        private const string ThrownRopeLayerName = "Rope";
        private const string PlacedRopeLayerName = "Ground";

        // How many points are tried along the curve of the rope for each link of it.
        private const int StepsPerLink = 32;

        private HeadState state = HeadState.Home;
        private bool started;
        private bool aiming;

        // The way he'll throw, in degrees, and whether the stick has been used to aim it since he began.
        private float aimAngle;
        private bool aimMoved;

        // How hard he'll throw, from 0 for the softest to 1 for the hardest, and how far out the stick was last time.
        private float aimPower;
        private float lastStickStrength;
        private LineRenderer aimArrow;
        private Material aimArrowMaterial;

        private float lastTapTime = float.NegativeInfinity;

        private Rigidbody2D headBody;
        private Collider2D headCollider;

        // What holds the head to the end of the rope while it's thrown, with a link's length to spare.
        private DistanceJoint2D tipJoint;

        // The point the rope is tied to, which follows his neck, and every collider that is a part of him.
        private Rigidbody2D ropeAnchor;
        private Collider2D[] ownColliders;

        // The rope he carries, and the two points on him that it's tied to.
        private GameObject carriedHolder;
        private Rigidbody2D carriedStartAnchor;
        private Rigidbody2D carriedEndAnchor;
        private readonly List<Rigidbody2D> carriedBodies = new List<Rigidbody2D>();
        private LineRenderer carriedLine;

        private int thrownRopeLayer = -1;
        private int placedRopeLayer = -1;

        // The rope that can be crossed, made of solid links.
        private GameObject ropeHolder;
        private readonly List<Rigidbody2D> ropeLinks = new List<Rigidbody2D>();
        private LineRenderer ropeLine;

        // The rope drawn while the head is out and there is no solid rope yet, or no more: while it flies and on its way
        // back. It's laid out as the solid rope will be, so that nothing changes when that takes over.
        private GameObject ropeVisual;
        private readonly List<Transform> visualLinks = new List<Transform>();

        // How wide a link of the template is before it's scaled, which is how wide its sprite is. The pivot is taken to be
        // in the middle of it. Without a sprite, it's the width of a box collider.
        private float linkWidth;

        // The points the ends of the links of the rope are at, for whichever rope was last shaped.
        private readonly List<Vector2> ropeNodes = new List<Vector2>();

        private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];

        private Vector2 NeckPosition
        {
            get { return neck ? (Vector2)neck.position : (Vector2)transform.position; }
        }

        // How much of his normal movement he has once his head is thrown, from 0 to 1.
        private float MovementAfterThrow
        {
            get { return Mathf.Clamp01(movementAfterThrow / 100f); }
        }

        // The height of a jump goes with the square of how hard it's pushed off, so it's pushed off with the square root
        // of the share he has left when his head is out, to leave him that share of the height.
        public override void Jump(bool active, float jumpValue = 1.0f)
        {
            base.Jump(active, state == HeadState.Home ? jumpValue : jumpValue * Mathf.Sqrt(MovementAfterThrow));
        }

        protected override void Start()
        {
            if (!linkTemplate)
            {
                print("Piton has no link template, so his thrown head will have no rope!");
            }
            else
            {
                if (linkTemplate.scene.IsValid())
                {
                    // The template is only for copying, so it isn't to be seen. A prefab asset is left alone.
                    linkTemplate.SetActive(false);
                }

                linkWidth = LinkWidth();
                if (linkWidth <= 0f)
                {
                    print("Piton's link template has no sprite to measure a link by, so his thrown head will have no rope!");
                }

                thrownRopeLayer = LayerMask.NameToLayer(ThrownRopeLayerName);
                placedRopeLayer = LayerMask.NameToLayer(PlacedRopeLayerName);

                if (thrownRopeLayer < 0)
                {
                    print($"Piton: there is no layer called \"{ThrownRopeLayerName}\". Add it in Project Settings > Tags and Layers, " +
                          "and turn off its collisions with characters in the Physics 2D layer matrix. Until then the thrown rope " +
                          "keeps the layer of its template.");
                }
            }

            base.Start();

            ownColliders = GetComponentsInChildren<Collider2D>();
            BuildCarriedRope();

            started = true;
        }

        // The rope and the head are objects of their own in the scene, as they can't be a part of him: he moves and is scaled
        // in ways they shouldn't be. So they follow him in and out of the game instead. Taking him out takes his rope and
        // head with him, and puts him back as he starts, with his head on, so nothing is left floating about.
        void OnDisable()
        {
            DestroyCarriedRope();
            DestroyRope();
            DestroyRopeVisual();

            if (headBody)
            {
                Destroy(headBody.gameObject);
                headBody = null;
            }

            aiming = false;
            HideAim();

            state = HeadState.Home;
            canMove = true;
        }

        // Putting him back gives him back the rope he carries. The first time is left to Start.
        void OnEnable()
        {
            if (started)
            {
                BuildCarriedRope();
            }
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            // Moved by him and nothing else, so it goes wherever he does, and the rope can't take him anywhere.
            if (ropeAnchor)
            {
                ropeAnchor.MovePosition(NeckPosition);
            }

            if (carriedStartAnchor && carriedEndAnchor)
            {
                carriedStartAnchor.MovePosition(carriedRopeStart.position);
                carriedEndAnchor.MovePosition(carriedRopeEnd.position);
            }

            switch (state)
            {
                case HeadState.Flying:
                    CheckFlight();
                    break;

                case HeadState.Returning:
                    ReturnHead();
                    break;
            }
        }

        // Once the head is thrown he is just his feet.
        private void LateUpdate()
        {
            if (state != HeadState.Home && feet)
            {
                spriteRenderer.sprite = feet;
            }

            UpdateRopeVisual();
            UpdateRopeLine();
            UpdateCarriedLine();
            UpdateAimArrow();
        }

        void OnDestroy()
        {
            if (aimArrowMaterial)
            {
                Destroy(aimArrowMaterial);
            }

            DestroyRope();
            DestroyRopeVisual();
            DestroyCarriedRope();

            if (headBody)
            {
                Destroy(headBody.gameObject);
            }
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            // Without his head he can only move as much as he's allowed to, which is not at all if that's none.
            if (state != HeadState.Home)
            {
                if (canMove)
                {
                    Move(directionValue);

                    var fastest = currentMovementSettings.maxMoveSpeed * MovementAfterThrow;
                    moveSpeed = Mathf.Clamp(moveSpeed, -fastest, fastest);
                }

                return;
            }

            if (aiming)
            {
                MoveAim(directionValue);
                return;
            }

            if (canMove)
            {
                Move(directionValue);
            }
        }

        // Called every frame with whether the button is held, so only a change is acted on.
        public override void Action(bool active)
        {
            if (active == performingAction)
            {
                return;
            }

            performingAction = active;

            if (state != HeadState.Home)
            {
                return;
            }

            if (active)
            {
                StartAim();
            }
            else if (aiming)
            {
                ReleaseAim();
            }
        }

        public override void FirstActionPress()
        {
            if (state == HeadState.Home || state == HeadState.Returning)
            {
                return;
            }

            if (Time.time - lastTapTime <= doubleTapSeconds)
            {
                lastTapTime = float.NegativeInfinity;
                Retract();
            }
            else
            {
                lastTapTime = Time.time;
            }
        }

        // An aim that was never let go of is dropped. A head that's out stays out for whoever crosses the rope.
        public override void ControlLost()
        {
            if (aiming)
            {
                aiming = false;
                performingAction = false;
                HideAim();
            }
        }

        // Nothing is shown, or thrown, until the stick is pushed to aim.
        private void StartAim()
        {
            aiming = true;
            aimMoved = false;
            lastStickStrength = 0f;
        }

        // Pushing the stick points the arrow that way, and the further out it's pushed, the harder he'll throw and the
        // longer the arrow is. Letting go of the stick leaves the arrow as it was.
        private void MoveAim(Vector2 directionValue)
        {
            var strength = Mathf.Min(directionValue.magnitude, 1f);
            var change = (strength - lastStickStrength) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastStickStrength = strength;

            if (strength < AimDeadZone || change < -StickReleaseRate)
            {
                return;
            }

            aimAngle = Mathf.Atan2(directionValue.y, directionValue.x) * Mathf.Rad2Deg;
            aimPower = Mathf.InverseLerp(AimDeadZone, 1f, strength);

            // The arrow shows up with the first push, as that's when there is something to throw.
            if (!aimMoved)
            {
                aimMoved = true;

                if (!aimArrow)
                {
                    CreateAimArrow();
                }

                if (aimArrow)
                {
                    aimArrow.enabled = true;
                }
            }
        }

        private void ReleaseAim()
        {
            aiming = false;
            HideAim();

            // Let go before aiming anywhere, and nothing is thrown, so tapping the button doesn't throw by accident.
            if (aimMoved)
            {
                Throw(AimDirection, aimPower);
            }
        }

        private Vector2 AimDirection
        {
            get { return new Vector2(Mathf.Cos(aimAngle * Mathf.Deg2Rad), Mathf.Sin(aimAngle * Mathf.Deg2Rad)); }
        }

        private void HideAim()
        {
            if (aimArrow)
            {
                aimArrow.enabled = false;
            }
        }

        private void CreateAimArrow()
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                print("Piton can't find the shader to draw his aim arrow with!");
                return;
            }

            var arrowObject = new GameObject("Aim Arrow");
            arrowObject.transform.parent = transform;

            aimArrowMaterial = new Material(shader);

            aimArrow = arrowObject.AddComponent<LineRenderer>();
            aimArrow.useWorldSpace = true;
            aimArrow.sharedMaterial = aimArrowMaterial;
            aimArrow.startColor = aimArrowColor;
            aimArrow.endColor = aimArrowColor;
            aimArrow.startWidth = AimArrowWidth;
            aimArrow.endWidth = AimArrowWidth;
            aimArrow.numCornerVertices = 2;
            aimArrow.numCapVertices = 2;
            aimArrow.sortingLayerID = spriteRenderer.sortingLayerID;
            aimArrow.sortingOrder = spriteRenderer.sortingOrder + 10;

            // The shaft to the tip, then back along one side of the head of the arrow, to the tip, and back along the other.
            aimArrow.positionCount = 5;
        }

        // An arrow from just ahead of his neck, pointing the way he'd throw and as long as he'd throw hard, which follows
        // him if he moves.
        private void UpdateAimArrow()
        {
            if (!aiming || !aimMoved || !aimArrow)
            {
                return;
            }

            var length = Mathf.Lerp(aimArrowShortLength, aimArrowLength, aimPower);

            // A short arrow keeps room for its shaft.
            var headSize = Mathf.Min(AimArrowHead, length * 0.5f);

            var direction = AimDirection;
            var across = new Vector2(-direction.y, direction.x);
            var start = NeckPosition + direction * AimArrowStart;
            var tip = start + direction * length;
            var back = tip - direction * headSize;
            var z = transform.position.z;

            aimArrow.SetPosition(0, new Vector3(start.x, start.y, z));
            aimArrow.SetPosition(1, new Vector3(tip.x, tip.y, z));
            aimArrow.SetPosition(2, new Vector3(back.x + across.x * headSize * 0.6f, back.y + across.y * headSize * 0.6f, z));
            aimArrow.SetPosition(3, new Vector3(tip.x, tip.y, z));
            aimArrow.SetPosition(4, new Vector3(back.x - across.x * headSize * 0.6f, back.y - across.y * headSize * 0.6f, z));
        }

        // The head is thrown the way he aimed, between the soft and hard throw speeds by how hard he aimed to, and
        // gravity bends its path from there.
        private void Throw(Vector2 direction, float power)
        {
            state = HeadState.Flying;
            canMove = MovementAfterThrow > 0f;
            moveSpeed = 0;
            lastTapTime = float.NegativeInfinity;

            // He doesn't slide on, off a ledge, while he can't move.
            rigidBody2D.linearVelocityX = 0;

            // The rope he carries is the one he throws now.
            DestroyCarriedRope();

            CreateRopeVisual();

            // Characters don't collide with each other, so on his layer the head passes through him and the rest.
            var headObject = new GameObject("Piton Head");
            headObject.layer = gameObject.layer;
            headObject.transform.position = NeckPosition;
            headObject.transform.localScale = spriteRenderer.transform.lossyScale;

            var headRenderer = headObject.AddComponent<SpriteRenderer>();
            headRenderer.sprite = head;
            headRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            headRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;

            var circle = headObject.AddComponent<CircleCollider2D>();
            circle.radius = Mathf.Min(head.bounds.extents.x, head.bounds.extents.y);
            headCollider = circle;

            // The head starts out inside him, and must never hit any part of him, or it would push him as it left, or
            // stick to him.
            ownColliders = GetComponentsInChildren<Collider2D>();
            foreach (var own in ownColliders)
            {
                Physics2D.IgnoreCollision(circle, own);
            }

            headBody = headObject.AddComponent<Rigidbody2D>();
            headBody.gravityScale = headGravityScale;
            headBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            headBody.angularVelocity = HeadSpin;
            headBody.linearVelocity = direction.normalized * Mathf.Lerp(softThrowSpeed, throwSpeed, power);

            StartRope();
        }

        // The head sticks to the first thing it touches. Until then it flies, with the rope paying out behind it, until
        // there's none left to pay out, and the rope stops it.
        private void CheckFlight()
        {
            var count = headBody.GetContacts(contacts);

            for (int i = 0; i < count; i++)
            {
                if (contacts[i].collider)
                {
                    Stick(contacts[i].collider);
                    return;
                }
            }

            GrowRope();
        }

        // Sticks to what it touched: held still if that stays where it is, and otherwise carried along with it, such as
        // when it's something that can be pushed. The rope that can be crossed is made then.
        private void Stick(Collider2D hit)
        {
            headBody.linearVelocity = Vector2.zero;
            headBody.angularVelocity = 0;

            var other = hit.attachedRigidbody;
            if (other && other.bodyType != RigidbodyType2D.Static)
            {
                var grip = headBody.gameObject.AddComponent<FixedJoint2D>();
                grip.connectedBody = other;
                grip.enableCollision = false;
            }
            else
            {
                headBody.bodyType = RigidbodyType2D.Static;
            }

            state = HeadState.Waiting;

            FinishRope();
        }

        private void Retract()
        {
            DestroyRope();

            // Whatever it is stuck to lets go.
            foreach (var joint in headBody.GetComponents<Joint2D>())
            {
                Destroy(joint);
            }

            // Passes through whatever it is stuck in or sitting on, on its way back.
            headBody.bodyType = RigidbodyType2D.Kinematic;
            headBody.angularVelocity = HeadSpin;
            state = HeadState.Returning;
        }

        private void ReturnHead()
        {
            var toNeck = NeckPosition - headBody.position;

            if (toNeck.magnitude <= retractSpeed * Time.fixedDeltaTime)
            {
                Destroy(headBody.gameObject);
                headBody = null;
                DestroyRopeVisual();

                state = HeadState.Home;
                canMove = true;

                BuildCarriedRope();
            }
            else
            {
                headBody.linearVelocity = toNeck.normalized * retractSpeed;
            }
        }

        // The rope is laid out behind the head as it flies: a link is added whenever the head is a link's length from the
        // end of it, so it pays out of his neck a link at a time, and hangs, and falls over whatever is in its way. The
        // links start out light and on the thrown rope layer, which doesn't touch characters. It's tied to a point that
        // follows his neck, not to him, and passes through him, so it goes where he does, but whatever the rope does, or
        // whoever crosses it, can't push or pull him.
        private void StartRope()
        {
            if (linkWidth <= 0f)
            {
                return;
            }

            ropeHolder = new GameObject("Piton Rope");
            ropeAnchor = NewAnchor(ropeHolder.transform, NeckPosition);

            ConnectTip(ropeAnchor, Vector2.zero);
            CreateRopeLine();
        }

        // A point for a rope to be tied to. It's moved by whatever sets its position and nothing else, so a rope tied to
        // it can't take it anywhere.
        private Rigidbody2D NewAnchor(Transform parent, Vector2 position)
        {
            var anchor = new GameObject("Anchor");
            anchor.transform.parent = parent;
            anchor.transform.position = position;

            var body = anchor.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;

            return body;
        }

        // The rope he carries while his head is on: links hanging in a loop between two points on his body, made just as
        // the thrown rope is, with the same weight and the same line, except that it has no colliders, so it hangs and
        // sways without catching on anything. Its ends follow him, but nothing it does can move him. It's gone while the
        // head is out, as then the rope is the thrown one.
        private void BuildCarriedRope()
        {
            if (carriedHolder || linkWidth <= 0f || !carriedRopeStart || !carriedRopeEnd || carriedLinks < 1)
            {
                return;
            }

            Vector2 start = carriedRopeStart.position;
            Vector2 end = carriedRopeEnd.position;
            var gap = Vector2.Distance(start, end);

            // Both ends are in the same place, with nothing to hang a loop between.
            if (gap < 0.01f)
            {
                return;
            }

            var length = gap * (1f + Mathf.Max(0f, carriedRopeSlack));
            ShapeRopeEvenly(start, end, length, carriedLinks);

            carriedHolder = new GameObject("Piton Carried Rope");
            carriedStartAnchor = NewAnchor(carriedHolder.transform, start);
            carriedEndAnchor = NewAnchor(carriedHolder.transform, end);

            var halfWidth = linkWidth * 0.5f;
            var back = carriedStartAnchor;
            var backPoint = Vector2.zero;

            for (int i = 0; i < ropeNodes.Count - 1; i++)
            {
                var body = CreateLink(carriedHolder.transform, $"Link {i}", ropeNodes[i], ropeNodes[i + 1], back, backPoint, false);

                carriedBodies.Add(body);
                back = body;
                backPoint = new Vector2(halfWidth, 0);
            }

            // The last link is tied on to the other point.
            var tie = back.gameObject.AddComponent<HingeJoint2D>();
            tie.autoConfigureConnectedAnchor = false;
            tie.enableCollision = false;
            tie.anchor = new Vector2(halfWidth, 0);
            tie.connectedBody = carriedEndAnchor;
            tie.connectedAnchor = Vector2.zero;

            carriedLine = CreateLine(carriedHolder.transform);
            UpdateCarriedLine();
        }

        private void UpdateCarriedLine()
        {
            if (!carriedLine || !carriedStartAnchor || !carriedEndAnchor)
            {
                return;
            }

            DrawLine(carriedLine, carriedStartAnchor.position, carriedBodies, carriedEndAnchor.position);
        }

        private void DestroyCarriedRope()
        {
            if (carriedHolder)
            {
                Destroy(carriedHolder);
            }

            carriedHolder = null;
            carriedStartAnchor = null;
            carriedEndAnchor = null;
            carriedLine = null;
            carriedBodies.Clear();
        }

        // The head is held to the end of the rope, but with up to a link's length of give, so it can fly on until it's a
        // whole link away and a link is added to fill the gap.
        private void ConnectTip(Rigidbody2D end, Vector2 endPoint)
        {
            if (tipJoint)
            {
                Destroy(tipJoint);
            }

            tipJoint = headBody.gameObject.AddComponent<DistanceJoint2D>();
            tipJoint.autoConfigureConnectedAnchor = false;
            tipJoint.autoConfigureDistance = false;
            tipJoint.connectedBody = end;
            tipJoint.connectedAnchor = endPoint;
            tipJoint.maxDistanceOnly = true;
            tipJoint.distance = linkWidth * linkTemplate.transform.lossyScale.x;
            tipJoint.enableCollision = false;
        }

        // Where the end of the last link is, which is where the rope has got to.
        private Vector2 RopeEnd()
        {
            if (ropeLinks.Count == 0)
            {
                return ropeAnchor.position;
            }

            return ropeLinks[ropeLinks.Count - 1].transform.TransformPoint(new Vector3(linkWidth * 0.5f, 0, 0));
        }

        // Adds a link when the head has gone about a link's length from the end of the rope. The last one is left for when
        // the head sticks, or the rope would be longer than the most links it's allowed.
        private void GrowRope()
        {
            if (!ropeHolder || ropeLinks.Count >= maxLinks - 1)
            {
                return;
            }

            var end = RopeEnd();
            var fullLink = linkWidth * linkTemplate.transform.lossyScale.x;

            if (Vector2.Distance(end, headBody.position) >= fullLink * LinkAddFraction)
            {
                AddLink(end, headBody.position);
            }
        }

        // A link from one point to another, hinged on to the end of the rope: the point at his neck for the first, and the
        // end of the one before for the rest.
        private void AddLink(Vector2 from, Vector2 to)
        {
            var halfWidth = linkWidth * 0.5f;

            var back = ropeAnchor;
            var backPoint = Vector2.zero;

            if (ropeLinks.Count > 0)
            {
                back = ropeLinks[ropeLinks.Count - 1];
                backPoint = new Vector2(halfWidth, 0);
            }

            var body = CreateLink(ropeHolder.transform, $"Link {ropeLinks.Count}", from, to, back, backPoint, true);

            ropeLinks.Add(body);
            ConnectTip(body, new Vector2(halfWidth, 0));
        }

        // A link of the rope, spanning exactly from one point to another so its back is where the rope it's joined to
        // ends, with its back hinged to a point on something. It's light and loose, on the thrown rope layer, and
        // doesn't touch him or the head, if there is one. If it's not solid, it has no colliders and touches nothing at
        // all, so it's just something to look at.
        private Rigidbody2D CreateLink(
            Transform parent, string linkName, Vector2 from, Vector2 to, Rigidbody2D back, Vector2 backPoint, bool solid)
        {
            var along = to - from;
            var scale = linkTemplate.transform.lossyScale;

            var link = Instantiate(linkTemplate, parent);
            link.name = linkName;
            link.SetActive(false);

            // A template copied from a link that was already in a rope brings its joints along.
            foreach (var joint in link.GetComponents<Joint2D>())
            {
                DestroyImmediate(joint);
            }

            if (!solid)
            {
                foreach (var linkCollider in link.GetComponents<Collider2D>())
                {
                    DestroyImmediate(linkCollider);
                }
            }

            link.transform.position = (from + to) * 0.5f;
            link.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            link.transform.localScale = new Vector3(along.magnitude / linkWidth, scale.y, scale.z);

            if (thrownRopeLayer >= 0)
            {
                link.layer = thrownRopeLayer;
            }

            var body = link.GetComponent<Rigidbody2D>();
            if (!body)
            {
                body = link.AddComponent<Rigidbody2D>();
            }

            if (solid)
            {
                SetLinkPhysics(body, false);
            }
            else
            {
                body.mass = carriedLinkMass;
                body.linearDamping = carriedLinkLinearDamping;
                body.angularDamping = carriedLinkAngularDamping;
                body.gravityScale = carriedLinkGravityScale;
            }

            var hinge = link.AddComponent<HingeJoint2D>();
            hinge.autoConfigureConnectedAnchor = false;
            hinge.enableCollision = false;
            hinge.anchor = new Vector2(linkWidth * -0.5f, 0);
            hinge.connectedBody = back;
            hinge.connectedAnchor = backPoint;

            link.SetActive(true);

            // Whatever colliders it has are told to leave him and the head alone.
            foreach (var linkCollider in link.GetComponents<Collider2D>())
            {
                if (headCollider)
                {
                    Physics2D.IgnoreCollision(linkCollider, headCollider);
                }

                foreach (var own in ownColliders)
                {
                    Physics2D.IgnoreCollision(linkCollider, own);
                }
            }

            return body;
        }

        // The head has stuck. The rest of the way to it is filled with a last link, and the head is tied on. The links
        // are made firm and heavy, to be a bridge, and moved to the layer that can be stood on.
        private void FinishRope()
        {
            if (!ropeHolder)
            {
                return;
            }

            var end = RopeEnd();
            var fullLink = linkWidth * linkTemplate.transform.lossyScale.x;

            // What's left, if it's too short to hinge, is just where the head is tied on.
            if (Vector2.Distance(end, headBody.position) >= fullLink * ShortestLastLink)
            {
                AddLink(end, headBody.position);
            }

            if (tipJoint)
            {
                Destroy(tipJoint);
            }

            foreach (var link in ropeLinks)
            {
                SetLinkPhysics(link, true);

                if (placedRopeLayer >= 0)
                {
                    link.gameObject.layer = placedRopeLayer;
                }
            }

            if (ropeLinks.Count == 0)
            {
                return;
            }

            // Tied on right where the head is now, so that nothing is pulled.
            var last = ropeLinks[ropeLinks.Count - 1];
            var tie = last.gameObject.AddComponent<HingeJoint2D>();
            tie.autoConfigureConnectedAnchor = false;
            tie.enableCollision = false;
            tie.anchor = new Vector2(linkWidth * 0.5f, 0);
            tie.connectedBody = headBody;
            tie.connectedAnchor = headBody.transform.InverseTransformPoint(RopeEnd());
        }

        private void SetLinkPhysics(Rigidbody2D link, bool firm)
        {
            link.mass = firm ? linkMass : looseLinkMass;
            link.linearDamping = firm ? linkLinearDamping : looseLinkLinearDamping;
            link.gravityScale = firm ? linkGravityScale : looseLinkGravityScale;
        }

        // A line along every link and across the gaps between them, drawn behind the links in the same material, so a
        // rope that has stretched still looks like one piece.
        private void CreateRopeLine()
        {
            ropeLine = CreateLine(ropeHolder.transform);
            UpdateRopeLine();
        }

        private void UpdateRopeLine()
        {
            if (!ropeLine || headBody == null || !ropeAnchor)
            {
                return;
            }

            DrawLine(ropeLine, ropeAnchor.position, ropeLinks, headBody.position);
        }

        // A line for a rope, in the colour and thickness set for it, drawn behind its links in the same material. Null if
        // the template has no sprite to take the material from.
        private LineRenderer CreateLine(Transform parent)
        {
            var source = linkTemplate.GetComponent<SpriteRenderer>();
            if (!source)
            {
                return null;
            }

            var lineObject = new GameObject("Line");
            lineObject.transform.parent = parent;

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = source.sharedMaterial;
            line.startColor = ropeLineColor;
            line.endColor = ropeLineColor;
            line.startWidth = ropeLineWidth;
            line.endWidth = ropeLineWidth;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sortingLayerID = source.sortingLayerID;
            line.sortingOrder = source.sortingOrder - 1;

            return line;
        }

        // Along every link and across the gaps between them, from the point the rope is tied on at to where it ends, so
        // a rope that has stretched still looks like one piece.
        private void DrawLine(LineRenderer line, Vector2 first, List<Rigidbody2D> links, Vector2 last)
        {
            var halfWidth = linkWidth * 0.5f;
            var z = linkTemplate.transform.position.z;

            // Where it's tied on, then both ends of each link, then where the rope ends.
            line.positionCount = links.Count * 2 + 2;
            line.SetPosition(0, new Vector3(first.x, first.y, z));

            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i].transform;
                var back = link.TransformPoint(new Vector3(-halfWidth, 0, 0));
                var front = link.TransformPoint(new Vector3(halfWidth, 0, 0));

                line.SetPosition(i * 2 + 1, new Vector3(back.x, back.y, z));
                line.SetPosition(i * 2 + 2, new Vector3(front.x, front.y, z));
            }

            line.SetPosition(links.Count * 2 + 1, new Vector3(last.x, last.y, z));
        }

        // How wide one link of the template is before it's scaled.
        private float LinkWidth()
        {
            var sprite = linkTemplate.GetComponent<SpriteRenderer>();
            if (sprite && sprite.sprite)
            {
                return sprite.sprite.bounds.size.x;
            }

            var box = linkTemplate.GetComponent<BoxCollider2D>();
            return box ? box.size.x : 0f;
        }

        // The rope as it hangs from his neck to his head, a little longer than the gap between them, as full links with
        // a shorter one on the end to reach the head. It grows a link at a time the further the head gets. Null if there
        // is no rope to be made, and otherwise a list that is reused, so it's only good until the next call.
        private List<Vector2> RopeNodes()
        {
            if (linkWidth <= 0f)
            {
                return null;
            }

            var start = NeckPosition;
            var end = headBody.position;
            var distance = Vector2.Distance(start, end);

            // The head is on top of him, with nothing to string a rope across.
            if (distance < 0.05f)
            {
                return null;
            }

            ShapeRope(start, end, distance * (1f + Mathf.Max(0f, ropeSlack)), linkWidth * linkTemplate.transform.lossyScale.x);

            return ropeNodes;
        }

        // Puts the points that are the ends of the links of a rope of that length hanging between two points: a shallow
        // curve sagging below the line between them, with a point every linkLength along it and one at the end.
        private void ShapeRope(Vector2 start, Vector2 end, float length, float linkLength)
        {
            var span = end - start;
            var distance = span.magnitude;

            // Across the gap, on the downward side, which is the way it sags.
            var sag = new Vector2(-span.y, span.x) / distance;
            if (sag.y > 0)
            {
                sag = -sag;
            }

            // How deep a shallow curve of that length is: its length is about distance + 8 * depth^2 / (3 * distance).
            var depth = Mathf.Sqrt(Mathf.Max(0f, 3f * distance * (length - distance) / 8f));

            ropeNodes.Clear();
            ropeNodes.Add(start);

            // Walks along the curve in small steps, and puts a point down each time a link's length is covered.
            var steps = Mathf.Max(StepsPerLink, Mathf.CeilToInt(length / linkLength) * StepsPerLink);
            for (int step = 1; step <= steps; step++)
            {
                var t = (float)step / steps;
                var point = Vector2.Lerp(start, end, t) + sag * (depth * 4f * t * (1f - t));

                if (Vector2.Distance(ropeNodes[ropeNodes.Count - 1], point) >= linkLength)
                {
                    ropeNodes.Add(point);
                }
            }

            // What's left over to the end is the last link, which is a part of one unless it came out even.
            if (Vector2.Distance(ropeNodes[ropeNodes.Count - 1], end) > 0.001f)
            {
                ropeNodes.Add(end);
            }
            else
            {
                ropeNodes[ropeNodes.Count - 1] = end;
            }
        }

        // Puts exactly count + 1 points hanging between two points, evenly spaced along a curve of that length that sags
        // below the line between them, so a rope made from them has exactly count links.
        private void ShapeRopeEvenly(Vector2 start, Vector2 end, float length, int count)
        {
            var span = end - start;
            var distance = span.magnitude;

            var sag = new Vector2(-span.y, span.x) / distance;
            if (sag.y > 0)
            {
                sag = -sag;
            }

            var depth = Mathf.Sqrt(Mathf.Max(0f, 3f * distance * (length - distance) / 8f));
            var steps = count * StepsPerLink;

            // How far along the curve it is, first in all, and then in each step.
            var total = 0f;
            var previous = start;
            for (int step = 1; step <= steps; step++)
            {
                var point = HangingPoint(start, end, sag, depth, (float)step / steps);
                total += Vector2.Distance(previous, point);
                previous = point;
            }

            ropeNodes.Clear();
            ropeNodes.Add(start);

            var travelled = 0f;
            previous = start;
            for (int step = 1; step <= steps && ropeNodes.Count < count; step++)
            {
                var point = HangingPoint(start, end, sag, depth, (float)step / steps);
                travelled += Vector2.Distance(previous, point);
                previous = point;

                if (travelled >= total * ropeNodes.Count / count)
                {
                    ropeNodes.Add(point);
                }
            }

            // Any that rounding left out are spread over what's left, and the last is always the end.
            var missing = count - ropeNodes.Count;
            var last = ropeNodes[ropeNodes.Count - 1];
            for (int i = 1; i <= missing; i++)
            {
                ropeNodes.Add(Vector2.Lerp(last, end, (float)i / (missing + 1)));
            }

            ropeNodes.Add(end);
        }

        private static Vector2 HangingPoint(Vector2 start, Vector2 end, Vector2 sag, float depth, float t)
        {
            return Vector2.Lerp(start, end, t) + sag * (depth * 4f * t * (1f - t));
        }

        private void CreateRopeVisual()
        {
            var source = linkTemplate ? linkTemplate.GetComponent<SpriteRenderer>() : null;
            if (!source || !source.sprite)
            {
                return;
            }

            ropeVisual = new GameObject("Piton Rope Visual");
        }

        private Transform NewVisualLink()
        {
            var source = linkTemplate.GetComponent<SpriteRenderer>();

            var link = new GameObject($"Link {visualLinks.Count}");
            link.transform.parent = ropeVisual.transform;

            var linkRenderer = link.AddComponent<SpriteRenderer>();
            linkRenderer.sprite = source.sprite;
            linkRenderer.sharedMaterial = source.sharedMaterial;
            linkRenderer.color = source.color;
            linkRenderer.sortingLayerID = source.sortingLayerID;
            linkRenderer.sortingOrder = source.sortingOrder;

            return link.transform;
        }

        // Draws the rope, in links laid out the way the solid rope will be, whenever the head is out but there is no solid
        // rope: while it flies, and on its way back.
        private void UpdateRopeVisual()
        {
            if (!ropeVisual)
            {
                return;
            }

            var shape = state != HeadState.Home && !ropeHolder && headBody != null ? RopeNodes() : null;
            var count = shape == null ? 0 : shape.Count - 1;

            while (visualLinks.Count < count)
            {
                visualLinks.Add(NewVisualLink());
            }

            var scale = linkTemplate.transform.lossyScale;
            var z = linkTemplate.transform.position.z;

            for (int i = 0; i < visualLinks.Count; i++)
            {
                visualLinks[i].gameObject.SetActive(i < count);

                if (i >= count)
                {
                    continue;
                }

                var along = shape[i + 1] - shape[i];
                var middle = (shape[i] + shape[i + 1]) * 0.5f;

                visualLinks[i].position = new Vector3(middle.x, middle.y, z);
                visualLinks[i].rotation = Quaternion.Euler(0, 0, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
                visualLinks[i].localScale = new Vector3(along.magnitude / linkWidth, scale.y, 1f);
            }
        }

        private void DestroyRopeVisual()
        {
            if (ropeVisual)
            {
                Destroy(ropeVisual);
            }

            ropeVisual = null;
            visualLinks.Clear();
        }

        private void DestroyRope()
        {
            if (ropeHolder)
            {
                Destroy(ropeHolder);
                ropeHolder = null;
            }

            ropeLinks.Clear();
            ropeLine = null;
            ropeAnchor = null;
            tipJoint = null;
        }
    }
}
