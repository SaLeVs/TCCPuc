using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class FollowTransform : NetworkBehaviour
    {
        [SerializeField] private Transform target;
    
        [SerializeField] private bool followPosition;
        [SerializeField] private bool followRotation;
        [SerializeField] private bool followScale;
        
        [SerializeField] private bool useInterpolation;
        [SerializeField] private float interpolationSpeed;
    
    
        public void SetTarget(Transform target)
        {
            this.target = target;
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
                transform.position = Vector3.Lerp(transform.position, target.position, interpolationSpeed * Time.deltaTime);

            if (followRotation)
                transform.rotation = Quaternion.Lerp(transform.rotation, target.rotation, interpolationSpeed * Time.deltaTime);

            if (followScale)
                transform.localScale = Vector3.Lerp(transform.localScale, target.localScale, interpolationSpeed * Time.deltaTime);
        }

        private void Move()
        {
            if (followPosition)
                transform.position = target.position;

            if (followRotation)
                transform.rotation = target.rotation;

            if (followScale)
                transform.localScale = target.localScale;
        }

    

        private void OnDisable()
        {
            target = null; 
        }
    } 
}

