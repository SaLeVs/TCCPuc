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

        protected override void OnEnter()
        {
            _monsterBrain.MonsterChase.StartChase();
            _monsterBrain.MonsterAnimator.PlayChase();
        }

        protected override void OnUpdate(float deltaTime)
        {
            _monsterBrain.MonsterChase.ChaseUpdate();
        }

        protected override void OnExit()
        {
            _monsterBrain.MonsterChase.StopChase();
            _monsterBrain.MonsterAnimator.PlayIdle();
        }
        
    }
}