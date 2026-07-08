using System;
using Enums;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class FootstepEmitter : NetworkBehaviour
    {
        public static event Action<FootstepSource, Vector3> OnFootstepSound;
        
        [SerializeField] private FootstepSource source;
        
        
        /// <summary>
        /// Called by animationEvents
        /// </summary>
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
        }
        
    }
}