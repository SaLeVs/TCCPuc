using Monster.HSM;

namespace Monster.MonsterStates.RoamingStates
{
    public class SabotageState : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        public SabotageState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }
        
        // protected override State GetTransitionState() { } 
    }
}