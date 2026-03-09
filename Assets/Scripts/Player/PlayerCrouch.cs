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
            private bool _isCeilingBlocked;
            
            private bool _crouchState;
            private bool _lastCrouchState;
            
            
            public override void OnNetworkSpawn()
            {
                if (IsOwner)
                {
                    inputReader.OnCrouchEvent += InputReader_OnCrouchEvent;
                    _standColliderCenter = capsuleCollider.center;
                    _standHeight = capsuleCollider.height;
                }
                
            }
            
            private void InputReader_OnCrouchEvent(bool isCrouching)
            {
                _isCrouching = isCrouching;
            }
            
            private void Update()
            {
                if (IsOwner)
                {
                    if (_isCrouching)
                    {
                        _crouchState = true;
                        CrouchCollider(); 
                    } 
                    else if (_crouchState && IsCeilingBlocked())
                    {
                        _crouchState = true;
                        CrouchCollider();
                    }
                    else
                    {
                        _crouchState = false;
                        StandCollider();
                    }
                    
                    if (_crouchState != _lastCrouchState)
                    {
                        _lastCrouchState = _crouchState;
                        OnCrouchEvent?.Invoke(_crouchState);
                    }
                    
                }
                
            }
            private void CrouchCollider()
            {
                capsuleCollider.height = Mathf.Lerp(capsuleCollider.height, crouchHeight, crouchSpeed * Time.deltaTime); 
                capsuleCollider.center = Vector3.Lerp(capsuleCollider.center, crouchColliderCenter, crouchSpeed * Time.deltaTime);

                if (Mathf.Abs(capsuleCollider.height - crouchHeight) < 0.01f)
                {
                    capsuleCollider.height = crouchHeight;
                }
                
            }
            
            private void StandCollider()
            {
                capsuleCollider.height = Mathf.Lerp(capsuleCollider.height, _standHeight, crouchSpeed * Time.deltaTime);
                capsuleCollider.center = Vector3.Lerp(capsuleCollider.center, _standColliderCenter, crouchSpeed * Time.deltaTime);

                if (Mathf.Abs(capsuleCollider.height - _standHeight) < 0.01f)
                {
                    capsuleCollider.height = _standHeight;
                }
                
            }
            
            
            private bool IsCeilingBlocked()
            {
                return Physics.CheckSphere(ceilingCheck.position, ceilingCheckRadius, ceilingMask, QueryTriggerInteraction.Ignore);
            }
            
            public float ModifySpeed(float baseSpeed)
            {
                if (_crouchState)
                {
                    return baseSpeed * speedModifier;
                }

                return baseSpeed;
                
            }
            
            private void OnDrawGizmos()
            {
                if (ceilingCheck == null) return;

                Gizmos.color = Color.darkRed;
                Gizmos.DrawWireSphere(ceilingCheck.position, ceilingCheckRadius);
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

