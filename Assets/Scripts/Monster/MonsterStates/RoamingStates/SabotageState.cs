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

        protected override void OnExit()
        {
            _monsterBrain.MonsterSabotage.EndSabotage();
        }
        
    }
}