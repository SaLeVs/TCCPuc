using System;
using System.Collections.Generic;
using System.Linq;
using Components;
using Components.Perception;
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
        [SerializeField] private HearingSensor hearingSensor;
        [SerializeField] private MonsterWander monsterWander;
        [SerializeField] private MonsterSabotage monsterSabotage;
        [SerializeField] private MonsterChase monsterChase;
        [SerializeField] private MonsterAttack monsterAttack;
        [SerializeField] private MonsterAnimator monsterAnimator;
        [SerializeField] private MonsterSearch monsterSearch;
        [SerializeField] private MonsterDoorForcer monsterDoorForcer;
        [SerializeField] private MonsterAwareness monsterAwareness;
        [SerializeField] private MonsterInvestigate monsterInvestigate;
        
        [Tooltip("Seconds the monster keeps a perfect fix on a target after losing sight of it.")]
        [SerializeField] private float trackingGraceSeconds = 3f;

        public NavMeshAgent NavMeshAgent => navMeshAgent;
        public MonsterAwareness MonsterAwareness => monsterAwareness;
        public MonsterInvestigate MonsterInvestigate => monsterInvestigate;
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

        public bool IsTrackingLostTarget => _trackingTimer > 0f && LastKnownTarget != null;
        
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

            if (monsterInvestigate != null)
            {
                monsterInvestigate.Initialize(navMeshAgent);
            }

            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.Initialize(navMeshAgent);
            }
            
            if (!IsServer) return;

            // Awareness is not optional the way the door forcer is — every Alert transition
            // reads it. Fail here with a clear message instead of a null ref mid-chase.
            if (monsterAwareness == null || monsterInvestigate == null)
            {
                Debug.LogError($"{name}: MonsterBrain is missing MonsterAwareness or MonsterInvestigate. The monster will not react to anything it hears.", this);
                return;
            }

            _stateMachine.Start();
            visionSensor.OnTargetEnter += VisionSensor_OnTargetEnter;
            visionSensor.OnTargetExit += VisionSensor_OnTargetExit;

            if (hearingSensor != null)
            {
                hearingSensor.OnNoiseHeard += HearingSensor_OnNoiseHeard;
            }
        }


        private void HearingSensor_OnNoiseHeard(HeardNoise heard)
        {
            // Vision outranks hearing. While it can see you — or is still coasting on a fix it
            // just lost — a noise tells it nothing it does not already know, and letting sound
            // rewrite the destination would pull it off an active chase.
            if (IsHunting) return;

            monsterAwareness.RegisterNoise(heard);
        }

        private void TickAwareness(float deltaTime)
        {
            // Seeing someone pins the meter, so losing them drops straight into Alerted
            // rather than into a lazy Investigate.
            if (IsHunting) monsterAwareness.PinToMax();
            else monsterAwareness.Tick(deltaTime);
        }


        private void VisionSensor_OnTargetEnter(GameObject player)
        {
            _playersInVision.Add(player.transform);

            _trackingTimer = 0f;

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

            monsterAwareness.RegisterLostSight(LastKnownTargetPosition);

            MonsterChase.ForgetTarget();
        }

        private void Update()
        {
            if (!IsServer) return;

            TickTracking(Time.deltaTime);
            TickAwareness(Time.deltaTime);
            
            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.Tick(Time.deltaTime);
            }

            _stateMachine.Tick(Time.deltaTime);

            string statePath = StatePath(_stateMachine.Root.Leaf());
            if (statePath == _lastPath) return;

            _lastPath = statePath;
            OnStateChanged?.Invoke(statePath);
        }

        /// <summary>Current "Root &gt; Parent &gt; Leaf" path. Empty before the machine starts.</summary>
        public string CurrentStatePath => _lastPath;

        /// <summary>
        /// Server-side, fires when the leaf state changes. <see cref="MonsterDebugger"/> listens
        /// so the brain no longer logs to the console on its own.
        /// </summary>
        public event Action<string> OnStateChanged;
        
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

            if (hearingSensor != null)
            {
                hearingSensor.OnNoiseHeard -= HearingSensor_OnNoiseHeard;
            }
        }
        
    }
}