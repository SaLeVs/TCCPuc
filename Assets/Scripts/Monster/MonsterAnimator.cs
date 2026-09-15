using Unity.Netcode.Components;
using UnityEngine;

namespace Monster
{
    public class MonsterAnimator : NetworkAnimator
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float transitionDuration = 0.15f;

        [Header("Foot sync")]
        [Tooltip("Metres per second the walk clip was authored for. The clip is time-scaled by " +
                 "how fast the monster is actually travelling, so the feet stop skating.")]
        [SerializeField, Min(0.01f)] private float walkClipSpeed = 1.5f;

        [Tooltip("Metres per second the run clip was authored for.")]
        [SerializeField, Min(0.01f)] private float runClipSpeed = 4f;

        [Tooltip("Above this speed the run clip is used instead of the walk clip.")]
        [SerializeField, Min(0f)] private float runThreshold = 2.5f;

        [Tooltip("How far the clip may be time-scaled. Too wide and a fast walk turns into a " +
                 "cartoon; too narrow and the skating comes back.")]
        [SerializeField] private Vector2 speedScaleClamp = new Vector2(0.6f, 1.8f);

        private Vector3 _lastPosition;
        private float _measuredSpeed;
        private bool _hasLastPosition;

        private readonly int _wanderState = Animator.StringToHash("Wander");
        private readonly int _chaseState = Animator.StringToHash("Chase");
        private readonly int _attackState = Animator.StringToHash("Attack");
        private readonly int _idleCombat = Animator.StringToHash("IdleCombat");
        private readonly int _idleState = Animator.StringToHash("Idle");
        private readonly int _sabotageState = Animator.StringToHash("Sabotage");
        private readonly int _searchStateLeft = Animator.StringToHash("SearchLeft");
        private readonly int _searchStateRight = Animator.StringToHash("SearchRight");
        
        private MonsterBrain _monsterBrain;


        public void Initialize(MonsterBrain brain)
        {
            _monsterBrain = brain;
            
            _monsterBrain.MonsterWander.OnStartedMovingAnimation += PlayWander;
            _monsterBrain.MonsterWander.OnStoppedMovingAnimation += PlayIdle;
            
            _monsterBrain.MonsterChase.OnStartedChasingAnimation += PlayChase;
            _monsterBrain.MonsterChase.OnStoppedChasingAnimation += PlayIdle;
            
            _monsterBrain.MonsterAttack.OnAttackStartedAnimation += PlayAttack;
            _monsterBrain.MonsterAttack.OnAttackEndedAnimation += PlayIdleCombat;
            
            _monsterBrain.MonsterSabotage.OnSabotageStartedAnimation += PlaySabotage;
            _monsterBrain.MonsterSabotage.OnSabotageEndedAnimation += PlayIdle;
            
            _monsterBrain.MonsterSearch.OnSearchStartedAnimation += PlaySearch;
            _monsterBrain.MonsterSearch.OnSearchEndedAnimation += PlayIdleCombat;
            _monsterBrain.MonsterSearch.OnStartedMovingAnimation += PlayLocomotionAt;

            if (_monsterBrain.MonsterInvestigate != null)
            {
                _monsterBrain.MonsterInvestigate.OnStartedMovingAnimation += PlayLocomotionAt;
                _monsterBrain.MonsterInvestigate.OnStoppedMovingAnimation += PlayIdleCombat;
            }

            if (_monsterBrain.MonsterDoorForcer != null)
            {
                // Forcing a door reuses the attack swipe.
                _monsterBrain.MonsterDoorForcer.OnDoorHitAnimation += PlayAttack;
                _monsterBrain.MonsterDoorForcer.OnDoorHitEndedAnimation += PlayAfterDoorHit;
            }
        }

        

        /// <summary>
        /// Keeps the clip's playback rate tied to how fast the monster is really travelling.
        ///
        /// <para>Measured from the transform rather than the NavMeshAgent on purpose: the agent
        /// only exists on the server, while every peer has to draw the feet. On a client this
        /// reads the interpolated network motion, which is exactly what the viewer sees.</para>
        /// </summary>
        private void LateUpdate()
        {
            if (animator == null) return;

            MeasureSpeed();

            int currentState = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            bool isLocomotion = currentState == _wanderState || currentState == _chaseState;

            if (!isLocomotion)
            {
                animator.speed = 1f;
                return;
            }

            float authored = currentState == _chaseState ? runClipSpeed : walkClipSpeed;

            animator.speed = Mathf.Clamp(_measuredSpeed / authored, speedScaleClamp.x, speedScaleClamp.y);
        }

        private void MeasureSpeed()
        {
            Vector3 position = transform.position;

            if (!_hasLastPosition)
            {
                _lastPosition = position;
                _hasLastPosition = true;
                return;
            }

            Vector3 delta = position - _lastPosition;
            delta.y = 0f;

            _lastPosition = position;

            if (Time.deltaTime <= 0f) return;

            // Smoothed: raw frame deltas jitter enough to make the clip rate visibly pulse.
            float instantSpeed = delta.magnitude / Time.deltaTime;
            _measuredSpeed = Mathf.Lerp(_measuredSpeed, instantSpeed, 10f * Time.deltaTime);
        }

        /// <summary>
        /// Picks walk or run from the speed the caller intends to travel at, so states that only
        /// say "I am moving now" do not each have to know which clip they want. Measured speed
        /// is useless here — the monster is still standing still at the moment it is called.
        /// </summary>
        private void PlayLocomotionAt(float intendedSpeed)
        {
            if (intendedSpeed >= runThreshold) PlayChase();
            else PlayWander();
        }

        private void PlayWander() => animator.CrossFade(_wanderState, transitionDuration);
        private void PlayChase() => animator.CrossFade(_chaseState, transitionDuration);
        private void PlayAttack() => animator.CrossFade(_attackState, transitionDuration);
        private void PlayIdle() => animator.CrossFade(_idleState, transitionDuration);
        private void PlayIdleCombat() => animator.CrossFade(_idleCombat, transitionDuration);
        private void PlaySabotage() => animator.CrossFade(_sabotageState, transitionDuration);
        private void PlaySearch(int direction) => animator.CrossFade(direction == 1 ? _searchStateLeft : _searchStateRight, transitionDuration);

        /// <summary>
        /// The door forcer runs outside the state machine, so no state re-enters afterwards to
        /// reassert its animation — without this the monster walks off still stuck on the swipe.
        /// </summary>
        private void PlayAfterDoorHit()
        {
            if (_monsterBrain.IsHunting) PlayChase();
            else PlayWander();
        }
        
        
        public void Uninitialize(MonsterBrain brain)
        {
            _monsterBrain.MonsterWander.OnStartedMovingAnimation -= PlayWander;
            _monsterBrain.MonsterWander.OnStoppedMovingAnimation -= PlayIdle;
            
            _monsterBrain.MonsterChase.OnStartedChasingAnimation -= PlayChase;
            _monsterBrain.MonsterChase.OnStoppedChasingAnimation -= PlayIdle;
            
            _monsterBrain.MonsterAttack.OnAttackStartedAnimation -= PlayAttack;
            _monsterBrain.MonsterAttack.OnAttackEndedAnimation -= PlayIdle;
            
            _monsterBrain.MonsterSabotage.OnSabotageStartedAnimation -= PlaySabotage;
            _monsterBrain.MonsterSabotage.OnSabotageEndedAnimation -= PlayIdle;
            
            _monsterBrain.MonsterSearch.OnSearchStartedAnimation -= PlaySearch;
            _monsterBrain.MonsterSearch.OnSearchEndedAnimation -= PlayIdleCombat;
            _monsterBrain.MonsterSearch.OnStartedMovingAnimation -= PlayLocomotionAt;

            if (_monsterBrain.MonsterInvestigate != null)
            {
                _monsterBrain.MonsterInvestigate.OnStartedMovingAnimation -= PlayLocomotionAt;
                _monsterBrain.MonsterInvestigate.OnStoppedMovingAnimation -= PlayIdleCombat;
            }

            if (_monsterBrain.MonsterDoorForcer != null)
            {
                _monsterBrain.MonsterDoorForcer.OnDoorHitAnimation -= PlayAttack;
                _monsterBrain.MonsterDoorForcer.OnDoorHitEndedAnimation -= PlayAfterDoorHit;
            }

            _monsterBrain = null;
        }
    }

}
