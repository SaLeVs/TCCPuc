using UnityEngine;

namespace Player
{
    public class PlayerRagdoll : MonoBehaviour
    {
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform eyesForward;
        [SerializeField] private Transform hipsBone;
        [SerializeField] private Rigidbody[] rigidbodies;
        [SerializeField] private Collider[] colliders;

        public Transform HeadBone => headBone;

        /// <summary>A point sitting in front of the eyes. The camera aims at it, so the view always
        /// looks wherever the head is looking without depending on the rig's bone axes.</summary>
        public Transform EyesForward => eyesForward;
        public Transform HipsBone => hipsBone;

        /// <summary>The gaze as a rotation in the head bone's space. EyesForward is parented to the
        /// head, so its local position is the direction the eyes look.</summary>
        public Quaternion HeadGaze => eyesForward != null && eyesForward.localPosition.sqrMagnitude > 0.000001f
            ? Quaternion.LookRotation(eyesForward.localPosition, Vector3.up)
            : Quaternion.identity;

        public void InitializeFrom(Transform sourceRoot)
        {
            if (sourceRoot == null) return;

            CopyPoseRecursive(sourceRoot, transform);
            EnablePhysics();
        }
        
        public void ApplyImpulse(Vector3 impulse)
        {
            if (impulse == Vector3.zero) return;

            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb == null || rb.isKinematic) continue;

                rb.AddForce(impulse, ForceMode.VelocityChange);
            }
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

                // Set once the body is dynamic: a kinematic one only takes speculative CCD. Thrown
                // by a knockdown, or by a door leaf still swinging into it, a discrete limb moves
                // further in a step than a leaf or a thin wall is thick.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }
    }
}
