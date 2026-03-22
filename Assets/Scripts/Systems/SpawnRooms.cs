using System.Collections.Generic;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class SpawnRooms : NetworkBehaviour
    {
        [SerializeField] private MissionsSO currentMission;
        [SerializeField] private Transform[] spawnPoints;
        
        private List<RoomDataSO> _generatedRooms = new List<RoomDataSO>();

        private int _totalRoomsToSpawn;
        private int _requiredRooms;
        private int _lootRooms;
        private int _baseRooms;
        
        private int _remainingRoomsToSpawn;

        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _totalRoomsToSpawn = spawnPoints.Length;
                GenerateRooms();
            }
            
        }

        private void GenerateRooms()
        {
            _generatedRooms.AddRange(currentMission.requiredRooms);
            _generatedRooms.AddRange(currentMission.lootRooms);
            _generatedRooms.AddRange(currentMission.baseRooms);

            if (_generatedRooms.Count > _totalRoomsToSpawn)
            {
                _generatedRooms = _generatedRooms.GetRange(0, _totalRoomsToSpawn);
            }
            
            while (_generatedRooms.Count < _totalRoomsToSpawn)
            {
                RoomDataSO randomBaseRoom = GetRandomRoom(currentMission.baseRooms);
                _generatedRooms.Add(randomBaseRoom);
            }

            RandomizeRoomsSpawns();
            SpawnRoom();
        }
        
        private RoomDataSO GetRandomRoom(List<RoomDataSO> pool)
        {
            return pool[Random.Range(0, pool.Count)];
        }

        private void RandomizeRoomsSpawns()
        {
            for (int i = 0; i < _generatedRooms.Count; i++)
            {
                int randomIndex = Random.Range(i, _generatedRooms.Count);
                
                // Randomize rooms spawns with Fisher-Yates shuffle
                (_generatedRooms[i], _generatedRooms[randomIndex]) = (_generatedRooms[randomIndex], _generatedRooms[i]);
            }
            
        }

        private void SpawnRoom()
        {
            for (int i = 0; i < _generatedRooms.Count; i++)
            {
                RoomDataSO roomData = _generatedRooms[i];
                Transform spawnPoint = spawnPoints[i];

                GameObject roomObject = Instantiate(roomData.prefab, spawnPoint.position, spawnPoint.rotation);

                if (roomObject.TryGetComponent(out NetworkObject networkObject))
                {
                    networkObject.Spawn();
                }
            }
            
        }
        
    }
}


