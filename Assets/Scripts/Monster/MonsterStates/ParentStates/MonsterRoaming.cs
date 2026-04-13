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
        
        private readonly float _minSabotageCooldown;
        private readonly float _maxSabotageCooldown;
        private readonly float _minSabotageStateDuration;
        private readonly float _maxSabotageStateDuration;
        
        private float _timer;
        private float _currentCooldown;
        private float _currentSabotageDuration;
        
        public MonsterRoaming(StateMachine stateMachine, State parentState ,MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
            wanderState = new WanderState(stateMachine, this, monsterBrain);
            sabotageState = new SabotageState(stateMachine, this, monsterBrain);
            
            _minSabotageCooldown = monsterBrain.MonsterSabotage.MinSabotageCooldown;
            _maxSabotageCooldown = monsterBrain.MonsterSabotage.MaxSabotageCooldown;
            _minSabotageStateDuration = monsterBrain.MonsterSabotage.MinSabotageStateDuration;
            _maxSabotageStateDuration = monsterBrain.MonsterSabotage.MaxSabotageStateDuration;
        }
        
        protected override State GetInitialState() => wanderState;

        protected override void OnEnter()
        {
            _timer = 0f;
            _currentCooldown = Random.Range(_minSabotageCooldown, _maxSabotageCooldown);
        }

        protected override void OnUpdate(float deltaTime)
        {
            _timer += deltaTime;

            if (ActiveChild == wanderState && _timer >= _currentCooldown)
            {
                _timer = 0f;
                _currentSabotageDuration = Random.Range(_minSabotageStateDuration, _maxSabotageStateDuration);
                StateMachine.Sequencer.RequestTransition(wanderState, sabotageState);
            }
            else if (ActiveChild == sabotageState && _timer >= _currentSabotageDuration)
            {
                _timer = 0f;
                _currentCooldown = Random.Range(_minSabotageCooldown, _maxSabotageCooldown);
                StateMachine.Sequencer.RequestTransition(sabotageState, wanderState);
            }
        }

        protected override State GetTransitionState() => null;
    }
}