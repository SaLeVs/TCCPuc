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
            return _monsterBrain.MonsterAwareness.Level == AwarenessLevel.Alerted ? searchState : investigateState;
        }

        /// <summary>
        /// Once it is committed to going and looking, the suspicion meter stops draining. The
        /// child states own the ending: Investigate gives up after its look-around, Search after
        /// its sweep, and both call Clear() on the way out. Without this the meter ran out
        /// mid-walk and the root pulled the monster back to Roaming before it ever arrived.
        /// </summary>
        protected override void OnEnter() => _monsterBrain.MonsterAwareness.Hold();

        protected override void OnExit() => _monsterBrain.MonsterAwareness.Release();

        protected override void OnUpdate(float deltaTime)
        {
            if (ActiveChild == investigateState && _monsterBrain.MonsterAwareness.Level == AwarenessLevel.Alerted)
            {
                StateMachine.Sequencer.RequestTransition(investigateState, searchState);
            }
        }

        protected override State GetTransitionState() => null;
        
    }
}
