using Monster.HSM;
using Monster.MonsterStates.RoamingStates;

namespace Monster.MonsterStates.ParentStates
{
    public class MonsterRoaming : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        public readonly WanderState wanderState;
        public readonly SabotageState sabotageState;
        
        public MonsterRoaming(StateMachine stateMachine, State parentState ,MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
            wanderState = new WanderState(stateMachine, this, monsterBrain);
            sabotageState = new SabotageState(stateMachine, this, monsterBrain);
        }
        
        protected override State GetInitialState() => wanderState;
        // protected override State GetTransitionState() { } Add transition
    }
}