using Components;
using Unity.Netcode;
using UnityEngine;

namespace LevelDesign.Gym
{
    public enum GymPadAction
    {
        [InspectorName("Monstro aparece no alvo e persegue")] MonsterChaseFrom,
        [InspectorName("Monstro vira estátua no alvo")] MonsterStatueAt,
        [InspectorName("Monstro volta a andar")] MonsterResume,
        [InspectorName("Monstro volta para casa")] MonsterHome,
        [InspectorName("Cronômetro: largada")] TimerStart,
        [InspectorName("Cronômetro: parcial")] TimerSplit,
        [InspectorName("Cronômetro: chegada")] TimerStop,
        [InspectorName("Curar jogador")] Heal,
        [InspectorName("Zerar awareness do monstro")] ClearAwareness,
    }

    /// <summary>
    /// A floor plate that does one GYM action when the local player steps on it. Built by the GYM
    /// generator; drop more anywhere — it only needs a trigger collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GymPad : MonoBehaviour
    {
        [SerializeField] private GymPadAction action;
        [SerializeField] private Transform target;
        [SerializeField] private string label;
        [SerializeField] private float cooldown = 1.5f;

        private float _nextAllowed;

        public GymPadAction Action => action;
        public string Label => label;

        public void Configure(GymPadAction padAction, Transform padTarget, string padLabel)
        {
            action = padAction;
            target = padTarget;
            label = padLabel;
        }

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < _nextAllowed) return;

            var body = other.attachedRigidbody;
            if (body == null || !body.TryGetComponent(out NetworkObject networkObject) || !networkObject.IsLocalPlayer) return;

            _nextAllowed = Time.time + cooldown;
            Run(body.transform);
        }

        private void Run(Transform player)
        {
            var remote = GymMonsterRemote.Instance;

            switch (action)
            {
                case GymPadAction.MonsterChaseFrom:
                    if (remote != null && target != null) remote.PlaceAt(target.position, player.position);
                    break;
                case GymPadAction.MonsterStatueAt:
                    if (remote != null) remote.FreezeAt(target);
                    break;
                case GymPadAction.MonsterResume:
                    if (remote != null) remote.SetPaused(false);
                    break;
                case GymPadAction.MonsterHome:
                    if (remote != null) remote.SendHome();
                    break;
                case GymPadAction.TimerStart:
                    GymStopwatch.Begin(label);
                    break;
                case GymPadAction.TimerSplit:
                    GymStopwatch.Split(label);
                    break;
                case GymPadAction.TimerStop:
                    GymStopwatch.Stop(label);
                    break;
                case GymPadAction.Heal:
                    if (player.TryGetComponent(out Health health)) health.RestoreHealth(health.MaxHealth);
                    break;
                case GymPadAction.ClearAwareness:
                    if (remote != null && remote.Monster != null) remote.Monster.MonsterAwareness.Clear();
                    break;
            }

            GymDebugHud.Toast(string.IsNullOrEmpty(label) ? action.ToString() : label);
        }
    }
}
