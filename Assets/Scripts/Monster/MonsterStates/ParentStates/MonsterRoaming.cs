using Monster.HSM;
using Monster.MonsterStates.RoamingStates;
using UnityEngine;

namespace Monster.MonsterStates.ParentStates
{
    public class MonsterRoaming : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        public readonly WanderState wanderState;
        public readonly SabotageState sabotageState;
        
        private readonly float _minTimeToSabotage;
        private readonly float _maxTimeToSabotage;

        private float _sabotageTimer;
        private float _sabotageDuration;
        
        public MonsterRoaming(StateMachine stateMachine, State parentState ,MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
            wanderState = new WanderState(stateMachine, this, monsterBrain);
            sabotageState = new SabotageState(stateMachine, this, monsterBrain);
            
            _minTimeToSabotage = monsterBrain.MonsterSabotage.MinTimeToSabotage;
            _maxTimeToSabotage = monsterBrain.MonsterSabotage.MaxTimeToSabotage;
        }
        
        protected override State GetInitialState() => wanderState;

        protected override void OnEnter()
        {
            _sabotageTimer = 0f;
            _sabotageDuration = Random.Range(_minTimeToSabotage, _maxTimeToSabotage);
        }

        protected override void OnUpdate(float deltaTime)
        {
            _sabotageTimer += deltaTime;
        }

        protected override State GetTransitionState()
        {
            if (_sabotageTimer >= _sabotageDuration)
            {
                _sabotageTimer = 0f;
                _sabotageDuration = Random.Range(_minTimeToSabotage, _maxTimeToSabotage);

                if (ActiveChild == wanderState)
                {
                    return sabotageState;
                }
                
                if (ActiveChild == sabotageState)
                {
                    return wanderState;
                }
            }

            return null;
        }
    }
}