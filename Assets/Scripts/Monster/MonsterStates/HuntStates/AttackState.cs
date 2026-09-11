using Monster.HSM;

namespace Monster.MonsterStates.HuntStates
{
    /// <summary>
    /// Swinging at someone in range.
    ///
    /// <para>The rhythm belongs to <see cref="MonsterAttack"/>, not to this state. This state is
    /// entered and left every time the player crosses the attack range, so anything stored here
    /// is wiped by a single step backwards — which is exactly how the cooldown used to be reset.
    /// It just asks to attack; the component decides whether it is allowed.</para>
    /// </summary>
    public class AttackState : State
    {
        private readonly MonsterBrain _monsterBrain;

        public AttackState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter()
        {
            _monsterBrain.MonsterAttack.TryStartAttack();
        }

        protected override void OnUpdate(float deltaTime)
        {
            // Cheap to ask every frame — refused while a swing is running or the cooldown is up.
            _monsterBrain.MonsterAttack.TryStartAttack();
        }

        protected override void OnExit()
        {
            _monsterBrain.MonsterAttack.CancelAttack();
        }
    }
}
