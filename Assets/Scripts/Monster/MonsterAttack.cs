using System;
using Components.Sound;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterAttack : NetworkBehaviour
    {
        public event Action OnAttackStartedAnimation;
        public event Action OnAttackEndedAnimation;

        [SerializeField] private SoundDefinitionSO attackSound;

        [Tooltip("Centre-to-centre distance at which a swing starts. The swing only lands if the " +
                 "hand's hitbox really reaches that far, so grow the hitbox together with this.")]
        [SerializeField] private float distanceToAttack;
        [SerializeField] private float damageAmountPerAttack;

        [Tooltip("Seconds after a swing ends before the next one may start. It keeps chasing meanwhile.")]
        [SerializeField] private float attackCooldown;

        [Tooltip("Seconds it stands swinging with the hitbox live. Keep it at the punch clip's length: " +
                 "the clip loops, so anything longer throws a second punch inside the same swing.")]
        [SerializeField] private float attackDuration;

        [Tooltip("Seconds into the swing when the hitbox goes live. Matches the hook of the punch " +
                 "clip, so the wind-up hurts nobody.")]
        [SerializeField, Min(0f)] private float hitWindowStart = 0.15f;

        [Tooltip("Seconds into the swing when the hitbox goes off again, before the arm swings back.")]
        [SerializeField, Min(0f)] private float hitWindowEnd = 0.5f;
        [SerializeField] private MonsterHitbox hitbox;

        [Header("Slow after a hit")]
        [Tooltip("Speed multiplier once a swing lands, applied on top of whatever the active state " +
                 "asked for. Gives whoever was hit a head start instead of a monster still glued to " +
                 "them the moment the cooldown is over.")]
        [SerializeField, Range(0.1f, 1f)] private float hitSlowMultiplier = 0.6f;

        [Tooltip("Seconds it stays at that speed after landing a hit.")]
        [SerializeField, Min(0f)] private float hitSlowSeconds = 2.5f;

        [Tooltip("Seconds it then takes to ease back up to full speed.")]
        [SerializeField, Min(0f)] private float hitSlowRecoverSeconds = 1.5f;

        [Tooltip("Slow down after a swing that missed as well, not only after one that landed.")]
        [SerializeField] private bool hitSlowOnMiss;

        public float DistanceToAttack => distanceToAttack;
        public float AttackCooldown => attackCooldown;

        /// <summary>
        /// True while the monster is still recovering from its last swing.
        ///
        /// <para>The cooldown lives here, on the component, and not in AttackState — that state is
        /// entered and left every time the player crosses the attack range, so a cooldown stored
        /// there was wiped on the way out. Stepping back and forward granted a free hit every
        /// time, which made dancing in and out strictly better than standing still.</para>
        /// </summary>
        public bool IsOnCooldown => _cooldownRemaining > 0f;

        /// <summary>Mid-swing. Anything that wants the monster's attention waits for this.</summary>
        public bool IsAttacking => _isAttacking;

        public float CooldownRemaining => _cooldownRemaining;

        private float _timer;
        private float _cooldownRemaining;
        private bool _isAttacking;
        private NavMeshAgent _agent;
        private ObstacleAvoidanceType _defaultAvoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        private bool _hitboxLive;
        private bool _swingLanded;
        private bool _isSlowed;
        private float _slowElapsed;
        private float _speedBeforeSlow;
        private float _slowedSpeed;


        public override void OnNetworkSpawn()
        {
            hitbox.Initialize(damageAmountPerAttack);
            hitbox.DisableHitbox();
            hitbox.OnTargetHit += Hitbox_OnTargetHit;
        }

        public override void OnNetworkDespawn()
        {
            hitbox.OnTargetHit -= Hitbox_OnTargetHit;
        }

        private void Hitbox_OnTargetHit()
        {
            _swingLanded = true;
            StartSlow();
        }

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
            _defaultAvoidance = agent.obstacleAvoidanceType;
        }

        /// <summary>
        /// Swings, unless already swinging or still on cooldown. Returns whether it actually
        /// started, so callers can keep asking every frame without having to track the rhythm.
        /// </summary>
        public bool TryStartAttack()
        {
            if (_isAttacking) return false;
            if (_cooldownRemaining > 0f) return false;

            _timer = 0f;
            _isAttacking = true;
            _swingLanded = false;

            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            // The attack is decided here, on the server; this used to raise a local event, so only
            // the host ever heard the monster swing.
            WorldSound.Play(attackSound, transform.position, NetworkObject);
            OnAttackStartedAnimation?.Invoke();
            hitbox.ResetHits();
            _hitboxLive = false;

            return true;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);
            }

            if (_isSlowed) TickSlow(Time.deltaTime);

            if (!_isAttacking) return;

            _timer += Time.deltaTime;

            // Live only through the hook itself. On from the first frame, it landed before the fist
            // had even moved, and the arm swinging back gave a second go at anyone it had missed.
            bool inHitWindow = _timer >= hitWindowStart && _timer < hitWindowEnd;

            if (inHitWindow != _hitboxLive)
            {
                _hitboxLive = inHitWindow;

                if (inHitWindow) hitbox.EnableHitbox();
                else hitbox.DisableHitbox();
            }

            if (_timer >= attackDuration)
            {
                EndAttack();
            }
        }

        private void EndAttack()
        {
            OnAttackEndedAnimation?.Invoke();
            _isAttacking = false;
            _cooldownRemaining = attackCooldown;
            _agent.isStopped = false;

            if (hitSlowOnMiss && !_swingLanded) StartSlow();

            // Was setting NoObstacleAvoidance again, same as StartAttack — so the first attack
            // switched avoidance off for good and the monster stopped steering around anything.
            _agent.obstacleAvoidanceType = _defaultAvoidance;

            hitbox.DisableHitbox();
        }

        public void CancelAttack()
        {
            bool wasSwinging = _isAttacking;

            _isAttacking = false;
            hitbox.DisableHitbox();
            _agent.isStopped = false;
            _agent.obstacleAvoidanceType = _defaultAvoidance;

            // An interrupted swing still costs a cooldown, otherwise stepping out mid-animation
            // is the same free-hit exploit by another route. But leaving the state while already
            // cooling down must not top the timer back up.
            if (wasSwinging) _cooldownRemaining = attackCooldown;

            if (wasSwinging && hitSlowOnMiss && !_swingLanded) StartSlow();
        }

        /// <summary>
        /// Starts the post-hit slow, or restarts it if it is already running. Server only, like the
        /// swing: the agent only moves there, and clients see the slower pace through the
        /// replicated transform.
        /// </summary>
        private void StartSlow()
        {
            if (!IsServer || _agent == null) return;
            if (hitSlowMultiplier >= 1f || hitSlowSeconds + hitSlowRecoverSeconds <= 0f) return;

            if (!_isSlowed)
            {
                _speedBeforeSlow = _agent.speed;
                _slowedSpeed = _agent.speed;
            }

            _isSlowed = true;
            _slowElapsed = 0f;
        }

        /// <summary>
        /// Scales whatever speed the active state set. States only write agent.speed when they
        /// start (StartChase, Search, Investigate, Wander), so a speed other than the one this last
        /// wrote is a fresh base from a state and gets scaled in turn. Overwriting chaseSpeed
        /// itself instead would have left the monster crawling until the next state change.
        /// </summary>
        private void TickSlow(float deltaTime)
        {
            if (!Mathf.Approximately(_agent.speed, _slowedSpeed)) _speedBeforeSlow = _agent.speed;

            _slowElapsed += deltaTime;

            if (_slowElapsed >= hitSlowSeconds + hitSlowRecoverSeconds)
            {
                _isSlowed = false;
                _agent.speed = _speedBeforeSlow;
                return;
            }

            float recovered = hitSlowRecoverSeconds > 0f
                ? Mathf.Clamp01((_slowElapsed - hitSlowSeconds) / hitSlowRecoverSeconds)
                : 0f;

            _slowedSpeed = _speedBeforeSlow * Mathf.Lerp(hitSlowMultiplier, 1f, recovered);
            _agent.speed = _slowedSpeed;
        }

    }
}