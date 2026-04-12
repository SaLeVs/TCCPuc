using System.Collections.Generic;
using Monster;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class MonsterWander : NetworkBehaviour
{
    [SerializeField] private float walkSpeed;
    [SerializeField] private float wanderRadius;
    [SerializeField] private float wanderIntervalForEachPoint;
    [SerializeField] private float minTimeInSector;
    [SerializeField] private float maxTimeInSector;
    [SerializeField] private float waypointReachedDistance;
    
    private NavMeshAgent _agent;
    private PatrolSector[] _allSectors;
    private PatrolSector _currentSector;

    private float _sectorTimer;
    private float _sectorDuration;
    private float _wanderTimer;

    
    public void Initialize(NavMeshAgent monsterAgent,  PatrolSector[] allSectors)
    {
        _agent = monsterAgent;
        _allSectors = allSectors;
        
    }
    
    public void StartWander()
    {
        _agent.isStopped = false;
        _agent.speed = walkSpeed;

        MigrateToNewSector();
    }
    
    private void MigrateToNewSector()
    {
        _currentSector = GetMostRelevantSector();
        _sectorDuration = Random.Range(minTimeInSector, maxTimeInSector);
        _sectorTimer = 0f;
        _wanderTimer = wanderIntervalForEachPoint;
    }
    
    private PatrolSector GetMostRelevantSector()
    {
        Vector3 playersCenter = GetPlayersCenter();

        PatrolSector mostRelevantSector = null;
        float bestDistance = float.MaxValue;

        foreach (PatrolSector sector in _allSectors)
        {
            if (sector == _currentSector && _allSectors.Length > 1) continue;

            float distance = Vector3.Distance(sector.Position, playersCenter);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                mostRelevantSector = sector;
            }
        }

        return mostRelevantSector ?? _allSectors[Random.Range(0, _allSectors.Length)];
    }
    
    /// <summary>
    /// Get the position in center of all players, make monster goes to the closest position off all players
    /// </summary>
    /// <returns></returns>
    private Vector3 GetPlayersCenter()
    {
        IReadOnlyList<NetworkClient> allClients = NetworkManager.Singleton.ConnectedClientsList;

        if (allClients == null || allClients.Count == 0)
        {
            return transform.position;
        }

        Vector3 playersTotalPositions = Vector3.zero;
        int playersCount = 0;

        foreach (NetworkClient client in allClients)
        {
            if (client.PlayerObject != null)
            {
                playersTotalPositions += client.PlayerObject.transform.position;
                playersCount++;
            }
        }
        
        return playersCount > 0 ? playersTotalPositions / playersCount : transform.position;
    }
    
    public void UpdateWander(float deltaTime)
    {
        _sectorTimer += deltaTime;
        _wanderTimer += deltaTime;

        if (_sectorTimer >= _sectorDuration)
        {
            MigrateToNewSector();
        }
        
        // Control movement for monster in each interval or if monster Reached destination
        if (_wanderTimer >= wanderIntervalForEachPoint || ReachedDestination())
        {
            _wanderTimer = 0f;
            _agent.SetDestination(_currentSector.GetRandomPointInSector());
        }
    }
    
    public void StopWander()
    {
        _agent.isStopped = true;
        _sectorTimer = 0f;
        _wanderTimer = 0f;
    }
    
    private bool ReachedDestination()
    {
        return !_agent.pathPending && _agent.remainingDistance <= waypointReachedDistance;
    }    
    
}
