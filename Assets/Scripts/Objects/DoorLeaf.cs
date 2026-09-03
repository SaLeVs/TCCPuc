using UnityEngine;

namespace Objects
{
    [System.Serializable]
    public class DoorLeaf
    {
        [Tooltip("Where door leaf rotates around.")]
        public Transform pivot;

        [Tooltip("Kinematic Rigidbody in the same gameObject of pivot.")]
        public Rigidbody rigidbodyRef;

        [Tooltip("Relay that will notify when this leaf collides with something.")]
        public DoorLeafCollisionRelay collisionRelay;

        public float openAngle = 100f;

        [Tooltip("1 for normal leaf and -1 for mirrored leaf (e.g., left leaf of a double door).")]
        public float mirrorMultiplier = 1f;

        [System.NonSerialized] private Quaternion _initialLocalRotation = Quaternion.identity;
        [System.NonSerialized] private bool _isCached;
        
        public void CacheInitialRotation()
        {
            if (_isCached || pivot == null) return;

            _initialLocalRotation = pivot.localRotation;
            _isCached = true;
        }
        
        public Quaternion GetTargetLocalRotation(float stateSign)
        {
            float angle = openAngle * mirrorMultiplier * stateSign;
            return _initialLocalRotation * Quaternion.Euler(0f, angle, 0f);
        }
    }
}