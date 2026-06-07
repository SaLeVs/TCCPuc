using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class FloppyDiskTotem : NetworkBehaviour, IInteractable
    {
        public event Action OnAllDisksPlaced;

        [SerializeField] private Transform[] diskSpawnPoints;
        [SerializeField] private GameObject diskVisualPrefab;

        private readonly NetworkVariable<int> _disksPlaced = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _requiredDisks = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsComplete => _requiredDisks.Value > 0 && _disksPlaced.Value >= _requiredDisks.Value;

        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _requiredDisks.Value = PlayerTracker.Instance.ConnectedPlayerCount;
            }
        }

        public bool CanInteract(GameObject interactor) => !IsComplete;

        public bool Interact(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out NetworkObject netObj)) return false;
            if (!netObj.IsOwner) return false;

            PlaceDiskRpc();
            return true;
        }

        [Rpc(SendTo.Server)]
        private void PlaceDiskRpc(RpcParams rpcParams = default)
        {
            if (IsComplete) return;

            ulong senderId = rpcParams.Receive.SenderClientId;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out NetworkClient client)) return;
            if (!client.PlayerObject.TryGetComponent(out PlayerDiskHolder diskHolder)) return;
            if (!diskHolder.HasDisk) return;

            diskHolder.TryConsumeDisk();

            int slotIndex = _disksPlaced.Value;
            _disksPlaced.Value++;

            SpawnDiskVisualRpc(slotIndex);
            Debug.Log($"Placed disk {slotIndex}");

            if (IsComplete)
            {
                Debug.Log("FloppyDiskTotem: All disks placed!");
                OnAllDisksPlaced?.Invoke();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SpawnDiskVisualRpc(int slotIndex)
        {
            if (slotIndex >= diskSpawnPoints.Length) return;

            Instantiate(diskVisualPrefab, diskSpawnPoints[slotIndex].position, diskSpawnPoints[slotIndex].rotation);
        }
        
    }
}