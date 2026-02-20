using UnityEngine;
using Inputs;
using Unity.Netcode;

namespace Player
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float runSpeed;
        
        private Vector2 _movementInput;
        private bool _isRunning;
        
        private Vector3 _movementDirection;
        private float _currentSpeed;

        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent += InputReader_OnMoveEvent;
                inputReader.OnRunEvent += InputReader_OnRunEvent;
            }
            
        }

        
        private void InputReader_OnMoveEvent(Vector2 movementInput)
        {
            _movementInput = movementInput;
        }
        
        private void InputReader_OnRunEvent(bool isRunning)
        {
            _isRunning = isRunning;
        }

        private void Update()
        {
            if (IsOwner)
            {
                _movementDirection = transform.TransformDirection(_movementInput.x, 0, _movementInput.y);
                
                _currentSpeed = _isRunning ? runSpeed : moveSpeed;
                transform.position += _movementDirection * (_currentSpeed * Time.deltaTime);
                
            }
            
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent -= InputReader_OnMoveEvent;
                inputReader.OnRunEvent -= InputReader_OnRunEvent;
            }
            
        }
        
    }
}
