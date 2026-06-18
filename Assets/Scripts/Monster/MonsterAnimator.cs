using Unity.Netcode.Components;
using UnityEngine;

namespace Monster
{
    public class MonsterAnimator : NetworkAnimator
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float transitionDuration = 0.15f;
    
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
        }

        

        private void PlayWander() => animator.CrossFade(_wanderState, transitionDuration);
        private void PlayChase() => animator.CrossFade(_chaseState, transitionDuration);
        private void PlayAttack() => animator.CrossFade(_attackState, transitionDuration);
        private void PlayIdle() => animator.CrossFade(_idleState, transitionDuration);
        private void PlayIdleCombat() => animator.CrossFade(_idleCombat, transitionDuration);
        private void PlaySabotage() => animator.CrossFade(_sabotageState, transitionDuration);
        private void PlaySearch(int direction) => animator.CrossFade(direction == 1 ? _searchStateLeft : _searchStateRight, transitionDuration);
        
        
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
            
            _monsterBrain = null;
        }
    }

}
