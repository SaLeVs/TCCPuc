using System;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    /// <summary>
    /// The monster stopping to mock a player who just went down in front of it — killed, or
    /// knocked over.
    ///
    /// <para>Borrows the agent the way the door forcer does: <see cref="MonsterBrain"/> stops
    /// ticking the state machine while it runs and resumes the active state when it ends, so the
    /// state puts its own movement and animation back. A request that arrives mid-swing or at a
    /// door waits for that to finish, and is dropped if it has to wait too long or if someone else
    /// is in sight — the hunt comes first.</para>
    /// </summary>
    public class MonsterTaunt
    {
        public event Action OnTauntAnimation;

        /// <summary>The taunt ended and the active state should take the agent back.</summary>
        public event Action OnFinished;

        private readonly NavMeshAgent _agent;
        private readonly Transform _body;
        private readonly float _duration;
        private readonly float _maxDelay;
        private readonly float _turnSpeed;

        private bool _pending;
        private float _pendingUntil;
        private float _endTime;
        private Vector3 _victimPosition;

        public bool IsTaunting { get; private set; }

        /// <summary>Who it is mocking, or about to. Null when idle.</summary>
        public Transform Victim { get; private set; }

        public MonsterTaunt(NavMeshAgent agent, Transform body, float duration, float maxDelay, float turnSpeed)
        {
            _agent = agent;
            _body = body;
            _duration = duration;
            _maxDelay = maxDelay;
            _turnSpeed = turnSpeed;
        }

        public void Request(Transform victim, float now)
        {
            if (IsTaunting || victim == null) return;

            _pending = true;
            _pendingUntil = now + _maxDelay;

            Victim = victim;
            _victimPosition = victim.position;
        }

        /// <param name="busy">A swing or a door is in progress: wait for it rather than cut it off.</param>
        /// <param name="someoneElseInView">A live player other than the victim is in sight.</param>
        public void Tick(float now, float deltaTime, bool busy, bool someoneElseInView)
        {
            if (IsTaunting)
            {
                if (someoneElseInView || now >= _endTime)
                {
                    Finish();
                    return;
                }

                HoldAgent();
                FaceVictim(deltaTime);
                return;
            }

            if (!_pending) return;

            if (someoneElseInView || now > _pendingUntil)
            {
                _pending = false;
                Victim = null;
                return;
            }

            if (busy) return;

            Begin(now);
        }

        private void Begin(float now)
        {
            _pending = false;
            IsTaunting = true;
            _endTime = now + _duration;

            if (_agent.isOnNavMesh) _agent.ResetPath();
            HoldAgent();

            OnTauntAnimation?.Invoke();
        }

        private void Finish()
        {
            IsTaunting = false;
            Victim = null;

            OnFinished?.Invoke();
        }

        /// <summary>
        /// Every frame, not once: tracking going cold mid-taunt reaches straight into the agent
        /// through MonsterChase, and nothing may walk the monster off in the middle of it.
        /// </summary>
        private void HoldAgent()
        {
            if (!_agent.isOnNavMesh) return;

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.updateRotation = false;
        }

        private void FaceVictim(float deltaTime)
        {
            Vector3 direction = _victimPosition - _body.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f) return;

            Quaternion target = Quaternion.LookRotation(direction);
            _body.rotation = Quaternion.Slerp(_body.rotation, target, _turnSpeed * deltaTime);
        }
    }
}
