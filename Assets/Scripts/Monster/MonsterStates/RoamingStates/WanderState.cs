using Monster.HSM;
using UnityEngine;

namespace Monster.MonsterStates.RoamingStates
{
    public class WanderState : State
    {
        private readonly MonsterBrain _monsterBrain;
        
        public WanderState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }
        
        
        protected override void OnInitialize()
        {
            _monsterBrain.MonsterWander.Initialize(_monsterBrain.NavMeshAgent);
        }
        
        protected override void OnEnter()
        {
            _monsterBrain.MonsterWander.StartWander();
        }

        protected override void OnUpdate(float deltaTime)
        {
            _monsterBrain.MonsterWander.UpdateWander(deltaTime);
        }
        
        protected override void OnExit()
        {
            _monsterBrain.MonsterWander.StopWander();
        }
        
        // protected override State GetTransitionState() { } 
    }
}