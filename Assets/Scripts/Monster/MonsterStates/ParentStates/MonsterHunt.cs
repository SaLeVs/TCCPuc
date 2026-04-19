using Monster.HSM;
using Monster.MonsterStates.HuntStates;
using UnityEngine;

namespace Monster.MonsterStates.ParentStates
{
    public class MonsterHunt : State
    {
        private readonly MonsterBrain _monsterBrain;
        public readonly ChaseState chaseState;
        public readonly AttackState attackState;
        
        private float distanceToTarget;
        private float distanceToAttack;
        
        public MonsterHunt(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
            chaseState = new ChaseState(stateMachine, this, monsterBrain);
            attackState = new AttackState(stateMachine, this, monsterBrain);
        }
    
        protected override State GetInitialState() => chaseState;

        protected override void OnUpdate(float deltaTime)
        {
            _monsterBrain.MonsterChase.UpdateDistanceFromTarget();
            
            distanceToTarget = _monsterBrain.MonsterChase.DistanceFromTarget;
            distanceToAttack = _monsterBrain.MonsterAttack.DistanceToAttack;
            
            if (distanceToTarget <= distanceToAttack)
            {
                if (ActiveChild != attackState)
                {
                    StateMachine.Sequencer.RequestTransition(chaseState, attackState);
                }
            }
            else
            {
                if (ActiveChild != chaseState)
                {
                    StateMachine.Sequencer.RequestTransition(attackState, chaseState);
                }
            }
        }

        protected override State GetTransitionState() => null;
    }
}