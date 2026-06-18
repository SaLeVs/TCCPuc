using Monster.HSM;

namespace Monster.MonsterStates.AlertStates
{
    public class SearchState : State
    {
        private readonly MonsterBrain _monsterBrain;
        private bool _destinationSet;

        public SearchState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter()
        {
            _monsterBrain.MonsterSearch.Begin(_monsterBrain.LastKnownTargetPosition, _monsterBrain.MonsterChase.ChaseSpeed);
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (_monsterBrain._playersInVision.Count > 0)
            {
                StateMachine.Sequencer.RequestTransition(this, ((MonsterRoot)ParentState.ParentState).HuntState);
                return;
            }

            _monsterBrain.MonsterSearch.Tick(deltaTime);

            if (_monsterBrain.MonsterSearch.IsFinished)
            {
                _monsterBrain.ShouldEnterAlert = false;
                StateMachine.Sequencer.RequestTransition(this, ((MonsterRoot)ParentState.ParentState).RoamingState);
            }
        }

        protected override void OnExit()
        {
            _monsterBrain.MonsterSearch.Stop();
        }
    }
}