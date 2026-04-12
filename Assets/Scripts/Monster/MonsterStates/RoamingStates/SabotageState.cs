using Monster.HSM;
using Monster.MonsterSabotages;

namespace Monster.MonsterStates.RoamingStates
{
    public class SabotageState : State
    {
        private readonly MonsterBrain _monsterBrain;

        public SabotageState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnInitialize()
        {
            
        }

        protected override void OnEnter()
        {
            
        }
        
        // protected override State GetTransitionState() { } 
    }
}