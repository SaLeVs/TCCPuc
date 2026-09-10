using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    /// <summary>
    /// Movement for "I think I heard something over there".
    ///
    /// <para>Sits between wandering and searching: quicker than a patrol so a noise actually
    /// costs the player something, slower than a chase so they still get a chance to move.
    /// Separated from <see cref="MonsterStates.AlertStates.InvestigateState"/> for the same
    /// reason <see cref="MonsterSearch"/> is separate from its state — the state decides
    /// <i>when</i>, this decides <i>how fast and how</i>.</para>
    /// </summary>
    public class MonsterInvestigate : NetworkBehaviour
    {
        [Tooltip("Speed while walking over to a noise. Should sit above the wander speed and " +
                 "below the chase speed.")]
        [SerializeField, Min(0f)] private float investigateSpeed = 3.5f;

        [Tooltip("How close counts as arrived.")]
        [SerializeField, Min(0.1f)] private float destinationTolerance = 0.75f;

        [Tooltip("Seconds spent looking around after arriving before giving up.")]
        [SerializeField, Min(0f)] private float lookAroundSeconds = 1.5f;

        [SerializeField] private float lookRotationSpeed = 8f;

        public bool IsFinished => _phase == InvestigatePhase.Finished;
        public bool HasArrived => _phase != InvestigatePhase.MovingToPoint;
        public float InvestigateSpeed => investigateSpeed;

        private NavMeshAgent _agent;
        private InvestigatePhase _phase;
        private float _lookTimer;
        private Vector3 _point;

        private enum InvestigatePhase
        {
            MovingToPoint,
            LookingAround,
            Finished
        }

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
        }

        public void Begin(Vector3 point)
        {
            if (_agent == null) return;

            _point = point;
            _phase = InvestigatePhase.MovingToPoint;
            _lookTimer = 0f;

            _agent.isStopped = false;
            _agent.speed = investigateSpeed;
            _agent.updateRotation = false;
            _agent.SetDestination(point);
        }

        /// <summary>
        /// Moves the goalposts to a fresher noise without restarting the look-around timer.
        /// Ignored once it has arrived — otherwise a trickle of distant noise would keep it
        /// walking forever instead of ever giving up.
        /// </summary>
        public void Retarget(Vector3 point)
        {
            if (_agent == null) return;
            if (_phase != InvestigatePhase.MovingToPoint) return;
            if (Vector3.Distance(_point, point) <= destinationTolerance) return;

            _point = point;
            _agent.SetDestination(point);
        }

        public void Tick(float deltaTime)
        {
            if (_agent == null) return;

            switch (_phase)
            {
                case InvestigatePhase.MovingToPoint:
                    UpdateMove();
                    break;

                case InvestigatePhase.LookingAround:
                    UpdateLookAround(deltaTime);
                    break;
            }
        }

        private void UpdateMove()
        {
            RotateTowardsMovement();

            if (_agent.pathPending) return;
            if (_agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, destinationTolerance)) return;

            _agent.isStopped = true;
            _agent.ResetPath();

            _phase = InvestigatePhase.LookingAround;
            _lookTimer = 0f;
        }

        private void RotateTowardsMovement()
        {
            Vector3 direction = _agent.desiredVelocity;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lookRotationSpeed * Time.deltaTime);
        }

        private void UpdateLookAround(float deltaTime)
        {
            _lookTimer += deltaTime;

            if (_lookTimer < lookAroundSeconds) return;

            _phase = InvestigatePhase.Finished;
        }

        public void Stop()
        {
            _phase = InvestigatePhase.Finished;

            if (_agent == null) return;

            _agent.updateRotation = true;
            _agent.isStopped = false;
        }
    }
}
