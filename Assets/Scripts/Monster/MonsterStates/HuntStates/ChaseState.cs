using Monster.HSM;

namespace Monster.MonsterStates.HuntStates
{
    public class ChaseState : State, IPathProblemHandler
    {
        private readonly MonsterBrain _monsterBrain;

        public ChaseState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter() => _monsterBrain.MonsterChase.StartChase();

        protected override void OnUpdate(float deltaTime)
        {
            // The door forcer owns the agent while it walks up to a door and swipes at it.
            // Re-pathing at the target here would pull the monster off its spot in front of the leaf.
            if (_monsterBrain.IsForcingDoor) return;

            _monsterBrain.MonsterChase.ChaseUpdate(deltaTime);
        }

        protected override void OnResume() => _monsterBrain.MonsterChase.StartChase();

        public void OnPathProblem(PathProblem problem) => _monsterBrain.MonsterChase.HoldUnreachable();

        protected override void OnExit() => _monsterBrain.MonsterChase.StopChase();

    }
}
