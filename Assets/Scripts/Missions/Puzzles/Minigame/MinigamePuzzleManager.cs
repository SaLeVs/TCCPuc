using Components.Perception;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Puzzles
{
    // Estação no mundo que abre um minigame de UI no jogador dono da missão.
    // Trocar o minigame = trocar o prefab no campo Minigame Prefab.
    public class MinigamePuzzleManager : PuzzleManagerBase, IInteractable
    {
        [SerializeField] private MinigameBase minigamePrefab;

        [Tooltip("Opcional: barulho emitido na estação quando o jogador erra (atrai o monstro).")]
        [SerializeField] private NoiseEmitter failNoise;


        // A própria estação é o puzzle: não há peças para spawnar.
        protected override void SpawnPuzzle() { }

        public bool CanInteract(GameObject interactor)
        {
            if (IsComplete) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;
            if (OwnershipSelector == null) return false;

            return OwnershipSelector.IsMissionOwner(networkObject.OwnerClientId);
        }

        // Roda no cliente dono do jogador (PlayerInteractor), então abre a UI direto, sem RPC.
        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;

            MinigameHost host = playerInteractor.GetComponentInChildren<MinigameHost>(true);

            if (host == null)
            {
                Debug.LogError($"{name}: MinigameHost not found in {playerInteractor.name}");
                return false;
            }

            return host.Open(minigamePrefab, () => NotifyMinigameSucceededRpc(), () => NotifyMinigameFailedRpc());
        }

        [Rpc(SendTo.Server)]
        private void NotifyMinigameSucceededRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (OwnershipSelector == null || !OwnershipSelector.IsMissionOwner(senderId)) return;

            CompletePuzzle(senderId);
        }

        [Rpc(SendTo.Server)]
        private void NotifyMinigameFailedRpc(RpcParams rpcParams = default)
        {
            if (IsComplete || failNoise == null) return;
            if (OwnershipSelector == null || !OwnershipSelector.IsMissionOwner(rpcParams.Receive.SenderClientId)) return;

            failNoise.Emit();
        }

    }
}
