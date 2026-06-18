using UnityEngine;

namespace Player
{
    public class PlayerRagdoll : MonoBehaviour
    {
        [SerializeField] private Transform headBone;
        [SerializeField] private Rigidbody[] rigidbodies;
        [SerializeField] private Collider[] colliders;

        public Transform HeadBone => headBone;
        
        public void InitializeFrom(Transform sourceRoot)
        {
            if (sourceRoot == null) return;

            CopyPoseRecursive(sourceRoot, transform);
            EnablePhysics();
        }

        private void CopyPoseRecursive(Transform source, Transform target)
        {
            target.position = source.position;
            target.rotation = source.rotation;

            foreach (Transform targetChild in target)
            {
                Transform sourceChild = source.Find(targetChild.name);
                if (sourceChild != null)
                {
                    CopyPoseRecursive(sourceChild, targetChild);
                }
            }
        }

        private void EnablePhysics()
        {
            foreach (Collider collider in colliders)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }

            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
    }
}