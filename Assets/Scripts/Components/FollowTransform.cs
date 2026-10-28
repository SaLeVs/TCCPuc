using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class FollowTransform : NetworkBehaviour
    {
        [SerializeField] private Transform target;
    
        // Pose kept from the target, in its axes and in metres (its scale is ignored, so bones work too).
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffset;

        [SerializeField] private bool followPosition;
        [SerializeField] private bool followRotation;
        [SerializeField] private bool followScale;
        
        [SerializeField] private bool useInterpolation;
        [SerializeField] private float interpolationSpeed;
    
    
        public void SetTarget(Transform target)
        {
            this.target = target;
        }
    
        public void SetTarget(Transform target, Vector3 positionOffset, Vector3 rotationOffset)
        {
            this.target = target;
            this.positionOffset = positionOffset;
            this.rotationOffset = rotationOffset;
        }
    
        private void LateUpdate()
        {
            if (target == null) return;
            if (!IsClient) return;
        
            if (useInterpolation)
            {
                MoveWithInterpolation();
            }
            else
            {
                Move();
            }
        }

        private void MoveWithInterpolation()
        {
            if (followPosition)
                transform.position = Vector3.Lerp(transform.position, TargetPosition(), interpolationSpeed * Time.deltaTime);

            if (followRotation)
                transform.rotation = Quaternion.Lerp(transform.rotation, TargetRotation(), interpolationSpeed * Time.deltaTime);

            if (followScale)
                transform.localScale = Vector3.Lerp(transform.localScale, target.localScale, interpolationSpeed * Time.deltaTime);
        }

        private void Move()
        {
            if (followPosition)
                transform.position = TargetPosition();

            if (followRotation)
                transform.rotation = TargetRotation();

            if (followScale)
                transform.localScale = target.localScale;
        }

        private Vector3 TargetPosition()
        {
            return target.position + target.rotation * positionOffset;
        }

        private Quaternion TargetRotation()
        {
            return target.rotation * Quaternion.Euler(rotationOffset);
        }

    

        private void OnDisable()
        {
            target = null; 
        }
    } 
}

