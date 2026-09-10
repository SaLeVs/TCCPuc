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
        
        public float LoudnessMultiplier { get; set; } = 1f;
        
        public void AnimationFootstep()
        {
            if (IsServer)
            {
                NotifyFootstepClientRpc(transform.position, source);
            }
            else if (IsOwner)
            {
                NotifyFootstepServerRpc(transform.position);
            }
        }


        [Rpc(SendTo.Server)]
        private void NotifyFootstepServerRpc(Vector3 position)
        {
            NotifyFootstepClientRpc(position, source);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyFootstepClientRpc(Vector3 position, FootstepSource footstepSource)
        {
            OnFootstepSound?.Invoke(footstepSource, position);
            
            if (reportsNoise)
            {
                NoiseBus.Report(position, footstepLoudness * Mathf.Max(0f, LoudnessMultiplier), NoiseType.Footstep, transform.root);
            }
        }

    }
}
