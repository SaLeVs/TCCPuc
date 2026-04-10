using Monster.HSM;
using Monster.MonsterStates.HuntStates;

namespace Monster.MonsterStates.ParentStates
{
    public class MonsterHunt : State
    {
        private readonly MonsterBrain _monsterBrain;
        public readonly ChaseState chaseState;
        public readonly AttackState attackState;
        
        public MonsterHunt(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
            chaseState = new ChaseState(stateMachine, this, monsterBrain);
            attackState = new AttackState(stateMachine, this, monsterBrain);
        }
    
        protected override State GetInitialState() => chaseState;
         // protected override State GetTransitionState() { }
    }
}