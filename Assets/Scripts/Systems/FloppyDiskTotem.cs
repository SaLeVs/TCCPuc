using System;
using Components.Sound;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class FloppyDiskTotem : NetworkBehaviour, IInteractable, IInteractionBlockReason
    {
        public event Action OnAllDisksPlaced;

        [SerializeField] private Transform[] diskSpawnPoints;
        [SerializeField] private GameObject diskVisualPrefab;
        [SerializeField] private SoundDefinitionSO diskInsertedSound;

        [Header("Escape objectives")]
        [Tooltip("Locks the totem until the main mission and the audience goal are done. The " +
                 "MainDoorEscapeManager reports both. Turn off for scenes without that manager.")]
        [SerializeField] private bool lockedUntilObjectives = true;

        [Header("Blocked messages")]
        [SerializeField] private string missingHeader = "Falta:";
        [SerializeField] private string missingMainMission = "- Concluir a missão principal";
        [SerializeField] private string missingAudience = "- Atingir a meta de audiência";
        [SerializeField] private string noDiskMessage = "Você não tem disquete";
        [SerializeField] private string completeMessage = "Todos os disquetes já estão no totem";

        private readonly NetworkVariable<bool> _mainMissionDone = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _audienceReached = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private int _cachedReasonKey = -1;
        private string _cachedReason;

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

        public bool IsUnlocked => !lockedUntilObjectives || (_mainMissionDone.Value && _audienceReached.Value);

        /// <summary>Server only. Called by the escape manager as the objectives complete.</summary>
        public void SetObjectivesProgress(bool mainMissionDone, bool audienceReached)
        {
            if (!IsServer) return;

            _mainMissionDone.Value = mainMissionDone;
            _audienceReached.Value = audienceReached;
        }

        public bool CanInteract(GameObject interactor) => IsUnlocked && !IsComplete && HasDisk(interactor);

        public string GetBlockedReason(GameObject interactor)
        {
            // Cached by state: the HUD asks every frame while the crosshair rests on the totem.
            int key;

            if (!IsUnlocked)
                key = (_mainMissionDone.Value ? 0 : 1) | (_audienceReached.Value ? 0 : 2);
            else if (IsComplete)
                key = 4;
            else if (!HasDisk(interactor))
                key = 8 + _disksPlaced.Value * 64 + _requiredDisks.Value;
            else
                return null;

            if (key == _cachedReasonKey) return _cachedReason;

            _cachedReasonKey = key;
            _cachedReason = BuildBlockedReason(interactor);
            return _cachedReason;
        }

        private string BuildBlockedReason(GameObject interactor)
        {
            if (!IsUnlocked)
            {
                string reason = missingHeader;
                if (!_mainMissionDone.Value) reason += "\n" + missingMainMission;
                if (!_audienceReached.Value) reason += "\n" + missingAudience;
                return reason;
            }

            if (IsComplete) return completeMessage;

            return $"{noDiskMessage}\nDisquetes: {_disksPlaced.Value}/{_requiredDisks.Value}";
        }

        private static bool HasDisk(GameObject interactor) =>
            interactor != null && interactor.TryGetComponent(out PlayerDiskHolder holder) && holder.HasDisk;

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
            if (IsComplete || !IsUnlocked) return;

            ulong senderId = rpcParams.Receive.SenderClientId;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out NetworkClient client)) return;
            if (!client.PlayerObject.TryGetComponent(out PlayerDiskHolder diskHolder)) return;
            if (!diskHolder.HasDisk) return;

            diskHolder.TryConsumeDisk();

            int slotIndex = _disksPlaced.Value;
            _disksPlaced.Value++;

            SpawnDiskVisualRpc(slotIndex);
            WorldSound.Play(diskInsertedSound, transform.position, NetworkObject);

            if (IsComplete)
            {
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