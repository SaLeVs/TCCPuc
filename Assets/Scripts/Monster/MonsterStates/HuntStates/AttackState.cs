using Monster.HSM;

namespace Monster.MonsterStates.HuntStates
{
    public class AttackState : State
    {
        private readonly MonsterBrain _monsterBrain;

        private float _cooldownTimer;
        private float _attackCooldown;
        private bool _waitingForCooldown;

        public AttackState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter()
        {
            _attackCooldown = _monsterBrain.MonsterAttack.AttackCooldown;
            _cooldownTimer = 0f;
            _waitingForCooldown = false;
            
            _monsterBrain.MonsterAttack.OnAttackEndedAnimation += OnAttackEnded;
            _monsterBrain.MonsterAttack.StartAttack();
        }
        
        private void OnAttackEnded()
        {
            _waitingForCooldown = true;
            _cooldownTimer = 0f;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!_waitingForCooldown) return;

            _cooldownTimer += deltaTime;

            if (_cooldownTimer >= _attackCooldown)
            {
                _waitingForCooldown = false;
                _monsterBrain.MonsterAttack.StartAttack();
            }
        }

        protected override void OnExit()
        {
            _monsterBrain.MonsterAttack.OnAttackEndedAnimation -= OnAttackEnded;
            _monsterBrain.MonsterAttack.CancelAttack();
        }

        // protected override State GetTransitionState() { } 
    }
}