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
            
        }
        
        private void InputReader_OnRunEvent(bool isRunning)
        {
            
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
