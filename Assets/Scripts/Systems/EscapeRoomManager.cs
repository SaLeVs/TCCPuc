using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class EscapeRoomManager : NetworkBehaviour
    {
        [SerializeField] private FloppyDiskTotem floppyDiskTotem;
        [SerializeField] private Animator leftDoorAnimator;
        [SerializeField] private Animator rightDoorAnimator;

        private static readonly int OpenHash = Animator.StringToHash("Open");

        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            
            floppyDiskTotem.OnAllDisksPlaced += HandleAllDisksPlaced;
        }

        
        private void HandleAllDisksPlaced()
        {
            OpenDoorsRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void OpenDoorsRpc()
        {
            leftDoorAnimator.SetTrigger(OpenHash);
            rightDoorAnimator.SetTrigger(OpenHash);
        }

        
        public override void OnNetworkDespawn()
        {
            if (IsServer && floppyDiskTotem != null)
            {
                floppyDiskTotem.OnAllDisksPlaced -= HandleAllDisksPlaced;
            }
        }
        
    }
}