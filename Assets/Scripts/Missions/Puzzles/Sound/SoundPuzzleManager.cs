using System;
using System.Collections.Generic;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Puzzles
{
    public class SoundPuzzleManager : PuzzleManagerBase, IInteractable
    {
        public event Action<SoundPuzzleManager> OnLocalPlayerInteracted;

        [SerializeField] private List<SpawnConfig> soundObjectConfigs;


        protected override void SpawnPuzzle()
        {
            foreach (SpawnConfig config in soundObjectConfigs)
            {
                foreach ((GameObject prefab, Transform spawnPoint) in SpawnUtility.GenerateSpawnAssignments(config))
                {
                    GameObject spawned = SpawnTracked(prefab, spawnPoint);

                    if (spawned.TryGetComponent(out IMissionOwnerAware ownerAware))
                    {
                        ownerAware.BindToPuzzle(this);
                    }
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
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (OwnershipSelector == null || !OwnershipSelector.IsMissionOwner(senderId)) return;

            CompletePuzzle(senderId);
        }

    }
}
