using Monster.HSM;
using UnityEngine;

namespace Monster.MonsterStates.RoamingStates
{
    public class SabotageState : State
    {
        private readonly MonsterBrain _monsterBrain;

        public SabotageState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter()
        {
            // The type was already chosen by MonsterRoaming — it only requests this transition
            // once it knows something of that type is still intact.
            _monsterBrain.MonsterSabotage.Execute(_monsterBrain.MonsterSabotage.GetAvailableTargets());
        }

        /// <summary>Sabotage stands still: keep the agent stopped and put the clip back.</summary>
        protected override void OnResume()
        {
            _monsterBrain.NavMeshAgent.isStopped = true;
            _monsterBrain.MonsterSabotage.ReplayAnimation();
        }

        protected override void OnExit()
        {
            _monsterBrain.MonsterSabotage.EndSabotage();
        }
        
    }
}