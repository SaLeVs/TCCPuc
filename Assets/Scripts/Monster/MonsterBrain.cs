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
        
        [SerializeField] private NavMeshAgent navMeshAgent;
        
        [SerializeField] private VisionSensor visionSensor;
        [SerializeField] private MonsterWander monsterWander;
        [SerializeField] private MonsterSabotage monsterSabotage;
        [SerializeField] private MonsterChase monsterChase;
        [SerializeField] private MonsterAttack monsterAttack;
        [SerializeField] private MonsterAnimator monsterAnimator;
        [SerializeField] private MonsterSearch monsterSearch;
        [SerializeField] private MonsterDoorForcer monsterDoorForcer;
        
        [Tooltip("Seconds the monster keeps a perfect fix on a target after losing sight of it.")]
        [SerializeField] private float trackingGraceSeconds = 3f;

        public MonsterWander MonsterWander => monsterWander;
        public MonsterSabotage MonsterSabotage => monsterSabotage;
        public MonsterChase MonsterChase => monsterChase;
        public MonsterAttack MonsterAttack => monsterAttack;
        public MonsterAnimator MonsterAnimator => monsterAnimator;
        public MonsterSearch MonsterSearch => monsterSearch;
        public MonsterDoorForcer MonsterDoorForcer => monsterDoorForcer;
        
        
        public readonly List<Transform> _playersInVision = new();
        public Vector3 LastKnownTargetPosition { get; private set; }
        public Transform LastKnownTarget { get; private set; }
        public bool ShouldEnterAlert { get; set; }
        
        public bool IsTrackingLostTarget => _trackingTimer > 0f && LastKnownTarget != null;

        /// <summary>Sees someone, or still holds a fix on someone it just lost.</summary>
        public bool IsHunting => _playersInVision.Count > 0 || IsTrackingLostTarget;
        public bool IsForcingDoor => monsterDoorForcer != null && monsterDoorForcer.IsForcingDoor;

        private StateMachine _stateMachine;
        private State _rootState;
        private string _lastPath;
        private float _trackingTimer;

        
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

            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.Initialize(navMeshAgent);
            }
            
            if (!IsServer) return;
            
            _stateMachine.Start();
            visionSensor.OnTargetEnter += VisionSensor_OnTargetEnter;
            visionSensor.OnTargetExit += VisionSensor_OnTargetExit;
        }
        
        
        private void VisionSensor_OnTargetEnter(GameObject player)
        {
            _playersInVision.Add(player.transform);
            
            _trackingTimer = 0f;
            ShouldEnterAlert = false;

            OnPlayerEnterInVision?.Invoke(player.transform);
        }

        private void VisionSensor_OnTargetExit(GameObject player)
        {
            _playersInVision.Remove(player.transform);

            if (_playersInVision.Count == 0)
            {
                LastKnownTargetPosition = player.transform.position;
                LastKnownTarget = player.transform;

                _trackingTimer = trackingGraceSeconds;
            }

            OnPlayerExitInVision?.Invoke(player.transform);
        }

        private void TickTracking(float deltaTime)
        {
            if (_trackingTimer <= 0f) return;

            if (LastKnownTarget == null)
            {
                GoCold();
                return;
            }
            
            LastKnownTargetPosition = LastKnownTarget.position;

            _trackingTimer -= deltaTime;
            if (_trackingTimer > 0f) return;

            GoCold();
        }

        private void GoCold()
        {
            _trackingTimer = 0f;
            ShouldEnterAlert = true;

            MonsterChase.ForgetTarget();
        }

        private void Update()
        {
            if (!IsServer) return;

            TickTracking(Time.deltaTime);
            
            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.Tick(Time.deltaTime);
            }

            _stateMachine.Tick(Time.deltaTime);
            
            string statePath = StatePath(_stateMachine.Root.Leaf());
            if (statePath != _lastPath)
            {
                Debug.Log($"Monster: State: {statePath}");
                _lastPath = statePath;
            }
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