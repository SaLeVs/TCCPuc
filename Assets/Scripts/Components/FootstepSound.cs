using System;
using UnityEngine;

namespace Components
{
    public class FootstepSound : MonoBehaviour
    {
        public static Action<Vector3> OnFootstepSound;
    
        [SerializeField] private float footstepDistance = 1.5f;
        [SerializeField] private float minMoveSpeedToStep = 0.3f;
        [SerializeField] private Rigidbody rb;
    
        private float _distanceSinceLastFootstep;
        private Vector3 _lastFootstepPosition;   
    
    
        private void FixedUpdate()
        {
            HandleFootsteps();
        }
    
        private void HandleFootsteps()
        {
            Vector3 currentPosition = transform.position;
            Vector3 flatDelta = currentPosition - _lastFootstepPosition;
            flatDelta.y = 0f;

            float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

            if (currentSpeed < minMoveSpeedToStep)
            {
                _distanceSinceLastFootstep = 0f;
                _lastFootstepPosition = currentPosition;
                return;
            }

            _distanceSinceLastFootstep += flatDelta.magnitude;
            _lastFootstepPosition = currentPosition;

            if (_distanceSinceLastFootstep >= footstepDistance)
            {
                _distanceSinceLastFootstep -= footstepDistance;
                OnFootstepSound?.Invoke(transform.position);
            }
        }
    }
}

