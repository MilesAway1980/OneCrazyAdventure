using UnityEngine;
using UnityEngine.Serialization;

namespace Characters
{
    // What the Bulb's light was last told to do, for the other machines in a networked game, which are told to do the same. The
    // revision goes up each time, so that being told the same thing again still counts as being told.
    public struct BulbLightCall
    {
        public int revision;

        public bool lit;

        // Seconds to ease to lit or unlit this once, or below 0 for the usual.
        public float litSeconds;

        public bool hasBoost;
        public float boostRadius;
        public float boostIntensity;
        public float boostSeconds;
    }

    // Glows normally, and brighter while the action is held. He stays as bright as he was left if the player moves to
    // another character, as characters are meant to work together.
    //
    // Standing at a LightSocket, holding Interact and then pressing the action screws him into it. He turns as he goes
    // in, and sits there, held still, with his light made far larger by the socket. While Interact is held the action
    // doesn't light him: he is only lit by being screwed in. He stays that way, even if the player moves to another
    // character, until he is played again and the action is pressed, which screws him out.
    public class Bulb : Character, IKeepsRunningWhenShownOnly
    {
        [Tooltip("The bulb's light. Its unlit look is the normal glow, and its lit look is the brighter glow while the action is held.")]
        [FormerlySerializedAs("useBrightness")]
        [FormerlySerializedAs("brightTileSource")]
        [FormerlySerializedAs("lightTileSource")]
        public LightSource lightSource;

        [Header("Light Sockets")]
        [Tooltip("Seconds he takes to screw himself into a socket, and the same to screw himself out of it.")]
        public float screwSeconds = 0.8f;

        [Tooltip("How many times he turns right round as he screws in or out.")]
        public float screwTurns = 3f;

        [Tooltip("How far, in units, he sinks into the socket as he's screwed in, below where he sits in it. He rises the same, and a little more, to come out.")]
        public float screwDepth = 0.1f;

        private enum State
        {
            Free,
            Screwing,
            Socketed,
            Unscrewing
        }

        // How far above where he sits, in units, he's lifted as he is unscrewed, before he is let go.
        private const float LiftOut = 0.2f;

        private State state;
        private LightSocket socket;
        private Collider2D bodyCollider;

        private bool interactHeld;
        private bool actionHeld;

        // Set when the action goes down while Interact is held, until the action is let go. That press is for the socket,
        // so it doesn't light him.
        private bool lightSuppressed;

        // How far through screwing in or out he is, from 0 to 1.
        private float progress;
        private Vector2 startPosition;
        private Vector2 seatedPosition;
        private RigidbodyType2D bodyTypeBefore;

        // His picture, which is what turns, and how it sat before.
        private Transform picture;
        private Vector3 pictureScale;

        // ----- Animation -----
        //
        // Lit is his Action (standing), and AirAction in the air: the filament flickering bright. Walking while lit is his own state, ActionWalk, which is
        // his walk with the bright filament. Screwed into a socket, he's lit as well, so he shows the lit one. A set without an ActionWalk clip shows
        // the standing lit one while he walks. See Docs/Animation.md.
        public const string ActionWalkState = "ActionWalk";

        protected override string AnimationState(string commonState)
        {
            if (commonState == CharacterAnimationSet.Action && WalkingForAnimation && HasAnimationClip(ActionWalkState))
            {
                return ActionWalkState;
            }

            return commonState;
        }

        protected override void Start()
        {
            if (!lightSource)
            {
                lightSource = GetComponent<LightSource>();
            }

            if (lightSource)
            {
                LightLit(false);
            }
            else
            {
                Debug.LogWarning($"{name}: the Bulb has no LightSource, so it has no light to brighten.", this);
            }

            base.Start();

            bodyCollider = GetComponent<Collider2D>();

            // Only the picture turns as he screws in. If it's on the same object as his body, that would turn the physics as
            // well, so it is left alone.
            if (spriteRenderer && spriteRenderer.transform != transform)
            {
                picture = spriteRenderer.transform;
                pictureScale = picture.localScale;
            }
        }

        // ----- Telling the light, and the other machines -----

        private BulbLightCall lightCall;

        // What the light was last told, which another machine tells its light too.
        public BulbLightCall LastLightCall
        {
            get { return lightCall; }
        }

        private void LightLit(bool isLit)
        {
            if (lightSource)
            {
                lightSource.SetLit(isLit);
            }

            lightCall.lit = isLit;
            lightCall.litSeconds = -1f;
            lightCall.revision++;
        }

        private void LightLit(bool isLit, float seconds)
        {
            if (lightSource)
            {
                lightSource.SetLit(isLit, seconds);
            }

            lightCall.lit = isLit;
            lightCall.litSeconds = seconds;
            lightCall.revision++;
        }

        private void LightBoost(float radiusMultiplier, float intensityMultiplier, float seconds)
        {
            if (lightSource)
            {
                lightSource.SetBoost(radiusMultiplier, intensityMultiplier, seconds);
            }

            lightCall.hasBoost = true;
            lightCall.boostRadius = radiusMultiplier;
            lightCall.boostIntensity = intensityMultiplier;
            lightCall.boostSeconds = seconds;
            lightCall.revision++;
        }

        // ----- On a machine that has joined someone else's game -----
        //
        // The host does everything the Bulb does, including going into a socket. This machine is sent where he is, which of the four
        // states he is in, and what his light was told, and does the rest of what it did before: his light changes as it would, and
        // his picture turns as he screws in and out.

        // Which of the states he is in.
        public byte StateForNetwork
        {
            get { return (byte)state; }
        }

        private State shownState;
        private float shownSince;

        public void ShowState(byte newState)
        {
            var shown = (State)newState;

            if (shown != shownState)
            {
                shownState = shown;
                shownSince = Time.time;
            }
        }

        // Tells his light what the host told it.
        public void ShowLight(BulbLightCall call)
        {
            if (!lightSource || call.revision == 0)
            {
                return;
            }

            if (call.litSeconds >= 0f)
            {
                lightSource.SetLit(call.lit, call.litSeconds);
            }
            else
            {
                lightSource.SetLit(call.lit);
            }

            if (call.hasBoost)
            {
                lightSource.SetBoost(call.boostRadius, call.boostIntensity, call.boostSeconds);
            }
        }

        // He turns for as long as the host says he's screwing in or out, the same time as there.
        private void UpdateShown()
        {
            var progress = Mathf.Min(1f, (Time.time - shownSince) / Mathf.Max(screwSeconds, 0.01f));

            switch (shownState)
            {
                case State.Screwing:
                    Turn(progress);
                    break;

                case State.Unscrewing:
                    Turn(1f - progress);
                    break;

                case State.Socketed:
                    Turn(1f);
                    break;

                default:
                    Turn(0f);
                    break;
            }
        }

        protected override void Update()
        {
            if (ShownOnly)
            {
                UpdateShown();
                return;
            }

            base.Update();

            switch (state)
            {
                case State.Screwing:
                    UpdateScrewing();
                    break;

                case State.Socketed:
                    // A socket that has gone, with him in it, lets him go.
                    if (!socket)
                    {
                        FinishUnscrewing();
                    }
                    else
                    {
                        SetPosition(seatedPosition + Vector2.down * screwDepth);
                    }

                    break;

                case State.Unscrewing:
                    UpdateUnscrewing();
                    break;
            }
        }

        protected override void FixedUpdate()
        {
            if (ShownOnly)
            {
                return;
            }

            base.FixedUpdate();

            // He's moved by hand while he's in a socket, and the base class's gravity would otherwise carry him off.
            if (state != State.Free)
            {
                rigidBody2D.linearVelocity = Vector2.zero;
            }
        }

        // Taken out of the game while in a socket, he lets go of it so that it isn't left holding him.
        void OnDisable()
        {
            if (state != State.Free)
            {
                FinishUnscrewing();
            }
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }

        public override void InteractHold(bool held)
        {
            base.InteractHold(held);
            interactHeld = held;
        }

        // Interact does nothing while he is in a socket or going into or out of one, as it would try to pick up or work
        // whatever is near.
        public override void Interact()
        {
            if (state == State.Free)
            {
                base.Interact();
            }
        }

        // Called once each time the action goes down. In a socket it unscrews him. With Interact held, it screws him into a
        // socket he is at, if there is one, and either way doesn't light him.
        public override void FirstActionPress()
        {
            if (state == State.Socketed)
            {
                StartUnscrewing();
                return;
            }

            if (state != State.Free)
            {
                return;
            }

            lightSuppressed = interactHeld;

            if (interactHeld)
            {
                var found = FindSocket();

                if (found)
                {
                    StartScrewing(found);
                }
            }
        }

        // Called every frame with whether the button is held, so the light is only told when that changes. While he is
        // screwed in, or going in or out, the light is the socket's, and the button only decides how he is once he's free.
        public override void Action(bool active)
        {
            actionHeld = active;

            if (!active)
            {
                lightSuppressed = false;
            }

            if (state != State.Free)
            {
                return;
            }

            var lit = active && !lightSuppressed;

            if (lit == performingAction)
            {
                return;
            }

            performingAction = lit;

            if (lightSource)
            {
                LightLit(lit);
            }
        }

        // The player has moved on to another character, so the buttons they were holding will never be seen let go. He
        // stays screwed in.
        public override void ControlLost()
        {
            actionHeld = false;
            interactHeld = false;
            lightSuppressed = false;
        }

        // The nearest free socket he is at, or null.
        private LightSocket FindSocket()
        {
            if (!bodyCollider)
            {
                return null;
            }

            var bounds = bodyCollider.bounds;
            LightSocket nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var candidate in LightSocket.Active)
            {
                if (!candidate || !candidate.IsFree || !candidate.Reaches(bounds))
                {
                    continue;
                }

                var distance = Vector2.Distance(candidate.SeatFor(bounds), bounds.center);

                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void StartScrewing(LightSocket target)
        {
            if (!target.TryOccupy(this))
            {
                return;
            }

            socket = target;
            state = State.Screwing;
            progress = 0f;
            startPosition = rigidBody2D.position;

            // The socket says where the middle of him goes, and what is moved is where his body is anchored, which may not
            // be the middle of his collider.
            var bounds = bodyCollider.bounds;
            seatedPosition = socket.SeatFor(bounds) - ((Vector2)bounds.center - rigidBody2D.position);

            // He's held by hand while he is in it, so the physics leaves him alone.
            bodyTypeBefore = rigidBody2D.bodyType;
            rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
            rigidBody2D.linearVelocity = Vector2.zero;
            moveSpeed = 0f;

            // He isn't lit while he is going in: his light waits until he is all the way in.
            canMove = false;
            performingAction = false;

            if (lightSource)
            {
                LightLit(false);
            }
        }

        // All the way in. His light eases from how he glows when he's not lit up to the full brightness of the socket, over
        // the socket's time.
        private void FinishScrewing()
        {
            state = State.Socketed;
            performingAction = true;

            if (lightSource)
            {
                LightLit(true, socket.boostSeconds);
                LightBoost(socket.radiusMultiplier, socket.intensityMultiplier, socket.boostSeconds);
            }
        }

        private void UpdateScrewing()
        {
            if (!socket)
            {
                FinishUnscrewing();
                return;
            }

            progress = Mathf.Min(1f, progress + Time.deltaTime / Mathf.Max(screwSeconds, 0.01f));

            // Slides over to the socket and down into it, turning as he goes.
            var ease = Mathf.SmoothStep(0f, 1f, progress);
            SetPosition(Vector2.Lerp(startPosition, seatedPosition + Vector2.down * screwDepth, ease));
            Turn(progress);

            if (progress >= 1f)
            {
                FinishScrewing();
            }
        }

        private void StartUnscrewing()
        {
            if (!socket)
            {
                FinishUnscrewing();
                return;
            }

            state = State.Unscrewing;
            progress = 0f;

            // He's no longer lit as soon as he starts to come out: his light eases back to how he glows when he's not lit
            // up, over the socket's time.
            performingAction = false;

            if (lightSource)
            {
                LightLit(false, socket.boostSeconds);
                LightBoost(1f, 1f, socket.boostSeconds);
            }
        }

        private void UpdateUnscrewing()
        {
            progress = Mathf.Min(1f, progress + Time.deltaTime / Mathf.Max(screwSeconds, 0.01f));

            // Turns the other way, and rises up out of it.
            var ease = Mathf.SmoothStep(0f, 1f, progress);
            var from = seatedPosition + Vector2.down * screwDepth;
            var to = seatedPosition + Vector2.up * LiftOut;

            SetPosition(Vector2.Lerp(from, to, ease));
            Turn(1f - progress);

            if (progress >= 1f)
            {
                FinishUnscrewing();
            }
        }

        // Lets go of the socket and is free again, with the light as the button says it should be.
        private void FinishUnscrewing()
        {
            // His light settles back, over the socket's time, from wherever it has got to.
            var settleSeconds = socket ? socket.boostSeconds : 0.3f;

            if (socket)
            {
                socket.Release(this);
                socket = null;
            }

            if (state != State.Free)
            {
                rigidBody2D.bodyType = bodyTypeBefore;
                rigidBody2D.linearVelocity = Vector2.zero;
            }

            state = State.Free;
            canMove = true;
            Turn(0f);

            var lit = actionHeld && !lightSuppressed;

            performingAction = lit;

            if (lightSource)
            {
                LightLit(lit);
                LightBoost(1f, 1f, settleSeconds);
            }
        }

        // Turns his picture about, as if he were being screwed round: it's squeezed in and flipped across as he goes round,
        // 0 being upright and 1 being all the turns done.
        private void Turn(float amount)
        {
            if (!picture)
            {
                return;
            }

            var scale = pictureScale;

            scale.x = pictureScale.x * Mathf.Cos(amount * screwTurns * 2f * Mathf.PI);
            picture.localScale = scale;
        }
    }
}
