using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerAnimations : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody rb;

        private Vector3 _velocity;
        private Vector3 _horizontalVelocity;
        
        private readonly int _moveXHash = Animator.StringToHash("_xVelocity");
        private readonly int _moveYHash = Animator.StringToHash("_yVelocity");
        

        private void Update()
        {
            _velocity = rb.linearVelocity;
            _horizontalVelocity = transform.InverseTransformDirection(_velocity);

            animator.SetFloat(_moveXHash, _horizontalVelocity.x);
            animator.SetFloat(_moveYHash, _horizontalVelocity.z);
        }
    }
}

