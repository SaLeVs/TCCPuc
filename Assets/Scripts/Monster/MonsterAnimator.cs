using Unity.Netcode.Components;
using UnityEngine;

namespace Monster
{
    public class MonsterAnimator : NetworkAnimator
    {
        [SerializeField] private Animator animator;
    
        private readonly int _wanderState = Animator.StringToHash("Wander");
        private readonly int _chaseState = Animator.StringToHash("Chase");
        private readonly int _attackState = Animator.StringToHash("Attack");
        private readonly int _idleState = Animator.StringToHash("Idle");
        private readonly int _sabotageState = Animator.StringToHash("Sabotage");
        
        private MonsterBrain _monsterBrain;


        public void Initialize(MonsterBrain brain)
        {
            _monsterBrain = brain;
            
            _monsterBrain.MonsterWander.OnStartedMovingAnimation += PlayWander;
            _monsterBrain.MonsterWander.OnStoppedMovingAnimation += PlayIdle;
            
            
        }

        private void PlayWander() => animator.Play(_wanderState);
        private void PlayChase()  => animator.Play(_chaseState);
        private void PlayAttack() => animator.Play(_attackState);
        private void PlayIdle()   => animator.Play(_idleState);
        private void PlaySabotage() => animator.Play(_sabotageState);
        
        
        public void Uninitialize(MonsterBrain brain)
        {
            _monsterBrain.MonsterWander.OnStartedMovingAnimation -= PlayWander;
            _monsterBrain.MonsterWander.OnStoppedMovingAnimation -= PlayIdle;
            
            _monsterBrain = null;
        }
        
    }

}
