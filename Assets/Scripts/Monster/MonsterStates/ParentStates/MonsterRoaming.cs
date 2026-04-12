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
        private readonly float _minSabotageCooldown;
        private readonly float _maxSabotageCooldown;

        private float _timer;
        private float _transitionDuration;
        
        public MonsterRoaming(StateMachine stateMachine, State parentState ,MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
            
            wanderState = new WanderState(stateMachine, this, monsterBrain);
            sabotageState = new SabotageState(stateMachine, this, monsterBrain);
            
            _minTimeToSabotage = monsterBrain.MonsterSabotage.MinTimeToSabotage;
            _maxTimeToSabotage = monsterBrain.MonsterSabotage.MaxTimeToSabotage;
            
            _minSabotageCooldown = monsterBrain.MonsterSabotage.MinSabotageCooldown;
            _maxSabotageCooldown = monsterBrain.MonsterSabotage.MaxSabotageCooldown;
        }
        
        protected override State GetInitialState() => wanderState;

        protected override void OnEnter()
        {
            _timer = 0f;
            _transitionDuration = Random.Range(_minTimeToSabotage, _maxTimeToSabotage);
        }

        protected override void OnUpdate(float deltaTime)
        {
            _timer += deltaTime;
        }

        protected override State GetTransitionState()
        {
            if (_timer >= _transitionDuration)
            {
                _timer = 0f;

                if (ActiveChild == wanderState)
                {
                    _transitionDuration = Random.Range(_minSabotageCooldown, _maxSabotageCooldown);
                    Debug.Log("Transitioning to Sabotage State");
                    return sabotageState;
                }

                if (ActiveChild == sabotageState)
                {
                    _transitionDuration = Random.Range(_minTimeToSabotage, _maxTimeToSabotage);
                    Debug.Log("Transitioning to wander State");
                    return wanderState;
                }
            }

            return null;
        }
    }
}