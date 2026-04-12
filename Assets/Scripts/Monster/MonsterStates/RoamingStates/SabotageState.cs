using Monster.HSM;
using Monster.MonsterSabotages;

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
            _monsterBrain.Sabotage.Initialize(_monsterBrain);
        }

        protected override void OnEnter()
        {
            SabotageTarget target = _monsterBrain.Sabotage.GetAvailableTarget(SabotageType.Light);
            
            if (target != null)
            {
                _monsterBrain.Sabotage.Execute(target);
            }
        }
        
        // protected override State GetTransitionState() { } 
    }
}