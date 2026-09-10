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
            bool hunting = _monsterBrain._playersInVision.Count > 0 || _monsterBrain.IsTrackingLostTarget;

            if (hunting)
            {
                return ActiveChild != HuntState ? HuntState : null;
            }

            // A swing already in progress finishes. Nothing below this line is urgent enough to
            // justify cutting one short, and cutting it strands the hitbox and the animation
            // half-applied. The guard used to sit inside the Alert branch only, so losing the
            // target and the suspicion in the same frame still yanked the monster into Roaming
            // mid-attack.
            if (IsAttacking()) return null;

            if (_monsterBrain.MonsterAwareness.ShouldInvestigate)
            {
                return ActiveChild != AlertState ? AlertState : null;
            }

            return ActiveChild != RoamingState ? RoamingState : null;
        }

        private bool IsAttacking()
        {
            if (ActiveChild != HuntState) return false;

            MonsterHunt hunt = (MonsterHunt)HuntState;
            return hunt.ActiveChild == hunt.attackState;
        }
    }
}