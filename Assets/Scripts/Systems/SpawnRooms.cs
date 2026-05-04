using System.Collections.Generic;
using Missions;
using Missions.PersonalMissions;
using ScriptableObjects;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;


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
                Debug.LogError($"SpawnRooms: Required rooms ({requiredRooms.Count}) exceeds SpawnPoints ({_totalSpawnPoints}).");
                return false;
            }

            _roomsToSpawn.AddRange(requiredRooms);
            _remainingSlots = _totalSpawnPoints - _roomsToSpawn.Count;

            FillWithLootRooms();
            FillWithUniqueBaseRooms();
            FillWithRandomBaseRooms();

            if (_remainingSlots > 0)
                Debug.LogWarning($"SpawnRooms: {_remainingSlots} SpawnPoints without rooms in contract {_currentContract.contractName}.");

            return true;
        }

        private void FillWithLootRooms()
        {
            foreach (RoomDataSO room in _currentContract.lootRooms)
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
            List<RoomDataSO> nonUniqueBaseRooms = _currentContract.baseRooms.FindAll(room => !room.isUniqueRoom);

            if (nonUniqueBaseRooms.Count == 0)
            {
                Debug.LogWarning("SpawnRooms: No non-unique base rooms available.");
                return;
            }

            while (_remainingSlots > 0)
            {
                RoomDataSO room = GetRandomRoom(nonUniqueBaseRooms);
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
                Transform spawnPoint = spawnPoints[i];
                RoomDataSO roomData = _roomsToSpawn[i];

                GameObject roomObject = Instantiate(roomData.prefab, spawnPoint.position, spawnPoint.rotation);
                MissionOwnershipSelector selector = roomObject.GetComponent<MissionOwnershipSelector>();

                if (roomObject.TryGetComponent(out NetworkObject networkObject))
                {
                    networkObject.Spawn(); 
                }
                SpawnNetworkEntries(roomData, spawnPoint, selector);
            }
        }
        
        private void SpawnNetworkEntries(RoomDataSO roomData, Transform spawnPoint, MissionOwnershipSelector selector)
        {
            foreach (NetworkSpawnEntry entry in roomData.networkSpawnEntries)
            {
                if (entry.prefab == null) continue;

                Vector3 worldPos = spawnPoint.TransformPoint(entry.localOffset);
                Quaternion worldRot = spawnPoint.rotation * Quaternion.Euler(entry.localRotation);
                
                GameObject spawned = Instantiate(entry.prefab, worldPos, worldRot);
                
                if (spawned.TryGetComponent(out MissionTotem totem))
                {
                    totem.SetOwnershipSelector(selector);
                }

                if (spawned.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                    Debug.Log($"SpawnRooms: Spawned network entry {entry.prefab.name} in room {roomData.roomID}");
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


