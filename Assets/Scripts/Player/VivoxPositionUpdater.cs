using Network;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace Player
{
    public class VivoxPositionUpdater : NetworkBehaviour
    {
        [SerializeField] private Transform earTransform;

        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            
            enabled = true;
        }

        private void Update()
        {
            if (!IsOwner) return;

            VivoxManager vivoxManager = VivoxManager.instance;
            
            if (vivoxManager == null) return;
            if (!vivoxManager.IsInPositionalChannel) return;
            if (!VivoxService.Instance.IsLoggedIn) return;

            VivoxService.Instance.Set3DPosition(gameObject.transform.position, earTransform.position, earTransform.forward, Vector3.up, vivoxManager.CurrentChannelName);
        }
    }
}