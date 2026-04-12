using System.Collections.Generic;
using System.Linq;
using Monster.HSM;
using Monster.MonsterStates;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterBrain : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private MonsterWander monsterWander;
        [SerializeField] private MonsterSabotage monsterSabotage;
        
        [SerializeField] private PatrolSector[] patrolSectors;
        
        public Animator Animator => animator;
        public NavMeshAgent NavMeshAgent => navMeshAgent;
        public MonsterWander MonsterWander => monsterWander;
        public MonsterSabotage MonsterSabotage => monsterSabotage;
        public PatrolSector[] PatrolSectors => patrolSectors;
        
        private StateMachine _stateMachine;
        private State _rootState;
        
        private string _lastPath;

        
        private void Awake()
        {
            _rootState = new MonsterRoot(null, this);
            StateMachineBuilder stateMachineBuilder = new StateMachineBuilder(_rootState);
            _stateMachine = stateMachineBuilder.Build();
        }
        
        private void Update()
        {
            _stateMachine.Tick(Time.deltaTime);
            
            string statePath = StatePath(_stateMachine.Root.Leaf());
            
            if (statePath != _lastPath)
            {
                Debug.Log($"State: {statePath}");
                _lastPath = statePath;
            }
        }
        
        
        private static string StatePath(State state)
        {
            return string.Join(" > ", state.PathToRoot().Reverse().Select(node => node.GetType().Name));
        }
        
    }
}