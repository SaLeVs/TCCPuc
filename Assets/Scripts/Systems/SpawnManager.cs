using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class SpawnManager : NetworkBehaviour
    {
        [SerializeField] private SpawnPoint[] spawnPoints;
        [SerializeField] private GameObject playerPrefab;
        
        private readonly List<SpawnPoint> _availableSpawns = new List<SpawnPoint>();
        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            
            InitializeSpawnPool();
            NetworkManager.SceneManager.OnSceneEvent += NetworkManager_OnSceneEvent;
        }
        
        private void InitializeSpawnPool()
        {
            _availableSpawns.Clear();
            _availableSpawns.AddRange(spawnPoints);
            ShuffleSpawns();
        }
        
        private void NetworkManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType != SceneEventType.LoadComplete) return;

            SpawnClient(sceneEvent.ClientId);
        }
     
        private void SpawnClient(ulong clientId)
        {
            if (!IsServer) return;

            if (_availableSpawns.Count == 0)
            {
                Debug.LogWarning($"SpawnManager: No spawns remaining for client: {clientId}");
                return;
            }

            SpawnPoint chosenSpawn = _availableSpawns[0];
            _availableSpawns.RemoveAt(0);

            SpawnPlayer(clientId, chosenSpawn);
        }
        
        private void SpawnPlayer(ulong clientId, SpawnPoint spawnPoint)
        {
            GameObject player = Instantiate(playerPrefab, spawnPoint.SpawnTransform.position, spawnPoint.SpawnTransform.rotation);
            
            Debug.Log(
                $"[SERVER] SpawnPoint: {spawnPoint.name} | " +
                $"Posição esperada: {spawnPoint.SpawnTransform.position} | " +
                $"Posição do objeto criado: {player.transform.position} | " +
                $"ClientId: {clientId}"
            );

            if (player.TryGetComponent(out NetworkObject networkObject))
            {
                networkObject.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            }
        }
        
        private void ShuffleSpawns()
        {
            for (int i = _availableSpawns.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_availableSpawns[i], _availableSpawns[j]) = (_availableSpawns[j], _availableSpawns[i]);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            
            NetworkManager.SceneManager.OnSceneEvent -= NetworkManager_OnSceneEvent;
        }
    }
}