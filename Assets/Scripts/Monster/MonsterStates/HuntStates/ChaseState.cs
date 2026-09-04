using Monster.HSM;

namespace Monster.MonsterStates.HuntStates
{
    public class ChaseState : State
    {
        private readonly MonsterBrain _monsterBrain;

        public ChaseState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter() => _monsterBrain.MonsterChase.StartChase();

        protected override void OnUpdate(float deltaTime)
        {
            // Re-pathing while the agent sits on a door link would cancel the traversal.
            if (_monsterBrain.IsForcingDoor) return;

            _monsterBrain.MonsterChase.ChaseUpdate(deltaTime);
        }

        protected override void OnExit() => _monsterBrain.MonsterChase.StopChase();
        
    }
}