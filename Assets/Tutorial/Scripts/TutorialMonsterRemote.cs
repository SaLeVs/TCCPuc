using Monster;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Tutorial
{
    /// <summary>
    /// Controle remoto do monstro no tutorial: estátua no palco até a hora da fuga, depois solto
    /// atrás do jogador. Só mexe no NavMeshAgent e liga/desliga o MonsterBrain por fora, como o GYM
    /// faz - o monstro em si é o prefab do jogo, intocado. Só no servidor (no tutorial, o host).
    /// </summary>
    public class TutorialMonsterRemote : MonoBehaviour
    {
        public static TutorialMonsterRemote Instance { get; private set; }

        [SerializeField] private MonsterBrain monster;
        [SerializeField] private Transform home;

        [Tooltip("Começa parado no palco. O diretor solta na lição de fuga.")]
        [SerializeField] private bool startPaused = true;

        private bool _startApplied;

        public MonsterBrain Monster => monster;
        public bool IsPaused { get; private set; }
        public bool IsReady => monster != null && monster.IsSpawned && _startApplied;

        private static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetPaused(bool paused)
        {
            if (monster == null || !IsServer) return;

            IsPaused = paused;
            monster.enabled = !paused;

            NavMeshAgent agent = monster.NavMeshAgent;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = paused;
                if (paused) agent.ResetPath();
            }
        }

        /// <summary>Põe o monstro em <paramref name="position"/> olhando para <paramref name="lookAt"/>. Não muda o estado de pausa.</summary>
        public void PlaceAt(Vector3 position, Vector3 lookAt)
        {
            if (monster == null || !IsServer) return;

            NavMeshAgent agent = monster.NavMeshAgent;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) position = hit.position;

            if (agent != null && agent.enabled) agent.Warp(position);
            else monster.transform.position = position;

            Vector3 facing = lookAt - position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f) monster.transform.rotation = Quaternion.LookRotation(facing);
        }

        /// <summary>Solta o monstro em <paramref name="position"/> já sabendo onde o jogador está.</summary>
        public void Hunt(Vector3 position, Vector3 playerPosition)
        {
            if (monster == null || !IsServer) return;

            PlaceAt(position, playerPosition);
            SetPaused(false);

            // Um alvo "recém-perdido" naquele ponto: a suspeita vai ao máximo e ele corre até lá
            // (Search). A visão dele assume dali em diante.
            if (monster.MonsterAwareness != null) monster.MonsterAwareness.RegisterLostSight(playerPosition);
        }

        /// <summary>Volta para a marca do palco e vira estátua de novo.</summary>
        public void SendHome()
        {
            if (home == null) return;

            PlaceAt(home.position, home.position + home.forward);
            SetPaused(true);
        }

        private void Update()
        {
            if (_startApplied || monster == null || !monster.IsSpawned || !IsServer) return;

            _startApplied = true;
            if (startPaused) SetPaused(true);
        }

        private void LateUpdate()
        {
            if (!IsPaused || monster == null) return;

            NavMeshAgent agent = monster.NavMeshAgent;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }
    }
}
