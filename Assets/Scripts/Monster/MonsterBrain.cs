using System;
using System.Collections.Generic;
using System.Linq;
using Components;
using Monster.HSM;
using Monster.MonsterStates;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterBrain : NetworkBehaviour
    {
        public event Action<Transform> OnPlayerEnterInVision;
        public event Action<Transform> OnPlayerExitInVision;
        public static Action<Vector3> OnMonsterFootstepSound;
        
        [SerializeField] private NavMeshAgent navMeshAgent;
        
        [SerializeField] private VisionSensor visionSensor;
        [SerializeField] private MonsterWander monsterWander;
        [SerializeField] private MonsterSabotage monsterSabotage;
        [SerializeField] private MonsterChase monsterChase;
        [SerializeField] private MonsterAttack monsterAttack;
        [SerializeField] private MonsterAnimator monsterAnimator;
        [SerializeField] private MonsterSearch monsterSearch;
        
        [SerializeField] private float footstepDistance = 2.0f;
        [SerializeField] private float minMoveSpeedToStep = 0.1f;

        private float _distanceSinceLastFootstep;
        private Vector3 _lastFootstepPosition;
        
        
        public MonsterWander MonsterWander => monsterWander;
        public MonsterSabotage MonsterSabotage => monsterSabotage;
        public MonsterChase MonsterChase => monsterChase;
        public MonsterAttack MonsterAttack => monsterAttack;
        public MonsterAnimator MonsterAnimator => monsterAnimator;
        public MonsterSearch MonsterSearch => monsterSearch;
        
        public readonly List<Transform> _playersInVision = new();
        public Vector3 LastKnownTargetPosition { get; private set; }
        public Transform LastKnownTarget { get; private set; }
        public bool ShouldEnterAlert { get; set; }
        
        private StateMachine _stateMachine;
        private State _rootState;
        
        private string _lastPath;

        
        private void Awake()
        {
            _rootState = new MonsterRoot(null, this);
            StateMachineBuilder stateMachineBuilder = new StateMachineBuilder(_rootState);
            _stateMachine = stateMachineBuilder.Build();
        }
        
        public override void OnNetworkSpawn()
        {
            MonsterWander.Initialize(navMeshAgent);
            MonsterChase.Initialize(_playersInVision, navMeshAgent, this);
            MonsterAnimator.Initialize(this);
            MonsterSabotage.Initialize();
            MonsterSearch.Initialize(navMeshAgent);
            MonsterAttack.Initialize(navMeshAgent);
            
            if (!IsServer) return;
            
            _stateMachine.Start();
            visionSensor.OnTargetEnter += VisionSensor_OnTargetEnter;
            visionSensor.OnTargetExit += VisionSensor_OnTargetExit;
        }
        
        private void VisionSensor_OnTargetEnter(GameObject player)
        {
            _playersInVision.Add(player.transform);
            OnPlayerEnterInVision?.Invoke(player.transform);
        }
        
        private void VisionSensor_OnTargetExit(GameObject player)
        {
            _playersInVision.Remove(player.transform);

            if (_playersInVision.Count == 0)
            {
                LastKnownTargetPosition = player.transform.position;
                LastKnownTarget = player.transform;

                ShouldEnterAlert = true;
            }

            OnPlayerExitInVision?.Invoke(player.transform);
        }
        
        private void Update()
        {
            if (!IsServer) return;
            
            _stateMachine.Tick(Time.deltaTime);
            HandleFootsteps();
            
            string statePath = StatePath(_stateMachine.Root.Leaf());
            if (statePath != _lastPath)
            {
                Debug.Log($"Monster: State: {statePath}");
                _lastPath = statePath;
            }
        }
        
        private void HandleFootsteps()
        {
            if (navMeshAgent == null || navMeshAgent.isStopped) 
            {
                _distanceSinceLastFootstep = 0f;
                _lastFootstepPosition = transform.position;
                return;
            }

            float currentSpeed = navMeshAgent.velocity.magnitude;

            if (currentSpeed < minMoveSpeedToStep)
            {
                _distanceSinceLastFootstep = 0f;
                _lastFootstepPosition = transform.position;
                return;
            }

            Vector3 flatDelta = transform.position - _lastFootstepPosition;
            flatDelta.y = 0f;

            _distanceSinceLastFootstep += flatDelta.magnitude;
            _lastFootstepPosition = transform.position;

            if (_distanceSinceLastFootstep >= footstepDistance)
            {
                _distanceSinceLastFootstep -= footstepDistance;
                NotifyFootstepClientRpc(transform.position);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyFootstepClientRpc(Vector3 position)
        {
            OnMonsterFootstepSound?.Invoke(position);
        }
        
        private static string StatePath(State state)
        {
            return string.Join(" > ", state.PathToRoot().Reverse().Select(node => node.GetType().Name));
        }

        
        public override void OnNetworkDespawn()
        {
            MonsterChase.Uninitialize(_playersInVision, navMeshAgent, this);
            MonsterAnimator.Uninitialize(this);
            MonsterSabotage.Uninitialize();
            
            if (!IsServer) return;
            
            visionSensor.OnTargetEnter -= VisionSensor_OnTargetEnter;
            visionSensor.OnTargetExit -= VisionSensor_OnTargetExit;
        }
        
    }
}