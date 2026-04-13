using Monster.HSM;
using Monster.MonsterStates.ParentStates;

namespace Monster.MonsterStates
{
    public class MonsterRoot : State
    {
        public readonly State RoamingState;
        public readonly State AlertState;
        public readonly State HuntState;
        private readonly MonsterBrain _monsterBrain;
        
        public MonsterRoot(StateMachine stateMachine, MonsterBrain brain) : base(stateMachine, null)
        {
            _monsterBrain  = brain;
            
            RoamingState = new MonsterRoaming(stateMachine, this, brain);
            AlertState = new MonsterAlert(stateMachine, this, brain);
            HuntState = new MonsterHunt(stateMachine, this, brain);
        }

        protected override State GetInitialState() => RoamingState;
        // protected override State GetTransitionState() { } Need to add Transitions here for Roaming, alert and hunt
    }
}