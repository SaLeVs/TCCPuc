using System;
using System.Collections.Generic;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class SoundMissionManager : MissionsManagerBase, IMissionSpawnable, IInteractable
    {
        public event Action<SoundMissionManager> OnLocalPlayerInteracted;
        public event Action OnSpawnCompleted;

        [SerializeField] private List<SpawnConfig> soundObjectConfigs;
        [SerializeField] private MissionCompleter  missionCompleter;
        
        public override bool IsComplete { get; protected set; }
        private readonly List<NetworkObject> _spawnedObjects = new();

        
        public void RequestSpawn()
        {
            if (!IsServer) return;

            SpawnSoundObjects();
            OnSpawnCompleted?.Invoke();
        }

        private void SpawnSoundObjects()
        {
            foreach (SpawnConfig config in soundObjectConfigs)
            {
                if (config.prefab == null || config.spawnPoint == null) continue;

                GameObject spawned = Instantiate(config.prefab, config.spawnPoint.position, config.spawnPoint.rotation);

                if (spawned.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();

                    if (spawned.TryGetComponent(out IMissionOwnerAware ownerAware))
                    {
                        ownerAware.SetOwnershipSelector(this);
                    }

                    _spawnedObjects.Add(netObj);
                }
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (IsComplete) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;
            if (OwnershipSelector == null) return false;

            return OwnershipSelector.IsMissionOwner(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false; 
            if (!playerInteractor.TryGetComponent(out NetworkObject networkObject)) return false;

            OpenUiRpc(RpcTarget.Single(networkObject.OwnerClientId, RpcTargetUse.Temp));
            return true;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void OpenUiRpc(RpcParams rpcParams = default)
        {
            NetworkObject localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            
            if (localPlayer == null) return;
            
            SoundMissionUi ui = localPlayer.GetComponentInChildren<SoundMissionUi>(true);
            
            if (ui == null)
            {
                Debug.LogError($"SoundMissionUi não encontrada em {localPlayer.name}");
                return;
            }

            ui.Open(this);
        }
        
        [Rpc(SendTo.Server)]
        public void NotifyPuzzleCompletedRpc(RpcParams rpcParams = default)
        {
            if (IsComplete) return;
            if (OwnershipSelector == null || !OwnershipSelector.IsMissionOwner(rpcParams.Receive.SenderClientId)) return;

            IsComplete = true;
            missionCompleter.Complete();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyOwnerMissionCompletedRpc(RpcParams rpcParams = default)
        {
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetObj != null && playerNetObj.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.CompletePersonalMission(OwnershipSelector.Mission);
            }
        }

        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            foreach (NetworkObject netObj in _spawnedObjects)
            {
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
            }
            
            _spawnedObjects.Clear();
        }
        
    }
}