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
                
                if (_isCrouching)
                {
                    _crouchState = true;
                    CrouchCollider();
                    Debug.Log("Crouching");
                }
                else if(!_isCrouching && IsCeilingBlocked())
                {
                    _crouchState = true;
                    CrouchCollider();
                    Debug.Log("Ceiling blocked, cannot stand up");
                }
                else
                {
                    _crouchState = false;
                    StandCollider();
                    Debug.Log("Standing up");
                }
                
                OnCrouchEvent?.Invoke(_crouchState);
                
            }
            
            private void CrouchCollider()
            {
                capsuleCollider.height = crouchHeight;
                capsuleCollider.center = crouchColliderCenter;

                if (Mathf.Abs(capsuleCollider.height - crouchHeight) < 0.01f)
                {
                    capsuleCollider.height = crouchHeight;
                }
                
            }
            
            private void StandCollider()
            {
                capsuleCollider.height = _standHeight;
                capsuleCollider.center = _standColliderCenter;

                if (Mathf.Abs(capsuleCollider.height - _standHeight) < 0.01f)
                {
                    capsuleCollider.height = _standHeight;
                }
                
            }
            
            
            private bool IsCeilingBlocked()
            {
                bool blocked = Physics.CheckSphere(ceilingCheck.position, ceilingCheckRadius, ceilingMask, QueryTriggerInteraction.Ignore);

                if (blocked)
                {
                    Debug.Log("Ceiling blocked by raycast");
                }

                return blocked;
                
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

