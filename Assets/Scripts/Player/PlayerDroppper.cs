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

        private bool _sceneChanging;

        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            
            playerDead.OnDeathEvent += PlayerDead_OnPlayerDied;

            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            }
        }

        // A scene change despawns every player too; only an owner leaving should drop anything.
        private void SceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.Load || sceneEvent.SceneEventType == SceneEventType.Unload)
            {
                _sceneChanging = true;
            }
        }

        /// <summary>
        /// Someone leaving drops what they carried, the same as dying. Their items used to vanish with
        /// the player object - mission items included, which made those missions impossible.
        ///
        /// <para>From the despawn and not from OnClientDisconnectCallback: Netcode despawns the leaving
        /// player's object first and raises that callback afterwards, when there is nothing left to
        /// read. A despawn for any other reason - the session shutting down, the scene changing, the
        /// host itself - drops nothing.</para>
        /// </summary>
        private bool IsOwnerLeaving()
        {
            NetworkManager networkManager = NetworkManager;

            if (networkManager == null || networkManager.ShutdownInProgress || !networkManager.IsListening) return false;
            if (_sceneChanging) return false;
            if (OwnerClientId == NetworkManager.ServerClientId) return false;

            return !playerDead.IsDead;
        }

        private void DropInventoryOfLeaver()
        {
            // Read only: the object is being despawned, too late to write its replicated slots.
            for (int i = 0; i < playerInventory.MaxInventorySize; i++)
            {
                int itemId = playerInventory.GetItemInSlot(i);
                if (itemId == -1) continue;

                ItemDataSO item = itemDatabase.GetItem(itemId);
                if (item != null)
                {
                    SpawnPickable(item.prefabPickable, itemId);
                }
            }

            if (diskItem == null || playerDiskHolder == null) return;

            for (int i = 0; i < playerDiskHolder.DiskCount; i++)
            {
                SpawnPickable(diskItem);
            }
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

            if (IsOwnerLeaving())
            {
                DropInventoryOfLeaver();
            }

            playerDead.OnDeathEvent -= PlayerDead_OnPlayerDied;

            if (NetworkManager != null && NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnSceneEvent -= SceneManager_OnSceneEvent;
            }
        }
        
        
    }
}
