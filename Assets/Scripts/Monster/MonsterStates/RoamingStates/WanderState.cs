using Monster.HSM;
using UnityEngine;

namespace Monster.MonsterStates.RoamingStates
{
    public class WanderState : State, IPathProblemHandler
    {
        private readonly MonsterBrain _monsterBrain;
        
        public WanderState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }
        
        protected override void OnEnter()
        {
            _monsterBrain.MonsterWander.StartWander();
        }

        protected override void OnUpdate(float deltaTime)
        {
            // Held up at a door: the forcer owns the agent and is driving the rotation itself.
            // Without this the wander timer could hand the agent a new destination mid-swipe and
            // walk the monster away from a door the forcer still thinks it is holding.
            // ChaseState, SearchState and InvestigateState all guard the same way.
            if (_monsterBrain.IsForcingDoor) return;

            _monsterBrain.MonsterWander.UpdateWander(deltaTime);
        }
        
        protected override void OnResume() => _monsterBrain.MonsterWander.Resume();

        /// <summary>This leg cannot be finished: count it as walked and pick another point.</summary>
        public void OnPathProblem(PathProblem problem) => _monsterBrain.MonsterWander.AbandonLeg();

        protected override void OnExit()
        {
            _monsterBrain.MonsterWander.StopWander();
        }
        
    }
}