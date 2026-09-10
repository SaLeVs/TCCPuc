using Monster.HSM;
using UnityEngine;
using UnityEngine.AI;

namespace Monster.MonsterStates.AlertStates
{
    public class InvestigateState : State
    {
        private readonly MonsterBrain _monsterBrain;

        private NavMeshAgent _agent;
        private float _idleTimer;
        private bool _arrived;

        private const float ARRIVAL_TOLERANCE = 0.75f;
        private const float LOOK_AROUND_SECONDS = 1.5f;
        
        private const float WALK_SPEED_FACTOR = 0.45f;

        public InvestigateState(StateMachine stateMachine, State parentState, MonsterBrain monsterBrain) : base(stateMachine, parentState)
        {
            _monsterBrain = monsterBrain;
        }

        protected override void OnEnter()
        {
            _arrived = false;
            _idleTimer = 0f;

            _agent = _monsterBrain.NavMeshAgent;
            if (_agent == null) return;

            _agent.isStopped = false;
            _agent.speed = _monsterBrain.MonsterChase.ChaseSpeed * WALK_SPEED_FACTOR;
            _agent.SetDestination(_monsterBrain.InvestigationPoint);
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (_monsterBrain._playersInVision.Count > 0)
            {
                StateMachine.Sequencer.RequestTransition(this, ((MonsterRoot)ParentState.ParentState).HuntState);
                return;
            }

            if (_monsterBrain.IsForcingDoor) return;
            if (_agent == null) return;

            if (!_arrived)
            {
                if (Vector3.Distance(_agent.destination, _monsterBrain.InvestigationPoint) > ARRIVAL_TOLERANCE)
                {
                    _agent.SetDestination(_monsterBrain.InvestigationPoint);
                }

                if (_agent.pathPending) return;
                if (_agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, ARRIVAL_TOLERANCE)) return;

                _arrived = true;
                _agent.isStopped = true;
                _agent.ResetPath();
                return;
            }

            _idleTimer += deltaTime;
            if (_idleTimer < LOOK_AROUND_SECONDS) return;
            
            _monsterBrain.ClearAlert();
            StateMachine.Sequencer.RequestTransition(this, ((MonsterRoot)ParentState.ParentState).RoamingState);
        }

        protected override void OnExit()
        {
            if (_agent == null) return;

            _agent.isStopped = false;
        }
        
    }
}
