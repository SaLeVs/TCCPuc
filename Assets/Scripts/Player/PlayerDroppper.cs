using System.Collections.Generic;
using Player;
using ScriptableObjects;
using Systems;
using Unity.Netcode;
using UnityEngine;
using System;

namespace Player
{
    public class PlayerDropper : NetworkBehaviour
    {
        public static event Action<GameObject, int> OnItemDropped;
        
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerDiskHolder playerDiskHolder;
        [SerializeField] private PlayerDead playerDead;
        [SerializeField] private ItemListSO itemDatabase;
        [SerializeField] private GameObject diskItem;

        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            
            playerDead.OnDeathEvent += PlayerDead_OnPlayerDied;
        }

        
        private void PlayerDead_OnPlayerDied(bool isDead)
        {
            if (!isDead) return;

            DropInventory();
            DropDisk();
        }
        
        private void DropInventory()
        {
            List<int> items = playerInventory.CollectAndClearAllItems();

            foreach (int itemId in items)
            {
                ItemDataSO item = itemDatabase.GetItem(itemId);
                if (item != null)
                {
                    SpawnPickable(item.prefabPickable, itemId);
                }
            }
        }


        private void DropDisk()
        {
            if (diskItem == null) return;

            while (playerDiskHolder.HasDisk)
            {
                playerDiskHolder.TryConsumeDisk();
                SpawnPickable(diskItem);
            }
        }

        private void SpawnPickable(GameObject prefab, int itemId = -1)
        {
            if (prefab == null) return;

            Transform spawnPoint = LostItemsSpawn.Instance?.GetNextSpawnPoint();
            if (spawnPoint == null) return;

            GameObject spawned = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

            if (spawned.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }

            if (itemId >= 0)
            {
                OnItemDropped?.Invoke(spawned, itemId);
            }
        }

        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            
            playerDead.OnDeathEvent -= PlayerDead_OnPlayerDied;
        }
        
        
    }
}
