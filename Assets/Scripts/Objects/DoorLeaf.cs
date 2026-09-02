using UnityEngine;

namespace Objects
{
    [System.Serializable]
    public class DoorLeaf
    {
        [Tooltip("Where door leaf rotates around.")]
        public Transform pivot;

        [Tooltip("Rigidbody in the same gameObject of pivot.")]
        public Rigidbody rigidbodyRef;

        [Tooltip("Relay that will notify when this leaf collides with something.")]
        public DoorLeafCollisionRelay collisionRelay;
            
        public float openAngle = 100f;

        [Tooltip("1 for normal leaf and -1 for mirrored leaf (e.g., left leaf of a double door).")]
        public float mirrorMultiplier = 1f;
    }
}