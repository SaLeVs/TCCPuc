using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerStairClimber : NetworkBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private Rigidbody rb;

        [Header("Step Detection")]
        [SerializeField] private float maxStepHeight = 0.3f;
        [SerializeField] private float stepReachDistance = 0.5f;
        [SerializeField] private LayerMask stepLayerMask;

        [Header("Smoothing")]
        [SerializeField] private float stepClimbSpeed = 12f;
        [SerializeField] private float climbTimeout = 0.4f;

        private bool _isDead;
        private bool _isLocked;
        private float _targetStepY = float.MinValue;
        private float _climbTimer;

        private static readonly float[] SideOffsets = { 0f, 0.4f, -0.4f };

        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerState.OnPlayerDead  += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
            }
        }

        
        private void PlayerState_OnPlayerDead(bool isDead) => _isDead = isDead;
        private void PlayerState_OnPlayerLocked(bool isLocked) => _isLocked = isLocked;

        private void FixedUpdate()
        {
            if (!IsOwner || _isDead || _isLocked) return;
            
            if (_targetStepY > float.MinValue)
            {
                ApplySmoothClimb();
                return;
            }

            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            
            if (horizontalVelocity.sqrMagnitude >= 0.01f)
            {
                TryClimbStairs(horizontalVelocity.normalized);
            }
        }

        private void ApplySmoothClimb()
        {
            _climbTimer += Time.fixedDeltaTime;

            float differenceStepY = _targetStepY - rb.position.y;

            if (differenceStepY <= 0.01f || _climbTimer >= climbTimeout)
            {
                FinishClimb();
                return;
            }

            rb.linearVelocity = new Vector3(rb.linearVelocity.x, differenceStepY * stepClimbSpeed, rb.linearVelocity.z);
        }

        private void FinishClimb()
        {
            _targetStepY = float.MinValue;
            _climbTimer = 0f;
            rb.useGravity = true;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        private void TryClimbStairs(Vector3 moveDirection)
        {
            Vector3 right = Vector3.Cross(Vector3.up, moveDirection);

            foreach (float sideOffset in SideOffsets)
            {
                Vector3 dir = (moveDirection + right * sideOffset).normalized;
                if (CheckAndClimbStep(rb.position, dir)) return;
            }
        }

        private bool CheckAndClimbStep(Vector3 origin, Vector3 direction)
        {
            Vector3 lowerOrigin = origin + Vector3.up * 0.05f;
            Vector3 upperOrigin = origin + Vector3.up * (maxStepHeight + 0.05f);

            bool stepFaceDetected = Physics.Raycast(lowerOrigin, direction, stepReachDistance, stepLayerMask);
            bool upperIsClear = !Physics.Raycast(upperOrigin, direction, stepReachDistance, stepLayerMask);

            if (!stepFaceDetected || !upperIsClear) return false;

            Vector3 topScanOrigin = origin + direction * (stepReachDistance * 0.8f) + Vector3.up * (maxStepHeight + 0.1f);

            if (!Physics.Raycast(topScanOrigin, Vector3.down, out RaycastHit topHit, maxStepHeight + 0.15f, stepLayerMask))
                return false;

            if (topHit.point.y <= origin.y + 0.01f) return false;
            
            _targetStepY = topHit.point.y;
            _climbTimer = 0f;
            rb.useGravity = false;
            return true;
        }

        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerState.OnPlayerDead  -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
            }
        }
        
    }
}