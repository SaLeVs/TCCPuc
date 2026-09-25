using System;
using System.Collections.Generic;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Puzzles
{
    // Para criar um puzzle novo:
    // 1. Herde desta classe e implemente SpawnPuzzle() usando SpawnPiece / SpawnTracked;
    // 2. As peças herdam de PuzzlePieceBase e chamam NotifyChanged() quando mudam;
    // 3. Sobrescreva IsSolved() só se a regra não for "todas as peças corretas".
    // Puzzles sem peças (ex.: Sound) chamam CompletePuzzle() direto.
    public abstract class PuzzleManagerBase : NetworkBehaviour, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;
        public event Action<bool> OnCompleteChanged;

        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionCompleter missionCompleter;

        public MissionOwnershipSelector OwnershipSelector => ownershipSelector;
        public bool IsComplete => _isComplete.Value;

        protected IReadOnlyList<PuzzlePieceBase> Pieces => _pieces;

        private readonly NetworkVariable<bool> _isComplete = new();
        private readonly List<PuzzlePieceBase> _pieces = new();
        private readonly List<GameObject> _spawnedObjects = new();


        public override void OnNetworkSpawn()
        {
            _isComplete.OnValueChanged += HandleCompleteChanged;
            OnCompleteChanged?.Invoke(_isComplete.Value);
        }

        private void HandleCompleteChanged(bool previousValue, bool newValue)
        {
            OnCompleteChanged?.Invoke(newValue);
        }

        public void RequestSpawn()
        {
            if (!IsServer) return;

            SpawnPuzzle();
            OnSpawnCompleted?.Invoke();
        }

        protected abstract void SpawnPuzzle();

        protected virtual bool IsSolved()
        {
            if (_pieces.Count == 0) return false;

            foreach (PuzzlePieceBase piece in _pieces)
            {
                if (!piece.IsCorrect) return false;
            }

            return true;
        }

        // Hook para atualizar estado derivado (contadores, feedback) antes de checar a solução.
        protected virtual void OnPieceChanged(PuzzlePieceBase piece, ulong clientId) { }

        public void NotifyPieceChanged(PuzzlePieceBase piece, ulong clientId)
        {
            if (!IsServer || IsComplete) return;

            OnPieceChanged(piece, clientId);

            if (IsSolved())
            {
                CompletePuzzle(clientId);
            }
        }

        protected void CompletePuzzle(ulong clientId)
        {
            if (!IsServer || IsComplete) return;

            _isComplete.Value = true;
            missionCompleter.Complete();
            NotifyOwnerPuzzleCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyOwnerPuzzleCompletedRpc(RpcParams rpcParams = default)
        {
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetObj != null && playerNetObj.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.CompletePersonalMission(OwnershipSelector.Mission);
            }
        }

        // Instancia, faz Spawn na rede e registra para o despawn automático.
        protected GameObject SpawnTracked(GameObject prefab, Transform spawnPoint)
        {
            GameObject spawned = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

            if (spawned.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }

            _spawnedObjects.Add(spawned);
            return spawned;
        }

        // SpawnTracked + liga a peça a este puzzle e inclui ela na checagem do IsSolved().
        protected T SpawnPiece<T>(GameObject prefab, Transform spawnPoint) where T : PuzzlePieceBase
        {
            GameObject spawned = SpawnTracked(prefab, spawnPoint);

            if (!spawned.TryGetComponent(out T piece))
            {
                Debug.LogWarning($"{name}: prefab '{prefab.name}' has no {typeof(T).Name}.");
                return null;
            }

            piece.Bind(this);
            _pieces.Add(piece);
            return piece;
        }

        protected int CountCorrectPieces()
        {
            int correct = 0;

            foreach (PuzzlePieceBase piece in _pieces)
            {
                if (piece.IsCorrect) correct++;
            }

            return correct;
        }


        public override void OnNetworkDespawn()
        {
            _isComplete.OnValueChanged -= HandleCompleteChanged;

            if (!IsServer) return;

            foreach (GameObject spawned in _spawnedObjects)
            {
                if (spawned == null) continue;

                if (spawned.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                else
                {
                    Destroy(spawned);
                }
            }

            _spawnedObjects.Clear();
            _pieces.Clear();
        }

    }
}
