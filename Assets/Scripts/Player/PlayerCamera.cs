using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerCamera : NetworkBehaviour
    {
        [SerializeField] private int ownerCameraPriority = 10;
        [SerializeField] private CinemachineCamera playerCamera;
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerCamera.Priority = ownerCameraPriority;
            }
        }
        
    
    }
}


