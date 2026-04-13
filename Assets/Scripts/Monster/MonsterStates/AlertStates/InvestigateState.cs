using Monster.HSM;

namespace Monster.MonsterStates.AlertStates
{
    public class InvestigateState : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        public InvestigateState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }
        
        // protected override State GetTransitionState() { }
    }
}