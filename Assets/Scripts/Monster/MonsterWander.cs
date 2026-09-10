using System;
using System.Collections.Generic;
using Interfaces;
using Monster;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Monster
{ 
    public class MonsterWander : NetworkBehaviour
    {
        public event Action OnStartedMovingAnimation;
        public event Action OnStoppedMovingAnimation;
        
        
        [SerializeField] private float walkSpeed;
        [SerializeField] private float wanderRadius;
        [SerializeField] private float minWanderIntervalForEachPoint;
        [SerializeField] private float maxWanderIntervalForEachPoint;
        [SerializeField] private float minTimeInSector;
        [SerializeField] private float maxTimeInSector;
        [SerializeField] private float waypointReachedDistance;
        [SerializeField] private PatrolSector[] allSectors;

        [Header("Sector choice")]
        [Tooltip("How strongly the monster favours sectors near a living player. " +
                 "0 turns it into a pure patrol that ignores where people are.")]
        [SerializeField, Min(0f)] private float playerProximityWeight = 1f;

        [Tooltip("How strongly it favours sectors it has not visited in a while. " +
                 "0 removes the coverage guarantee and it will camp near the players.")]
        [SerializeField, Min(0f)] private float coverageWeight = 1f;

        [Tooltip("Metres at which a sector's proximity score halves.")]
        [SerializeField, Min(1f)] private float proximityFalloff = 30f;

        [Tooltip("Seconds unvisited after which a sector reaches its maximum coverage score.")]
        [SerializeField, Min(1f)] private float fullyStaleSeconds = 180f;

        [Tooltip("How many times a sector may fail to produce a walkable point before the monster " +
                 "gives up and migrates early, instead of standing still for the whole sector timer.")]
        [SerializeField, Min(1)] private int maxFailedPointDraws = 3;

        [Header("Debug")]
        [Tooltip("Logs every wander leg: how far the point was, how long it took, and how long " +
                 "it then stood still. Short legs back to back are what reads as the monster " +
                 "twitching instead of patrolling.")]
        [SerializeField] private bool logWanderLegs;

        [Tooltip("A leg shorter than this many metres is flagged as suspicious.")]
        [SerializeField, Min(0f)] private float shortLegMeters = 3f;

        
        private float _footstepTimer;
        
        private NavMeshAgent _agent;
        private PatrolSector _currentSector;

        private float _sectorTimer;
        private float _sectorDuration;
        private float _wanderTimer;
        private float _currentWanderInterval;

        private bool _waitingAtPoint;

        private float[] _sectorWeights;
        private float[] _sectorLastVisitTime;
        private readonly List<Vector3> _huntablePositions = new();

        private int _failedPointDraws;

        private float _legStartTime;
        private Vector3 _legStartPosition;
        private float _legPlannedDistance;
        private float _waitStartTime;
        private int _legIndex;
        
        
        public void Initialize(NavMeshAgent monsterAgent)
        {
            _agent = monsterAgent;

            _sectorWeights = new float[allSectors.Length];
            _sectorLastVisitTime = new float[allSectors.Length];

            // Start every sector fully stale so the first few migrations spread out across the
            // map instead of clustering wherever the players happen to have spawned.
            for (int i = 0; i < _sectorLastVisitTime.Length; i++)
            {
                _sectorLastVisitTime[i] = -fullyStaleSeconds;
            }
        }
        
        public void StartWander()
        {
            _agent.isStopped = false;
            _agent.speed = walkSpeed;

            // Wander has no rotation code of its own — it relies on the agent turning itself.
            // Chase, Search and Investigate all switch this off while they steer manually, so
            // asserting it here is what makes wander correct no matter which of them ran last,
            // instead of depending on every one of them having tidied up on the way out.
            _agent.updateRotation = true;

            OnStartedMovingAnimation?.Invoke();

            MigrateToNewSector();
        }
        
        private void MigrateToNewSector()
        {
            _currentSector = GetMostRelevantSector();
            
            _sectorDuration = Random.Range(minTimeInSector, maxTimeInSector);
            _currentWanderInterval = Random.Range(minWanderIntervalForEachPoint, maxWanderIntervalForEachPoint);
            
            _wanderTimer = _currentWanderInterval;
            _sectorTimer = 0f;
        }
        
        /// <summary>
        /// Picks the next sector by weighted draw rather than by "whichever is closest".
        ///
        /// <para>Two scores decide the odds. <b>Proximity</b> keeps the monster near people —
        /// measured to the nearest player who is still in play, not to the group's average,
        /// because with players spread across the theatre that average lands in an empty
        /// corridor. <b>Staleness</b> grows for every sector the monster has not visited, and
        /// eventually outweighs proximity, which is what guarantees the whole map gets patrolled
        /// instead of two sectors being traded back and forth forever.</para>
        /// </summary>
        private PatrolSector GetMostRelevantSector()
        {
            CollectHuntablePositions();

            float totalWeight = 0f;
            int candidates = 0;

            for (int i = 0; i < allSectors.Length; i++)
            {
                PatrolSector sector = allSectors[i];

                // Always leave the current one out so a migration actually migrates.
                bool excluded = sector == null || (sector == _currentSector && allSectors.Length > 1);

                _sectorWeights[i] = excluded ? 0f : ScoreSector(i, sector);

                totalWeight += _sectorWeights[i];
                if (_sectorWeights[i] > 0f) candidates++;
            }

            if (candidates == 0 || totalWeight <= 0f)
            {
                return _currentSector != null ? _currentSector : allSectors[Random.Range(0, allSectors.Length)];
            }

            float roll = Random.value * totalWeight;

            for (int i = 0; i < allSectors.Length; i++)
            {
                roll -= _sectorWeights[i];

                if (roll > 0f) continue;

                _sectorLastVisitTime[i] = Time.time;
                LogSectorChoice(i);
                return allSectors[i];
            }

            return allSectors[allSectors.Length - 1];
        }

        private float ScoreSector(int index, PatrolSector sector)
        {
            float proximity = 0f;

            if (_huntablePositions.Count > 0)
            {
                float nearest = float.MaxValue;

                foreach (Vector3 position in _huntablePositions)
                {
                    float distance = Vector3.Distance(sector.Position, position);
                    if (distance < nearest) nearest = distance;
                }

                // 1.0 on top of someone, halving every proximityFalloff metres.
                proximity = 1f / (1f + nearest / proximityFalloff);
            }

            float sinceVisited = Time.time - _sectorLastVisitTime[index];
            float staleness = Mathf.Clamp01(sinceVisited / fullyStaleSeconds);

            // The floor keeps every sector reachable even when both scores bottom out, so no
            // corner of the map can ever become permanently unreachable.
            return (playerProximityWeight * proximity) + (coverageWeight * staleness) + 0.01f;
        }

        /// <summary>
        /// Positions of everyone still in play. Dead players and players who have escaped are
        /// skipped — a corpse on the floor used to keep dragging the monster towards it.
        /// </summary>
        private void CollectHuntablePositions()
        {
            _huntablePositions.Clear();

            IReadOnlyList<NetworkClient> allClients = NetworkManager.Singleton.ConnectedClientsList;
            if (allClients == null) return;

            foreach (NetworkClient client in allClients)
            {
                if (client.PlayerObject == null) continue;
                if (!client.PlayerObject.TryGetComponent(out IHuntable huntable)) continue;
                if (!huntable.IsHuntable) continue;

                _huntablePositions.Add(huntable.HuntablePosition);
            }
        }

        private void LogDeadSector()
        {
            if (!logWanderLegs) return;

            Debug.Log($"Wander: setor '{(_currentSector == null ? "?" : _currentSector.name)}' nao " +
                      $"produziu ponto caminhavel em {maxFailedPointDraws} tentativas — migrando cedo. " +
                      "Se for um SpawnRoom, a sala provavelmente ainda nao nasceu.");
        }

        private void LogSectorChoice(int chosen)
        {
            if (!logWanderLegs) return;

            Debug.Log($"Wander: setor -> '{allSectors[chosen].name}' " +
                      $"(peso {_sectorWeights[chosen]:0.00} de {SumWeights():0.00}, " +
                      $"{_huntablePositions.Count} jogador(es) vivo(s))");
        }

        private float SumWeights()
        {
            float total = 0f;
            foreach (float weight in _sectorWeights) total += weight;
            return total;
        }
        
        public void UpdateWander(float deltaTime)
        {
            _sectorTimer += deltaTime;

            if (_sectorTimer >= _sectorDuration)
            {
                MigrateToNewSector();
            }
            
            if (!_waitingAtPoint && ReachedDestination())
            {
                _waitingAtPoint = true;
                _wanderTimer = 0f;
                _agent.isStopped = true;

                LogLegFinished();

                OnStoppedMovingAnimation?.Invoke();
            }

            if (_waitingAtPoint)
            {
                _wanderTimer += deltaTime;

                if (_wanderTimer >= _currentWanderInterval)
                {
                    // No point on the navmesh this time: stay put and try again next interval
                    // rather than handing the agent an invalid destination.
                    if (!_currentSector.TryGetRandomPointInSector(out Vector3 destination))
                    {
                        _wanderTimer = 0f;
                        _failedPointDraws++;

                        // A sector with no walkable floor at all would otherwise pin the monster
                        // in place for the whole sector duration. The mission-room slots are
                        // exactly this until SpawnRooms has built them and rebuilt the navmesh,
                        // and that happens only once every player has connected — so there is a
                        // real window early in the match where these sectors are empty ground.
                        if (_failedPointDraws >= maxFailedPointDraws)
                        {
                            LogDeadSector();
                            MigrateToNewSector();
                        }

                        return;
                    }

                    _failedPointDraws = 0;
                    _waitingAtPoint = false;
                    _wanderTimer = 0f;
                    _currentWanderInterval = Random.Range(minWanderIntervalForEachPoint, maxWanderIntervalForEachPoint);
                    _agent.isStopped = false;
                    _agent.SetDestination(destination);

                    LogLegStarted(destination);

                    OnStartedMovingAnimation?.Invoke();
                }
            }
        }
        
        public void StopWander()
        {
            _agent.isStopped = true;
            _agent.ResetPath();

            _sectorTimer = 0f;
            _wanderTimer = 0f;
        }
        
        private void LogLegStarted(Vector3 destination)
        {
            if (!logWanderLegs) return;

            float waited = _waitStartTime > 0f ? Time.time - _waitStartTime : 0f;

            _legIndex++;
            _legStartTime = Time.time;
            _legStartPosition = transform.position;
            _legPlannedDistance = Vector3.Distance(_legStartPosition, destination);

            string flag = _legPlannedDistance < shortLegMeters ? "  <<< PERNA CURTA" : "";

            Debug.Log($"Wander leg #{_legIndex}: parado por {waited:0.00}s, novo ponto a " +
                      $"{_legPlannedDistance:0.0}m (setor {(_currentSector == null ? "?" : _currentSector.name)}){flag}");
        }

        private void LogLegFinished()
        {
            _waitStartTime = Time.time;

            if (!logWanderLegs) return;
            if (_legStartTime <= 0f) return;

            float duration = Time.time - _legStartTime;
            float travelled = Vector3.Distance(_legStartPosition, transform.position);
            float averageSpeed = duration > 0.001f ? travelled / duration : 0f;

            // A leg that ends far short of its planned distance means the agent gave up or the
            // point was unreachable — that repathing is what shows up as twitching.
            string flag = travelled < _legPlannedDistance * 0.6f ? "  <<< NAO CHEGOU PERTO DO PONTO" : "";

            Debug.Log($"Wander leg #{_legIndex} fim: andou {travelled:0.0}m de {_legPlannedDistance:0.0}m " +
                      $"em {duration:0.00}s (media {averageSpeed:0.0} m/s, esperado {walkSpeed:0.0}){flag}");
        }

        private bool ReachedDestination()
        {
            if (_agent.pathPending) return false;
            if (_agent.remainingDistance > waypointReachedDistance) return false;

            // remainingDistance also reads 0 when the agent has no path at all, so without the
            // hasPath check the very first frame of a wander counts as "arrived" and the monster
            // hitches into idle before it has taken a step.
            return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f;
        }
    }
}
