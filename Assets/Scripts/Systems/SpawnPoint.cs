using UnityEngine;

namespace Systems
{
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private ReadyTotem readyTotem;
        
        public Transform SpawnTransform => transform;
        public ReadyTotem ReadyTotem => readyTotem;
    }
}