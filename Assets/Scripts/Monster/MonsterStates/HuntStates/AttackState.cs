using Monster.HSM;

namespace Monster.MonsterStates.HuntStates
{
    public class AttackState : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        private float _timer;
        private float _attackCooldown;
        
        public AttackState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter()
        {
            _timer = 0f;
            _attackCooldown = _monsterBrain.MonsterAttack.AttackCooldown;
            _monsterBrain.MonsterAttack.StartAttack();
            _monsterBrain.MonsterAnimator.PlayAttack();
        }

        protected override void OnUpdate(float deltaTime)
        {
            _timer += deltaTime;

            if (_timer >= _attackCooldown)
            {
                _timer = 0f;
                _monsterBrain.MonsterAttack.StartAttack();
                _monsterBrain.MonsterAnimator.PlayAttack();
            }
        }

        // protected override State GetTransitionState() { } 
    }
}