using System.Reflection;
using Monster;
using Monster.HSM;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LevelDesign.Gym
{
    /// <summary>
    /// The GYM's remote control for the monster: freeze it in place, drop it at a set distance
    /// behind you, send it home. It only drives the NavMeshAgent and enables/disables the brain from
    /// outside; MonsterBrain itself is untouched. Server only — in the GYM that is you, the host.
    /// </summary>
    public class GymMonsterRemote : MonoBehaviour
    {
        public static GymMonsterRemote Instance { get; private set; }

        [SerializeField] private MonsterBrain monster;
        [SerializeField] private Transform home;

        [Tooltip("Começa como estátua no covil, para você explorar o GYM em paz. F3 solta.")]
        [SerializeField] private bool startPaused = true;

        private bool _startApplied;

        private static readonly FieldInfo LeafField = typeof(MonsterBrain).GetField("_lastLeaf", BindingFlags.Instance | BindingFlags.NonPublic);

        public MonsterBrain Monster => monster;
        public bool IsPaused { get; private set; }

        private static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Name of the state the brain is in (ChaseState, WanderState...).</summary>
        public string CurrentStateName
        {
            get
            {
                if (monster == null) return "—";
                if (IsPaused) return "Pausado (estátua)";
                var leaf = LeafField?.GetValue(monster) as State;
                return leaf != null ? leaf.GetType().Name.Replace("State", string.Empty) : "—";
            }
        }

        public void SetPaused(bool paused)
        {
            if (monster == null || !IsServer) return;

            IsPaused = paused;
            monster.enabled = !paused;

            var agent = monster.NavMeshAgent;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = paused;
                if (paused) agent.ResetPath();
            }
        }

        public void TogglePaused() => SetPaused(!IsPaused);

        /// <summary>Drops the monster <paramref name="distance"/> metres behind the player, facing them.</summary>
        public void PlaceBehind(Transform player, float distance)
        {
            if (player == null) return;

            Vector3 back = -player.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = Vector3.back;

            PlaceAt(player.position + back.normalized * distance, player.position);
        }

        public void PlaceAt(Vector3 position, Vector3 lookAt)
        {
            if (monster == null || !IsServer) return;

            var agent = monster.NavMeshAgent;
            if (NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas)) position = hit.position;

            if (agent != null && agent.enabled) agent.Warp(position);
            else monster.transform.position = position;

            Vector3 facing = lookAt - position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f) monster.transform.rotation = Quaternion.LookRotation(facing);

            // A chase pad means "come get me": a frozen monster would just stand there.
            if (IsPaused) SetPaused(false);
        }

        public void SendHome()
        {
            if (home == null) return;
            PlaceAt(home.position, home.position + home.forward);
        }

        public void FreezeAt(Transform spot)
        {
            if (spot == null) return;
            PlaceAt(spot.position, spot.position + spot.forward);
            SetPaused(true);
        }

        private void Update()
        {
            // The monster only takes orders once the host has spawned it.
            if (_startApplied || monster == null || !monster.IsSpawned || !IsServer) return;

            _startApplied = true;
            if (startPaused) SetPaused(true);
        }

        private void LateUpdate()
        {
            if (!IsPaused || monster == null) return;

            var agent = monster.NavMeshAgent;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }
    }
}
