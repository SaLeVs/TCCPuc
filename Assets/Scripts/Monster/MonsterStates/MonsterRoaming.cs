using Monster.HSM;

namespace Monster.MonsterStates
{
    public class MonsterRoaming : State
    {
        private readonly MonsterBrain _monsterBrain;
        public readonly WanderState wanderState;
        public readonly 
        
        public MonsterRoaming(StateMachine stateMachine, State parentState ,MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
        }

        protected override void OnEnter()
        {
            
        }
    }
}