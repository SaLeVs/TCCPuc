using Interfaces;
using UnityEngine;

namespace Monster.MonsterSabotages
{
    /// <summary>
    /// Slams this door shut and holds it. Sits on the door prefab the same way LightSabotage sits
    /// on a light, but registers itself instead of being listed on the monster — doors are spawned
    /// with their rooms, so nothing can reference them ahead of time.
    /// </summary>
    public class DoorSabotage : MonoBehaviour, ISabotageable
    {
        [Tooltip("How long players are locked out of this door after the monster slams it.")]
        [SerializeField] private float blockSeconds = 12f;

        public SabotageType SabotageType => SabotageType.Door;

        /// <summary>The hold expires on its own, so this is simply whether it is still held.</summary>
        public bool IsSabotaged => _door != null && _door.IsLocked;

        private IForceableDoor _door;

        private void Awake()
        {
            _door = GetComponent<IForceableDoor>();
        }

        private void OnEnable()
        {
            SabotageRegistry.Register(this);
        }

        private void OnDisable()
        {
            SabotageRegistry.Unregister(this);
        }

        public void Sabotage()
        {
            // Server-guarded inside the door: the state is a NetworkVariable and replicates on its
            // own, so the client-side call from SabotageClientRpc is a harmless no-op.
            _door?.CloseAndLock(blockSeconds);
        }

        public void Restore()
        {
            _door?.ClearLock();
        }
    }
}
