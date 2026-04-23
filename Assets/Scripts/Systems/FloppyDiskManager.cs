using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class FloppyDiskManager : NetworkBehaviour
    {
        public event Action OnAllDisksPlaced;
    
        [SerializeField] private int totemCount;
        [SerializeField] private FloppyDiskTotem totemPrefab;
        [SerializeField] private GameObject diskObjectPrefab;
        [SerializeField] private Transform[] spawnPoints;
        
        private FloppyDiskTotem[] _totems;
        private int _completedTotems;
        

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                SpawnTotems(); 
            }
        }

        
        private void SpawnTotems()
        {
            _totems = new FloppyDiskTotem[totemCount];

            for (int i = 0; i < _totems.Length; i++)
            {
                GameObject totemObject = Instantiate(totemPrefab.gameObject, spawnPoints[i].position, spawnPoints[i].rotation);

                if (totemObject.TryGetComponent(out NetworkObject networkObject))
                {
                    networkObject.Spawn();
                }

                if (totemObject.TryGetComponent(out FloppyDiskTotem floppyDiskTotem))
                {
                    floppyDiskTotem.Initialize(diskObjectPrefab, this);
                    _totems[i] = floppyDiskTotem;
                }
            }
        }
        
        public void NotifyDiskPlaced(FloppyDiskTotem totem)
        {
            if (IsServer)
            {
                _completedTotems++;

                if (_completedTotems >= spawnPoints.Length)
                {
                    OnAllDisksPlaced?.Invoke();
                    Debug.Log("All Disks Placed");
                }
            }
        }
        
    } 
}

