using Monster.HSM;

namespace Monster.MonsterStates.RoamingStates
{
    public class WanderState : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        public WanderState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }
        
        // protected override State GetTransitionState() { } 
    }
}