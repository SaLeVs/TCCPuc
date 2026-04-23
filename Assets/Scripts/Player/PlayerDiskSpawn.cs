using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerDiskSpawn : NetworkBehaviour
    {
        [SerializeField] private GameObject diskPrefab;
        [SerializeField] private PlayerDead playerDead;
        [SerializeField] private Transform diskSpawnPoint;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                playerDead.OnDeathEvent += PlayerDead_OnDeathEvent;
            }
        }

        private void PlayerDead_OnDeathEvent(bool isDead)
        {
            if (isDead)
            {
                SpawnDisk();
            }
        }

        private void SpawnDisk()
        {
            GameObject disk = Instantiate(diskPrefab, diskSpawnPoint.position, diskSpawnPoint.rotation);

            if (disk.TryGetComponent(out NetworkObject networkObject))
            {
                networkObject.Spawn();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                playerDead.OnDeathEvent -= PlayerDead_OnDeathEvent;
            }
        }
        
    }
}

