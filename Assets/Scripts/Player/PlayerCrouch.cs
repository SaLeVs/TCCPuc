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
        [SerializeField] private float crouchHeight;
        [SerializeField] private float crouchSpeed;
        
        [SerializeField] private Transform ceilingCheck;
        [SerializeField] private float ceilingCheckRadius = 0.25f;
        [SerializeField] private LayerMask ceilingMask;
        
        private Vector3 _standColliderCenter;
        private float _standHeight;
        private bool _isCrouching;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnCrouchEvent += InputReader_OnCrouchEvent;
                _standColliderCenter = capsuleCollider.center;
                _standHeight = capsuleCollider.height;
            }
            
        }

        
        private void Update()
        {
            if (IsOwner)
            {
                CrouchCollider();
            }
            
        }
        
        private void InputReader_OnCrouchEvent(bool isCrouching)
        {
            _isCrouching = isCrouching;

            OnCrouchEvent?.Invoke(_isCrouching);
            
        }

        private void CrouchCollider()
        {
            Vector3 targetCenter = _standColliderCenter;
            float targetHeight = _standHeight;
            
            bool wantCrouch = _isCrouching;
            bool ceilingBlocked = IsCeilingBlocked();

            if (wantCrouch || ceilingBlocked)
            {
                targetCenter = crouchColliderCenter;
                targetHeight = crouchHeight;
            }
            
            capsuleCollider.height = Mathf.Lerp(capsuleCollider.height, targetHeight, crouchSpeed * Time.deltaTime);
            capsuleCollider.center = Vector3.Lerp(capsuleCollider.center, targetCenter, crouchSpeed * Time.deltaTime);

            if (Mathf.Abs(capsuleCollider.height - targetHeight) < 0.01f)
            {
                capsuleCollider.height = targetHeight;
            }
            
        }
        
        
        private bool IsCeilingBlocked()
        {
            return Physics.CheckSphere(ceilingCheck.position, ceilingCheckRadius, ceilingMask, QueryTriggerInteraction.Ignore);
            
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

