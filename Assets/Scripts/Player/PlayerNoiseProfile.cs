using Components;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Turns how the player is moving into how loud their footsteps are.
    ///
    /// <para>This is where the stealth loop actually lives: crouching buys you silence at the
    /// cost of speed, sprinting buys you speed at the cost of being heard across the floor.
    /// Keeping the mapping here means <see cref="FootstepEmitter"/> never has to know that
    /// crouching or sprinting exist.</para>
    /// </summary>
    public class PlayerNoiseProfile : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private FootstepEmitter footstepEmitter;
        [SerializeField] private PlayerCrouch playerCrouch;
        [SerializeField] private PlayerRun playerRun;

        [Header("Loudness multipliers")]
        [Tooltip("Crouched. Below ~0.4 the monster effectively cannot hear you walk.")]
        [SerializeField, Min(0f)] private float crouchingMultiplier = 0.3f;

        [SerializeField, Min(0f)] private float walkingMultiplier = 1f;

        [Tooltip("Sprinting. Above ~1.5 a sprint across a room is a reliable way to get found.")]
        [SerializeField, Min(0f)] private float runningMultiplier = 1.8f;

        private bool _isCrouching;
        private bool _isRunning;

        public override void OnNetworkSpawn()
        {
            // Movement events only fire on the owner, and the multiplier only matters there —
            // it travels to the server attached to each footstep.
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            if (playerCrouch != null) playerCrouch.OnCrouchEvent += PlayerCrouch_OnCrouchEvent;
            if (playerRun != null) playerRun.OnRunEvent += PlayerRun_OnRunEvent;

            Apply();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            if (playerCrouch != null) playerCrouch.OnCrouchEvent -= PlayerCrouch_OnCrouchEvent;
            if (playerRun != null) playerRun.OnRunEvent -= PlayerRun_OnRunEvent;
        }

        private void PlayerCrouch_OnCrouchEvent(bool isCrouching)
        {
            _isCrouching = isCrouching;
            Apply();
        }

        private void PlayerRun_OnRunEvent(bool isRunning)
        {
            _isRunning = isRunning;
            Apply();
        }

        private void Apply()
        {
            if (footstepEmitter == null) return;

            // Crouching wins: a crouch-sprint should be quiet, not loud.
            footstepEmitter.LoudnessMultiplier =
                _isCrouching ? crouchingMultiplier
                : _isRunning ? runningMultiplier
                : walkingMultiplier;
        }
    }
}
