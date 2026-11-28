using System.Collections.Generic;
using Components.Sound;
using Missions.Puzzles;
using Monster;
using Player;
using Systems;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MonsterLures
{
    /// <summary>
    /// Pulls the monster, at a run, toward three things: a puzzle just solved, the lights just
    /// restored, and a player who has gone too long without seeing it.
    ///
    /// <para>Server-only. It never drives the agent: it only calls
    /// <see cref="MonsterAwareness.RegisterLure"/>, and the state machine does the rest
    /// (Alert → Search, which runs at chase speed). That keeps the existing priorities intact:
    /// seeing a player always wins, and a swing in progress is never cut.</para>
    ///
    /// <para>Overlap rules: ignored while hunting; one lure at a time, a new one only replaces the
    /// current one if it has a higher priority (puzzle &gt; light &gt; unseen); otherwise a global
    /// cooldown has to pass. Every lure and every chase resets the "unseen" timers.</para>
    ///
    /// <para>Goes on the monster, next to <see cref="MonsterBrain"/>.</para>
    /// </summary>
    [RequireComponent(typeof(MonsterBrain))]
    public class MonsterLure : MonoBehaviour
    {
        private enum LureReason { None = 0, Unseen = 1, Light = 2, Puzzle = 3 }

        [Header("Gatilhos")]
        [SerializeField] private bool lureOnPuzzleSolved = true;
        [SerializeField] private bool lureOnLightsRestored = true;
        [SerializeField] private bool lureWhenUnseen = true;

        [Header("Sobreposição")]
        [Tooltip("Seconds after a lure before another one of equal or lower priority is accepted.")]
        [SerializeField, Min(0f)] private float globalCooldown = 50f;

        [Header("Tempo sem ver o monstro")]
        [Tooltip("Seconds a living player can go without seeing the monster before it comes looking.")]
        [SerializeField, Min(10f)] private float unseenSeconds = 180f;

        [Tooltip("How far from that player it aims, so it arrives close without walking straight onto them.")]
        [SerializeField, Min(0f)] private float unseenMinOffset = 5f;
        [SerializeField, Min(0f)] private float unseenMaxOffset = 8f;

        [Tooltip("Beyond this the player is not counted as seeing it, even with a clear line.")]
        [SerializeField, Min(1f)] private float sightDistance = 25f;

        [Tooltip("Half-angle of the player's view, measured from where the body faces.")]
        [SerializeField, Range(10f, 90f)] private float sightHalfAngle = 70f;

        [Tooltip("What blocks the line between a player's eyes and the monster.")]
        [SerializeField] private LayerMask sightBlockers = ~0;

        [SerializeField, Min(0.05f)] private float sightCheckInterval = 0.25f;
        [SerializeField] private float playerEyeHeight = 1.6f;
        [SerializeField] private float monsterChestHeight = 1.2f;

        [Header("Feedback")]
        [Tooltip("Played at the monster when a lure fires, so players know why it is coming.")]
        [SerializeField] private SoundDefinitionSO lureSound;

        [SerializeField] private bool logLures = true;

        private MonsterBrain _brain;

        private LureReason _activeReason;
        private int _activeLureCount = -1;
        private float _lastLureTime = float.NegativeInfinity;

        private readonly Dictionary<ulong, float> _unseenTimers = new();
        private float _sightTimer;

        private readonly Dictionary<PuzzleManagerBase, bool> _puzzles = new();
        private readonly Dictionary<PuzzleManagerBase, System.Action<bool>> _puzzleHandlers = new();
        private readonly Dictionary<ElectricCircuit, System.Action> _circuits = new();
        private float _rescanTimer;
        private const float RescanInterval = 2f;

        private static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake() => _brain = GetComponent<MonsterBrain>();

        private void OnDisable() => UnsubscribeAll();

        private void Update()
        {
            if (!IsServer || _brain == null || !_brain.IsSpawned) return;

            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = RescanInterval;
                ScanSources();
            }

            RefreshActiveLure();
            TickUnseen(Time.deltaTime);
        }

        // ---------------------------------------------------------------- sources

        /// <summary>Rooms and puzzles spawn during the match, so new ones are picked up as they appear.</summary>
        private void ScanSources()
        {
            foreach (PuzzleManagerBase puzzle in FindObjectsByType<PuzzleManagerBase>(FindObjectsSortMode.None))
            {
                if (_puzzles.ContainsKey(puzzle)) continue;

                // Remember the state it had when found: the spawn-time invoke and a puzzle already
                // solved before the monster existed must not count as "just solved".
                _puzzles[puzzle] = puzzle.IsSpawned && puzzle.IsComplete;

                PuzzleManagerBase captured = puzzle;
                System.Action<bool> handler = complete => Puzzle_OnCompleteChanged(captured, complete);
                _puzzleHandlers[puzzle] = handler;
                puzzle.OnCompleteChanged += handler;
            }

            foreach (ElectricCircuit circuit in FindObjectsByType<ElectricCircuit>(FindObjectsSortMode.None))
            {
                if (_circuits.ContainsKey(circuit)) continue;

                ElectricCircuit captured = circuit;
                System.Action handler = () => Circuit_OnRestored(captured);
                _circuits[circuit] = handler;
                circuit.OnCircuitRestored += handler;
            }
        }

        private void UnsubscribeAll()
        {
            foreach (KeyValuePair<PuzzleManagerBase, System.Action<bool>> pair in _puzzleHandlers)
            {
                if (pair.Key != null) pair.Key.OnCompleteChanged -= pair.Value;
            }

            foreach (KeyValuePair<ElectricCircuit, System.Action> pair in _circuits)
            {
                if (pair.Key != null) pair.Key.OnCircuitRestored -= pair.Value;
            }

            _puzzles.Clear();
            _puzzleHandlers.Clear();
            _circuits.Clear();
        }

        private void Puzzle_OnCompleteChanged(PuzzleManagerBase puzzle, bool complete)
        {
            if (!IsServer || puzzle == null) return;

            bool wasComplete = _puzzles.TryGetValue(puzzle, out bool previous) && previous;
            _puzzles[puzzle] = complete;

            if (!complete || wasComplete || !lureOnPuzzleSolved) return;

            TryLure(LureReason.Puzzle, puzzle.transform.position, puzzle.name);
        }

        private void Circuit_OnRestored(ElectricCircuit circuit)
        {
            // The notify is ClientsAndHost: only the host's copy acts.
            if (!IsServer || circuit == null || !lureOnLightsRestored) return;

            TryLure(LureReason.Light, circuit.transform.position, circuit.name);
        }

        // ---------------------------------------------------------------- unseen

        private void TickUnseen(float deltaTime)
        {
            NetworkManager network = NetworkManager.Singleton;

            // A chase means everyone involved knows where it is: start counting again afterwards.
            if (_brain.IsHunting)
            {
                _unseenTimers.Clear();
                return;
            }

            _sightTimer -= deltaTime;
            bool checkSight = _sightTimer <= 0f;
            if (checkSight) _sightTimer = sightCheckInterval;

            foreach (NetworkClient client in network.ConnectedClientsList)
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject == null) continue;

                if (!playerObject.TryGetComponent(out PlayerState state) || state.IsDead)
                {
                    _unseenTimers.Remove(client.ClientId);
                    continue;
                }

                _unseenTimers.TryGetValue(client.ClientId, out float timer);
                timer += deltaTime;

                if (checkSight && CanSeeMonster(playerObject.transform)) timer = 0f;

                _unseenTimers[client.ClientId] = timer;

                if (lureWhenUnseen && timer >= unseenSeconds)
                {
                    // Reset either way, so a refused lure does not retry every frame.
                    _unseenTimers[client.ClientId] = 0f;
                    TryLure(LureReason.Unseen, OffsetAround(playerObject.transform.position), $"player {client.ClientId} sem ver o monstro");
                    return; // TryLure may have cleared the dictionary.
                }
            }
        }

        private bool CanSeeMonster(Transform player)
        {
            Vector3 eye = player.position + Vector3.up * playerEyeHeight;
            Vector3 target = transform.position + Vector3.up * monsterChestHeight;
            Vector3 toMonster = target - eye;

            float distance = toMonster.magnitude;
            if (distance > sightDistance) return false;

            Vector3 flat = Vector3.ProjectOnPlane(toMonster, Vector3.up);
            if (flat.sqrMagnitude > 0.01f && Vector3.Angle(player.forward, flat) > sightHalfAngle) return false;

            if (!Physics.Raycast(eye, toMonster / distance, out RaycastHit hit, distance, sightBlockers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // The first thing hit is the monster itself, or the player's own body on the way out.
            return hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player);
        }

        /// <summary>
        /// A few random spots around the player; the first on the NavMesh wins. A single try often
        /// landed inside a wall, and the lure then waited a whole <see cref="unseenSeconds"/> again.
        /// Falls back to the player's own position.
        /// </summary>
        private Vector3 OffsetAround(Vector3 point)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float radius = Random.Range(unseenMinOffset, Mathf.Max(unseenMinOffset, unseenMaxOffset));
                Vector3 candidate = point + new Vector3(dir.x, 0f, dir.y) * radius;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return hit.position;
            }

            return point;
        }

        // ---------------------------------------------------------------- lure

        /// <summary>Drops the "active" mark once the monster is no longer going for that lure.</summary>
        private void RefreshActiveLure()
        {
            if (_activeReason == LureReason.None) return;

            MonsterAwareness awareness = _brain.MonsterAwareness;
            bool stillGoing = awareness.IsHeld && awareness.LureCount == _activeLureCount;

            if (!stillGoing) _activeReason = LureReason.None;
        }

        private void TryLure(LureReason reason, Vector3 point, string source)
        {
            if (_brain.IsHunting)
            {
                Log($"ignorado ({reason}, {source}): está caçando");
                return;
            }

            bool overridesActive = _activeReason != LureReason.None && reason > _activeReason;

            if (_activeReason != LureReason.None && !overridesActive)
            {
                Log($"ignorado ({reason}, {source}): já indo para {_activeReason}");
                return;
            }

            if (!overridesActive && Time.time - _lastLureTime < globalCooldown)
            {
                Log($"ignorado ({reason}, {source}): cooldown {globalCooldown - (Time.time - _lastLureTime):0}s");
                return;
            }

            if (!NavMesh.SamplePosition(point, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
            {
                Log($"ignorado ({reason}, {source}): ponto fora do NavMesh");
                return;
            }

            MonsterAwareness awareness = _brain.MonsterAwareness;
            awareness.RegisterLure(navHit.position);

            _activeReason = reason;
            _activeLureCount = awareness.LureCount;
            _lastLureTime = Time.time;
            _unseenTimers.Clear();

            if (lureSound != null) WorldSound.Play(lureSound, transform.position, _brain.NetworkObject);

            Log($"ATRAÍDO por {reason} ({source}) → {navHit.position}");
        }

        private void Log(string message)
        {
            if (logLures) Debug.Log($"MonsterLure: {message}", this);
        }
    }
}
