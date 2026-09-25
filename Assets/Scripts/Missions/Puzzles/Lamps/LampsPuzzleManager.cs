using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Missions.Puzzles
{
    public class LampsPuzzleManager : PuzzleManagerBase
    {
        public event Action<int, int> OnCorrectLampsCountChanged;

        [SerializeField] private GameObject lampPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private LampsFeedback lampsFeedback;

        public int CorrectLampsCount => _correctLampsCount.Value;
        public int TotalLampsCount => _totalLampsCount.Value;

        private NetworkVariable<int> _correctLampsCount = new NetworkVariable<int>();
        private NetworkVariable<int> _totalLampsCount = new NetworkVariable<int>();


        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _correctLampsCount.OnValueChanged += HandleCorrectCountChanged;
            _totalLampsCount.OnValueChanged += HandleTotalCountChanged;

            OnCorrectLampsCountChanged?.Invoke(_correctLampsCount.Value, _totalLampsCount.Value);
        }

        private void HandleCorrectCountChanged(int previousValue, int newValue)
        {
            OnCorrectLampsCountChanged?.Invoke(newValue, _totalLampsCount.Value);
        }

        private void HandleTotalCountChanged(int previousValue, int newValue)
        {
            OnCorrectLampsCountChanged?.Invoke(_correctLampsCount.Value, newValue);
        }

        protected override void SpawnPuzzle()
        {
            int totalLamps = spawnPoints.Length;
            HashSet<int> wrongIndices = PickWrongIndices(totalLamps);

            for (int i = 0; i < totalLamps; i++)
            {
                LampTotem lamp = SpawnPiece<LampTotem>(lampPrefab, spawnPoints[i]);

                if (lamp == null) continue;

                bool shouldBeOn = Random.Range(0, 2) == 0;
                bool startOn = wrongIndices.Contains(i) ? !shouldBeOn : shouldBeOn;

                lamp.Setup(shouldBeOn, startOn);
            }

            _totalLampsCount.Value = Pieces.Count;
            _correctLampsCount.Value = CountCorrectPieces();
        }

        private HashSet<int> PickWrongIndices(int totalLamps)
        {
            List<int> shuffledIndices = new List<int>(totalLamps);

            for (int i = 0; i < totalLamps; i++)
            {
                shuffledIndices.Add(i);
            }

            ShuffleIndices(shuffledIndices);

            int wrongCount = CalculateWrongLampsCount(totalLamps);
            HashSet<int> wrongIndices = new HashSet<int>();

            for (int i = 0; i < wrongCount; i++)
            {
                wrongIndices.Add(shuffledIndices[i]);
            }

            return wrongIndices;
        }

        private int CalculateWrongLampsCount(int totalLamps)
        {
            if (totalLamps <= 0) return 0;

            int minWrong = Mathf.CeilToInt(totalLamps * 0.5f);
            int maxWrong = totalLamps;

            return Random.Range(minWrong, maxWrong + 1);
        }

        private void ShuffleIndices(List<int> indices)
        {
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
        }

        protected override void OnPieceChanged(PuzzlePieceBase piece, ulong clientId)
        {
            _correctLampsCount.Value = CountCorrectPieces();
        }


        public override void OnNetworkDespawn()
        {
            _correctLampsCount.OnValueChanged -= HandleCorrectCountChanged;
            _totalLampsCount.OnValueChanged -= HandleTotalCountChanged;

            base.OnNetworkDespawn();
        }

    }
}
