using System.Linq;
using Monster.HSM;
using Monster.MonsterStates.ParentStates;

namespace Monster.MonsterStates
{
    public class MonsterRoot : State
    {
        public readonly State RoamingState;
        public readonly State AlertState;
        public readonly State HuntState;
        private readonly MonsterBrain _monsterBrain;
        
        public MonsterRoot(StateMachine stateMachine, MonsterBrain brain) : base(stateMachine, null)
        {
            _monsterBrain  = brain;
            
            RoamingState = new MonsterRoaming(stateMachine, this, brain);
            AlertState = new MonsterAlert(stateMachine, this, brain);
            HuntState = new MonsterHunt(stateMachine, this, brain);
        }

        protected override State GetInitialState() => RoamingState;

        protected override State GetTransitionState()
        {
            bool hasTargets = _monsterBrain._playersInVision.Any(playerTarget => playerTarget);
            
            if (hasTargets && ActiveChild != HuntState)
            {
                return HuntState;
            }
            
            if (!hasTargets && ActiveChild != RoamingState)
            {
                return RoamingState;
            }

            return null;  
        } 
    }
}