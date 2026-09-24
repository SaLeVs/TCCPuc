using Monster.HSM;

namespace Monster.MonsterStates.AlertStates
{
    public class SearchState : State, IPathProblemHandler
    {
        private readonly MonsterBrain _monsterBrain;

        public SearchState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter()
        {
            _monsterBrain.MonsterSearch.Begin(_monsterBrain.MonsterAwareness.InvestigationPoint, _monsterBrain.MonsterChase.ChaseSpeed);
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (_monsterBrain._playersInVision.Count > 0)
            {
                StateMachine.Sequencer.RequestTransition(this, ((MonsterRoot)ParentState.ParentState).HuntState);
                return;
            }
            
            if (_monsterBrain.IsForcingDoor) return;

            _monsterBrain.MonsterSearch.Tick(deltaTime);

            if (_monsterBrain.MonsterSearch.IsFinished)
            {
                _monsterBrain.MonsterAwareness.Clear();
                StateMachine.Sequencer.RequestTransition(this, ((MonsterRoot)ParentState.ParentState).RoamingState);
            }
        }

        protected override void OnResume() => _monsterBrain.MonsterSearch.Resume();

        /// <summary>Cannot get to the last known position: sweep from here instead.</summary>
        public void OnPathProblem(PathProblem problem) => _monsterBrain.MonsterSearch.AbandonMove();

        protected override void OnExit()
        {
            _monsterBrain.MonsterSearch.Stop();
        }
        
    }
}