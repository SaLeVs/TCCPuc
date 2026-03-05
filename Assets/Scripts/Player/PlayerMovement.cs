using UnityEngine;
using Inputs;
using Unity.Netcode;


namespace Player
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Transform orientation;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float blendMovementTime = 8.9f;
        
        private Vector2 _movementInput;
        private Vector3 _movementDirection;
        
        private Vector2 _currentVelocity;
        private float _xVelocityDifference;
        private float _zVelocityDifference;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent += InputReader_OnMoveEvent;
                
            }
            
        }
        
        private void InputReader_OnMoveEvent(Vector2 movementInput) => _movementInput = movementInput;
        
        
        
        private void FixedUpdate()
        {
            if (IsOwner)
            {
                Move(_movementInput);
                
            }
            
        }
        
        
        private void Move(Vector2 moveVector)
        {
            Vector3 desiredVelocityWorld = Vector3.zero;
            
            if (moveVector.sqrMagnitude > 0.0001f)
            {
                desiredVelocityWorld = orientation.forward * moveVector.y + orientation.right * moveVector.x;
                desiredVelocityWorld.y = 0f;
                float inputMag = desiredVelocityWorld.magnitude;
                
                if (inputMag > 0.0001f)
                {
                    desiredVelocityWorld = desiredVelocityWorld.normalized * (moveSpeed * Mathf.Clamp01(moveVector.magnitude));
                }
                else
                {
                    desiredVelocityWorld = Vector3.zero;
                }
            }
            else
            {
                desiredVelocityWorld = Vector3.zero;
            }
            
            _currentVelocity.x = Mathf.Lerp(_currentVelocity.x, desiredVelocityWorld.x, blendMovementTime * Time.fixedDeltaTime);
            _currentVelocity.y = Mathf.Lerp(_currentVelocity.y, desiredVelocityWorld.z, blendMovementTime * Time.fixedDeltaTime);
            
            _xVelocityDifference = _currentVelocity.x - rb.linearVelocity.x;
            _zVelocityDifference = _currentVelocity.y - rb.linearVelocity.z;
            
            rb.AddForce(new Vector3(_xVelocityDifference, 0f, _zVelocityDifference), ForceMode.VelocityChange); 
            
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent -= InputReader_OnMoveEvent;
                
            }
            
        }
        
    }
}
