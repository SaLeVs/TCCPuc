using Monster.HSM;
using Monster.MonsterStates.HuntStates;

namespace Monster.MonsterStates.ParentStates
{
    public class MonsterHunt : State
    {
        private readonly MonsterBrain _monsterBrain;
        public readonly ChaseState chaseState;
        public readonly AttackState attackState;
        
        private float distanceToTarget;
        private float distanceToAttack;
        
        public MonsterHunt(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
            chaseState = new ChaseState(stateMachine, this, monsterBrain);
            attackState = new AttackState(stateMachine, this, monsterBrain);
        }
    
        protected override void OnEnter()
        {
            distanceToTarget = _monsterBrain.MonsterChase.DistanceFromTarget;
            distanceToAttack = _monsterBrain.MonsterAttack.DistanceToAttack;
            
        }

        protected override void OnUpdate(float deltaTime)
        {
            
        }

        protected override State GetTransitionState() => null;
    }
}