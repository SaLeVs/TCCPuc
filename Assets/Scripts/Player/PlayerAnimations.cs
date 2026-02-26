using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerAnimations : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private float animationSmoothing = 0.1f;

        private Vector3 _velocity;
        private Vector3 _horizontalVelocity;
        
        private readonly int _moveXHash = Animator.StringToHash("_xVelocity");
        private readonly int _moveYHash = Animator.StringToHash("_yVelocity");
        

        private void Update()
        {
            _velocity = rb.linearVelocity;
            _horizontalVelocity = transform.InverseTransformDirection(_velocity);

            animator.SetFloat(_moveXHash, _horizontalVelocity.x, animationSmoothing, Time.deltaTime);
            animator.SetFloat(_moveYHash, _horizontalVelocity.z,animationSmoothing, Time.deltaTime );
        }
    }
}

