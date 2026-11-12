using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Tutorial
{
    /// <summary>
    /// Um volume de gatilho com nome. O diretor do tutorial pergunta "o jogador já pisou em X?" e
    /// "o jogador está dentro de X agora?" - só o jogador local conta.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TutorialZone : MonoBehaviour
    {
        [SerializeField] private string zoneId;

        public static event Action<string> OnEntered;

        private static readonly HashSet<string> Entered = new();
        private static readonly Dictionary<string, TutorialZone> ById = new();

        public string Id => zoneId;
        public bool PlayerInside { get; private set; }

        public void Configure(string id) => zoneId = id;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(zoneId)) ById[zoneId] = this;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(zoneId) && ById.TryGetValue(zoneId, out TutorialZone zone) && zone == this) ById.Remove(zoneId);
            PlayerInside = false;
        }

        public static bool WasEntered(string id) => Entered.Contains(id);

        public static void Forget(string id) => Entered.Remove(id);

        public static bool IsInside(string id) => ById.TryGetValue(id, out TutorialZone zone) && zone.PlayerInside;

        public static TutorialZone Find(string id) => ById.TryGetValue(id, out TutorialZone zone) ? zone : null;

        private static bool IsLocalPlayer(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null) return false;

            return body.TryGetComponent(out NetworkObject networkObject) && networkObject.IsLocalPlayer;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsLocalPlayer(other)) return;

            PlayerInside = true;

            if (string.IsNullOrEmpty(zoneId)) return;

            Entered.Add(zoneId);
            OnEntered?.Invoke(zoneId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsLocalPlayer(other)) return;

            PlayerInside = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entered.Clear();
            ById.Clear();
            OnEntered = null;
        }
    }
}
