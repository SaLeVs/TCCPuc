using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class MonsterWander : NetworkBehaviour
{
    [SerializeField] private float walkSpeed;
    [SerializeField] private float wanderRadius;
    [SerializeField] private float wanderInterval;
    
    private NavMeshAgent agent;
    private float _wanderTimer;

    public void UpdateWander(float deltaTime)
    {
        
    }
    
    
}
