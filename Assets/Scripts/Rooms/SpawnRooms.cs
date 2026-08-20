using System.Collections.Generic;
using Missions;
using ScriptableObjects;
using Systems;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;


namespace Rooms
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
            if (IsServer)
            {
                _currentContract = missionManager.CurrentContract;
                _totalSpawnPoints = spawnPoints.Length;
            
                PlayerTracker.Instance.OnAllPlayersConnected += GenerateRooms;
            }
        }

        
        private void GenerateRooms()
        {
            if (!BuildRoomList()) return;

            ShuffleList(_roomsToSpawn);
            SpawnAllRooms();
            missionManager.OnRoomsSpawned();
            RebuildNavMeshRpc();
        }

        private bool BuildRoomList()
        {
            _roomsToSpawn.Clear();
            
            List<RoomDataSO> requiredRooms = _currentContract.GetAllRequiredRooms();

            if (requiredRooms.Count > _totalSpawnPoints) 
            {
                return false;
            }

            _roomsToSpawn.AddRange(requiredRooms);
            _remainingSlots = _totalSpawnPoints - _roomsToSpawn.Count;

            FillWithLootRooms();
            FillWithUniqueBaseRooms();
            FillWithRandomBaseRooms();

            return true;
        }

        private void FillWithLootRooms()
        {
            List<RoomDataSO> shuffledLoot = new List<RoomDataSO>(_currentContract.lootRooms);
            ShuffleList(shuffledLoot);

            foreach (RoomDataSO room in shuffledLoot)
            {
                if (_remainingSlots <= 0) break;
                
                _roomsToSpawn.Add(room);
                _remainingSlots--;
            }
        }

        private void FillWithUniqueBaseRooms()
        {
            foreach (RoomDataSO room in _currentContract.baseRooms)
            {
                if (_remainingSlots <= 0) break;
                if (!room.isUniqueRoom) continue;

                _roomsToSpawn.Add(room);
                _remainingSlots--;
            }
        }

        private void FillWithRandomBaseRooms()
        {
            List<RoomDataSO> nonUniqueBaseRooms = _currentContract.baseRooms.FindAll(r => !r.isUniqueRoom);
            if (nonUniqueBaseRooms.Count == 0) return;

            ShuffleList(nonUniqueBaseRooms);
            int poolIndex = 0;

            while (_remainingSlots > 0)
            {
                _roomsToSpawn.Add(nonUniqueBaseRooms[poolIndex % nonUniqueBaseRooms.Count]);
                poolIndex++;
                _remainingSlots--;
            }
        }
        
        private void SpawnAllRooms()
        {
            for (int i = 0; i < _roomsToSpawn.Count; i++)
            {
                Transform spawnPoint = spawnPoints[i];
                RoomDataSO roomData = _roomsToSpawn[i];

                GameObject roomObject = Instantiate(roomData.prefab, spawnPoint.position, spawnPoint.rotation);

                if (roomObject.TryGetComponent(out NetworkObject networkObject))
                {
                    networkObject.Spawn();
                }

                SpawnNetworkEntries(roomData, spawnPoint); 
            }
        }

        private void SpawnNetworkEntries(RoomDataSO roomData, Transform spawnPoint)
        {
            foreach (NetworkSpawnEntry entry in roomData.networkSpawnEntries)
            {
                if (entry.prefab == null) continue;

                Vector3 worldPos = spawnPoint.TransformPoint(entry.localOffset);
                Quaternion worldRot = spawnPoint.rotation * Quaternion.Euler(entry.localRotation);

                GameObject spawned = Instantiate(entry.prefab, worldPos, worldRot);

                if (spawned.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RebuildNavMeshRpc()
        {
            navMeshSurface.BuildNavMesh();
        }
        
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                if (PlayerTracker.Instance != null)
                {
                    PlayerTracker.Instance.OnAllPlayersConnected -= GenerateRooms;
                }
            }
            
        }
        
    }
}


