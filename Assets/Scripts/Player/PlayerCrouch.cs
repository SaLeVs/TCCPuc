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
        
        [SerializeField] private Transform ceilingCheck;
        [SerializeField] private float ceilingCheckRadius = 0.25f;
        [SerializeField] private LayerMask ceilingMask;
        
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
            if (isCrouching)
            {
                _isCrouching = true;
                CrouchCollider();
            }
            else
            {
                if (!IsCeilingBlocked())
                {
                    _isCrouching = false;
                    StandCollider();
                }
                else
                {
                    _isCrouching = true;
                }
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
        
        private bool IsCeilingBlocked()
        {
            return Physics.CheckSphere(
                ceilingCheck.position,
                ceilingCheckRadius,
                ceilingMask,
                QueryTriggerInteraction.Ignore
            );
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

