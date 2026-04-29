using System.Collections.Generic;
using ScriptableObjects;
using Unity.AI;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems
{
    public class SpawnRooms : NetworkBehaviour
    { 
        [SerializeField] private MissionManager missionManager;
        
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private NavMeshSurface navMeshSurface;
        
        
        private ContractsSO _currentContract;
        private List<RoomDataSO> _roomsToSpawn = new List<RoomDataSO>();
        private int _totalSpawnPoints;
        private int _remainingSlots;

        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            _currentContract = missionManager.CurrentContract;
            _totalSpawnPoints = spawnPoints.Length;

            GenerateRooms();
        }

        private void GenerateRooms()
        {
            if (!BuildRoomList()) return;

            ShuffleRooms();
            SpawnAllRooms();
            missionManager.OnRoomsSpawned();
            RebuildNavMeshRpc();
        }

        private bool BuildRoomList()
        {
            List<RoomDataSO> requiredRooms = _currentContract.GetAllRequiredRooms();

            if (requiredRooms.Count > _totalSpawnPoints)
            {
                Debug.LogError($"SpawnRooms: Required rooms number ({requiredRooms.Count}) is bigger than SpawnPoints ({_totalSpawnPoints}).");
                return false;
            }

            _roomsToSpawn.AddRange(requiredRooms);

            _remainingSlots = _totalSpawnPoints - _roomsToSpawn.Count;

            FillWithLootRooms();
            FillWithBaseRooms();

            if (_remainingSlots > 0)
            {
                Debug.LogWarning($"SpawnRooms: {_remainingSlots} SpawnPoints without rooms, add more rooms in contract {_currentContract.contractName}");
            }
            
            return true;
        }
        
        private void FillWithLootRooms()
        {
            foreach (RoomDataSO room in _currentContract.lootRooms)
            {
                if (_remainingSlots <= 0) break;
                if (room.isUniqueRoom && _roomsToSpawn.Contains(room)) continue;

                _roomsToSpawn.Add(room);
                _remainingSlots--;
            }
        }
        private void FillWithBaseRooms()
        {
            int attempts = 0;
            int maxAttempts = _currentContract.baseRooms.Count * 2;

            while (_remainingSlots > 0)
            {
                if (attempts++ > maxAttempts)
                {
                    Debug.LogWarning("SpawnRoom: No base rooms available.");
                    break;
                }

                RoomDataSO room = GetRandomRoom(_currentContract.baseRooms);
                if (room.isUniqueRoom && _roomsToSpawn.Contains(room)) continue;

                _roomsToSpawn.Add(room);
                _remainingSlots--;
            }
        }
        private void ShuffleRooms()
        {
            for (int i = _roomsToSpawn.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (_roomsToSpawn[i], _roomsToSpawn[randomIndex]) = (_roomsToSpawn[randomIndex], _roomsToSpawn[i]);
            }
        }

        
        private void SpawnAllRooms()
        {
            for (int i = 0; i < _roomsToSpawn.Count; i++)
            {
                GameObject roomObject = Instantiate(_roomsToSpawn[i].prefab, spawnPoints[i].position, spawnPoints[i].rotation);

                if (roomObject.TryGetComponent(out NetworkObject networkObject))
                {
                    networkObject.Spawn(); 
                }
            }
        }

        private RoomDataSO GetRandomRoom(List<RoomDataSO> pool)
        {
            return pool[Random.Range(0, pool.Count)];
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RebuildNavMeshRpc()
        {
            navMeshSurface.BuildNavMesh();
        }
        
    }
}


