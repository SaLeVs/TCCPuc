using System;
using Components.Perception;
using Enums;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class FootstepEmitter : NetworkBehaviour
    {
        public static event Action<FootstepSource, Vector3> OnFootstepSound;

        [SerializeField] private FootstepSource source;

        [Header("Noise heard by the monster")]
        [Tooltip("How far a normal step carries, in meters. Crouching and sprinting scale this through LoudnessMultiplier.")]
        [SerializeField, Min(0f)] private float footstepLoudness = 9f;

        [Tooltip("Off for the monster's own steps, or for anything that should be silent to the AI.")]
        [SerializeField] private bool reportsNoise = true;

        /// <summary>
        /// Scales every step this emitter makes. Driven by <see cref="Perception.PlayerNoiseProfile"/>:
        /// crouch turns it down, sprint turns it up. Only meaningful on the owner, which is why
        /// it travels with the step instead of being read on the receiving side.
        /// </summary>
        public float LoudnessMultiplier { get; set; } = 1f;

        /// <summary>
        /// Called by animationEvents
        /// </summary>
        public void AnimationFootstep()
        {
            float multiplier = Mathf.Max(0f, LoudnessMultiplier);

            if (IsServer)
            {
                NotifyFootstepClientRpc(transform.position, source, multiplier);
            }
            else if (IsOwner)
            {
                NotifyFootstepServerRpc(transform.position, multiplier);
            }
        }


        [Rpc(SendTo.Server)]
        private void NotifyFootstepServerRpc(Vector3 position, float loudnessMultiplier)
        {
            NotifyFootstepClientRpc(position, source, loudnessMultiplier);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyFootstepClientRpc(Vector3 position, FootstepSource footstepSource, float loudnessMultiplier)
        {
            OnFootstepSound?.Invoke(footstepSource, position);

            // The bus fires on every peer; only the server's HearingSensor acts on it.
            if (reportsNoise)
            {
                NoiseBus.Report(
                    position,
                    footstepLoudness * loudnessMultiplier,
                    NoiseType.Footstep,
                    transform.root);
            }
        }

    }
}
