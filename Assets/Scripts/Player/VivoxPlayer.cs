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
        private PlayerState _state;
        private AudioListener _listener;

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

            Vector3 speakerPosition = gameObject.transform.position;
            Transform ears = earTransform;

            // Out of play the body is no place to be: a dead player's colliders are off and the body
            // keeps falling through the floor, thousands of meters down within the minute. Vivox drops
            // anyone past the channel's audible distance, so the dead stopped hearing the living and
            // each other some fifteen seconds after dying. They listen through the spectator camera,
            // and that is where they are for Vivox now. Nobody inside hears them anyway, and between
            // the dead the voice has no position (RemoteVoiceFilter).
            if (IsOutOfPlay() && TryGetListener(out AudioListener listener))
            {
                ears = listener.transform;
                speakerPosition = ears.position;
            }

            VivoxService.Instance.Set3DPosition(speakerPosition, ears.position, ears.forward, Vector3.up, vivoxManager.CurrentChannelName);
        }

        private bool IsOutOfPlay()
        {
            if (_state == null) _state = GetComponentInParent<PlayerState>();
            return _state != null && !_state.IsHuntable;
        }

        private bool TryGetListener(out AudioListener listener)
        {
            if (_listener == null || !_listener.isActiveAndEnabled) _listener = FindFirstObjectByType<AudioListener>();

            listener = _listener;
            return listener != null;
        }
    }
}
