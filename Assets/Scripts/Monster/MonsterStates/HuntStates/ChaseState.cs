using Monster.HSM;

namespace Monster.MonsterStates.HuntStates
{
    public class ChaseState : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        public ChaseState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }
        
        // protected override State GetTransitionState() { } 
    }
}