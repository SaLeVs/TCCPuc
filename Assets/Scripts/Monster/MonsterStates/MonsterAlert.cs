using Monster.HSM;

namespace Monster.MonsterStates
{
    public class MonsterAlert : State
    {
        private readonly MonsterBrain _monsterBrain;
        
            
        public MonsterAlert(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }
    
        protected override void OnEnter()
        {
                
        }
            
    }
}