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

        protected override void OnInitialize()
        {
            _monsterBrain.MonsterSabotage.Initialize();
        }

        protected override void OnEnter()
        {
            _monsterBrain.MonsterSabotage.ChooseSabotageType();
            _monsterBrain.MonsterSabotage.Execute(_monsterBrain.MonsterSabotage.GetAvailableTargets());
        }
        
    }
}