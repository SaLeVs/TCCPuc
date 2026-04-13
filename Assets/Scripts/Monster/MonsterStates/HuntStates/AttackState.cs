using Monster.HSM;

namespace Monster.MonsterStates.HuntStates
{
    public class AttackState : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        public AttackState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }
        
        // protected override State GetTransitionState() { } 
    }
}