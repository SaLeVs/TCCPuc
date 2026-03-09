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
        [SerializeField] private CapsuleCollider capsuleCollider;
        
        [SerializeField] private float speedModifier = 0.5f;
        
        [Header("Crouch collider settings")]
        [SerializeField] private Vector3 crouchColliderCenter;
        [SerializeField] private float crouchRadius;
        [SerializeField] private float crouchHeight;
        
        [Header("Stand collider settings")]
        [SerializeField] private Vector3 standColliderCenter;
        [SerializeField] private float standRadius;
        [SerializeField] private float standHeight;
        
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

            if (_isCrouching)
            {
                CrouchCollider();
            }
            else
            {
                StandCollider();
            }
            
            OnCrouchEvent?.Invoke(_isCrouching);
            
        }

        private void CrouchCollider()
        {
            capsuleCollider.center = crouchColliderCenter;
            capsuleCollider.radius = crouchRadius;
            capsuleCollider.height = crouchHeight;
        }
        
        private void StandCollider()
        {
            capsuleCollider.center = standColliderCenter;
            capsuleCollider.radius = standRadius;
            capsuleCollider.height = standHeight;
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

