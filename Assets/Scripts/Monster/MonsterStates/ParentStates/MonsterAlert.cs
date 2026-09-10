using Monster.HSM;
using Monster.MonsterStates.AlertStates;

namespace Monster.MonsterStates.ParentStates
{
    public class MonsterAlert : State
    {
        private readonly MonsterBrain _monsterBrain;
        public readonly InvestigateState investigateState;
        public readonly SearchState searchState;

        public MonsterAlert(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;

            investigateState = new InvestigateState(stateMachine, this, monsterBrain);
            searchState = new SearchState(stateMachine, this, monsterBrain);
        }

        protected override State GetInitialState()
        {
            return _monsterBrain.Awareness == AwarenessLevel.Alerted ? searchState : investigateState;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (ActiveChild == investigateState && _monsterBrain.Awareness == AwarenessLevel.Alerted)
            {
                StateMachine.Sequencer.RequestTransition(investigateState, searchState);
            }
        }

        protected override State GetTransitionState() => null;
        
    }
}
