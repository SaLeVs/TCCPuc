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
        
        public void PlayWander() => animator.Play(_wanderState);
        public void PlayChase()  => animator.Play(_chaseState);
        public void PlayAttack() => animator.Play(_attackState);
        public void PlayIdle()   => animator.Play(_idleState);
        public void PlaySabotage() => animator.Play(_sabotageState);
        
    }

}
