using Network;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace Player
{
    public class VivoxPlayer : NetworkBehaviour
    {
        [SerializeField] private Transform earTransform;

        private PlayerVoiceIdentity _identity;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            enabled = true;

            // A LAN match names its channel after the key the host hands out on this same player,
            // which may land after this spawn: it asks again when it does.
            _identity = GetComponentInChildren<PlayerVoiceIdentity>(true);
            if (_identity != null) _identity.OnVoiceSessionKeyChanged += Identity_OnVoiceSessionKeyChanged;

            EnterVoice();
        }

        public override void OnNetworkDespawn()
        {
            if (_identity != null) _identity.OnVoiceSessionKeyChanged -= Identity_OnVoiceSessionKeyChanged;
        }

        private void Identity_OnVoiceSessionKeyChanged(string key) => EnterVoice();

        private void EnterVoice()
        {
            // The tutorial is played alone: it never had voice, and a channel there would only let the
            // microphone wake its monster.
            if (gameObject.scene.name.Contains("Tutorial") || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Tutorial")) return;

            VivoxManager.instance?.EnterGameVoice(_identity != null ? _identity.VoiceSessionKey : null);
        }

        private void Update()
        {
            if (!IsOwner) return;

            VivoxManager vivoxManager = VivoxManager.instance;
            
            if (vivoxManager == null) return;
            if (!vivoxManager.IsInPositionalChannel) return;
            if (!VivoxService.Instance.IsLoggedIn) return;

            VivoxService.Instance.Set3DPosition(gameObject.transform.position, earTransform.position, earTransform.forward, Vector3.up, vivoxManager.CurrentChannelName);
        }
    }
}
