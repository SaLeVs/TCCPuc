using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class FloppyDiskTotem : NetworkBehaviour, IInteractable
    {
        public event Action<FloppyDiskTotem> OnDiskPlaced;

        [SerializeField] private Transform diskSpawnPoint;

        private GameObject _diskObjectPrefab;
        private FloppyDiskManager _floppyDiskManager;

        private NetworkVariable<bool> _hasDisksInTotem = new NetworkVariable<bool>();

        public bool IsComplete => _hasDisksInTotem.Value;

        
        public void Initialize(GameObject diskObjectPrefab, FloppyDiskManager manager)
        {
            if (IsServer)
            {
                _diskObjectPrefab = diskObjectPrefab;
                _floppyDiskManager = manager;
            }
        }

        public override void OnNetworkSpawn()
        {
            _hasDisksInTotem.OnValueChanged += HasDisk_OnValueChanged;
        }

        private void HasDisk_OnValueChanged(bool previous, bool current)
        {
            if (current)
            {
                SpawnDiskVisualRpc();
            }
        }

        public void PlaceDiskServer()
        {
            if (!IsServer) return;
            if (IsComplete) return;

            _hasDisksInTotem.Value = true;
            _floppyDiskManager.NotifyDiskPlaced(this);
        }
        

        [Rpc(SendTo.ClientsAndHost)]
        private void SpawnDiskVisualRpc()
        { 
            Instantiate(_diskObjectPrefab, diskSpawnPoint.position, diskSpawnPoint.rotation);
        }

        public bool Interact(GameObject playerInteractor) => false;
        public bool CanInteract(GameObject interactor) => !IsComplete;

        public override void OnNetworkDespawn()
        {
            _hasDisksInTotem.OnValueChanged -= HasDisk_OnValueChanged;
        }
    }
}