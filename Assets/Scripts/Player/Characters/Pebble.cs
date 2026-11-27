using UnityEngine;

namespace Characters
{
    public class Pebble : Character
    {
        // ----- Animation -----
        //
        // He has no action of his own, but he's quick and nimble, so at the top of a big jump he rolls: tucks in, spins once, and opens out as he comes
        // down. That's his own state, Roll. A short hop, or a jump let go of early, or a fall off a ledge, doesn't roll. See Docs/Animation.md.
        public const string RollState = "Roll";

        // Running flat out for a while is another of his own states, Sprint: hands in the air and a big smile, because he loves it. It's his run, with
        // that on top, so it goes as fast as he does.
        public const string SprintState = "Sprint";

        [Tooltip("How long, in seconds, he has to have been running at his top speed before he puts his hands up and smiles.")]
        public float sprintAfterSeconds = 1f;

        [Header("Rolling in the air")]
        [Tooltip("How fast, in units a second, he has to have been going up at some point in a jump for him to roll at the top of it. A bigger jump rolls; " +
                 "a small hop doesn't.")]
        public float rollMinJumpSpeed = 8f;

        [Tooltip("He starts to roll when he's going up slower than this, in units a second, which is just before the top.")]
        public float rollStartSpeed = 1.5f;

        // How long the Roll clip takes: its ten pictures, at 20 a second.
        private const float RollSeconds = 0.5f;

        private float rollUntil;
        private float peakRise;
        private bool rolled;

        // When he got to his top speed, or nothing while he isn't at it, and whether he's been there long enough.
        private float atTopSpeedSince = -1f;
        private bool sprinting;

        // At least this much of his top speed counts as being at it, and he has to drop below this much of it to be out of it, so being a touch
        // short of it for a moment doesn't start him over.
        private const float TopSpeedFraction = 0.97f;
        private const float OffTopSpeedFraction = 0.9f;

        protected override void Update()
        {
            // Before his picture is chosen.
            UpdateRoll();
            UpdateSprint();

            base.Update();
        }

        private void UpdateRoll()
        {
            if (!isJumping)
            {
                rolled = false;
                peakRise = 0f;
                return;
            }

            var rising = rigidBody2D.linearVelocity.y;
            peakRise = Mathf.Max(peakRise, rising);

            if (!rolled && CarriedObject == null && peakRise >= rollMinJumpSpeed && rising < rollStartSpeed)
            {
                rolled = true;
                rollUntil = Time.time + RollSeconds;
            }
        }

        private void UpdateSprint()
        {
            var fastest = MaxWalkSpeed;
            var speed = Mathf.Abs(moveSpeed);

            if (fastest > 0.01f && speed >= fastest * TopSpeedFraction)
            {
                if (atTopSpeedSince < 0f)
                {
                    atTopSpeedSince = Time.time;
                }

                sprinting = Time.time - atTopSpeedSince >= sprintAfterSeconds;
            }
            else if (fastest <= 0.01f || speed < fastest * OffTopSpeedFraction)
            {
                atTopSpeedSince = -1f;
                sprinting = false;
            }
        }

        protected override string AnimationState(string commonState)
        {
            if (sprinting && commonState == CharacterAnimationSet.Walk && CarriedObject == null)
            {
                return SprintState;
            }

            if (Time.time < rollUntil && (commonState == CharacterAnimationSet.Rise || commonState == CharacterAnimationSet.Fall))
            {
                return RollState;
            }

            return commonState;
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }
    }
}
