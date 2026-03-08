using System;
using Inputs;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerCrouch : NetworkBehaviour, ISpeedModifier
    {
        public event Action<bool> OnCrouchEvent;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private float speedModifier = 0.5f;
    
        private bool _isCrouching;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnCrouchEvent += InputReader_OnCrouchEvent;
            }
            
        }

        
        private void InputReader_OnCrouchEvent(bool isCrouching)
        {
            _isCrouching = isCrouching;
            OnCrouchEvent?.Invoke(_isCrouching);
            
        }

        public float ModifySpeed(float baseSpeed)
        {
            if (_isCrouching)
            {
                return baseSpeed * speedModifier;
            }

            return baseSpeed;
            
        }


        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnCrouchEvent -= InputReader_OnCrouchEvent;
            }
            
        }
        
    }  
}

