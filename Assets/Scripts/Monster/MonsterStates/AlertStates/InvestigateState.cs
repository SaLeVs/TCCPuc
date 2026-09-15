using Monster.HSM;

namespace Monster.MonsterStates.AlertStates
{
    /// <summary>
    /// "I think I heard something." The low half of Alert.
    ///
    /// <para>Walks over to where the noise seemed to come from and keeps listening on the way.
    /// It is the state that gives players a chance: you hear it coming, and you get to decide
    /// whether to freeze or move. If more noise arrives while it walks, awareness climbs and
    /// <see cref="ParentStates.MonsterAlert"/> promotes it to <see cref="SearchState"/>; if the
    /// walk finishes quietly, it gives up and goes back to roaming.</para>
    /// </summary>
    public class InvestigateState : State
    {
        private readonly MonsterBrain _monsterBrain;

        public InvestigateState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter()
        {
            _monsterBrain.MonsterInvestigate.Begin(_monsterBrain.MonsterAwareness.InvestigationPoint);
        }

        protected override void OnUpdate(float deltaTime)
        {
            // Anything it can actually see beats anything it thinks it heard.
            if (_monsterBrain._playersInVision.Count > 0)
            {
                StateMachine.Sequencer.RequestTransition(this, ((MonsterRoot)ParentState.ParentState).HuntState);
                return;
            }

            // Held up at a door: let the forcer finish before advancing.
            if (_monsterBrain.IsForcingDoor) return;

            // A fresher noise moves the goalposts — walk to where the sound is now, not to the
            // stale point it started from.
            _monsterBrain.MonsterInvestigate.Retarget(_monsterBrain.MonsterAwareness.InvestigationPoint);

            _monsterBrain.MonsterInvestigate.Tick(deltaTime);

            if (!_monsterBrain.MonsterInvestigate.IsFinished) return;

            // Nothing here. Settle down rather than bouncing straight back into Alert.
            _monsterBrain.MonsterAwareness.Clear();
            StateMachine.Sequencer.RequestTransition(this, ((MonsterRoot)ParentState.ParentState).RoamingState);
        }

        protected override void OnExit()
        {
            _monsterBrain.MonsterInvestigate.Stop();
        }

    }
}
