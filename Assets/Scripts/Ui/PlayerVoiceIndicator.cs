using Network;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// The speaker next to a player's name: still while they are quiet, with waves while they talk,
    /// and not there at all while they have no voice connection.
    /// </summary>
    /// <remarks>
    /// Read on every machine from how loud that player sounds there, so it costs no RPC. For other
    /// players that is RemoteVoiceFilter's measurement of their tap, through
    /// VivoxManager.GetHeardEnergy: Vivox leaves their AudioEnergy at 0 and their SpeechDetected
    /// false once tapped, which is why the icon never lit. For this machine's own player it is
    /// AudioEnergy. A whisper lights it up too, and whoever is watching wants to know that.
    /// </remarks>
    public class PlayerVoiceIndicator : MonoBehaviour
    {
        [Tooltip("Whose voice this shows. Missing where there is no voice session, like the GYM: " +
                 "then the icon stays hidden.")]
        [SerializeField] private PlayerVoiceIdentity voiceIdentity;

        [SerializeField] private Image icon;
        [SerializeField] private Sprite silentSprite;
        [SerializeField] private Sprite speakingSprite;
        [SerializeField] private Color silentColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private Color speakingColor = Color.white;

        [Tooltip("Seconds the waves stay up after the voice stops, so they don't flicker between words.")]
        [SerializeField, Min(0f)] private float releaseSeconds = 0.35f;

        [Tooltip("Energy (0..1, Vivox's AudioEnergy scale) from which the player counts as talking. " +
                 "Silence reads 0; a whisper is around 0,3.")]
        [SerializeField, Range(0f, 1f)] private float speakingEnergy = 0.2f;

        // Finding the participant is a walk over the channel, so until it is found it is looked
        // for a few times a second rather than every frame.
        private const float BindRetryInterval = 0.5f;

        private VivoxManager _vivox;
        private VivoxParticipant _participant;
        private float _nextBindTime;
        private float _speakingUntil;


        private void OnEnable()
        {
            // Only looked up when there is a voice to show: the lookup logs an error where there is
            // no VivoxManager at all.
            if (voiceIdentity == null) return;

            _vivox = VivoxManager.instance;
            if (_vivox == null) return;

            _vivox.OnParticipantLeftChannel += VivoxManager_OnParticipantLeft;
            _vivox.OnLeftChannel += VivoxManager_OnLeftChannel;
        }

        private void Update()
        {
            if (_participant == null) TryBind();

            bool isConnected = _participant != null;

            if (icon.gameObject.activeSelf != isConnected) icon.gameObject.SetActive(isConnected);
            if (!isConnected) return;

            if (IsSpeaking()) _speakingUntil = Time.unscaledTime + releaseSeconds;

            bool showSpeaking = Time.unscaledTime < _speakingUntil;
            Sprite sprite = showSpeaking ? speakingSprite : silentSprite;

            if (icon.sprite == sprite) return;

            icon.sprite = sprite;
            icon.color = showSpeaking ? speakingColor : silentColor;
        }

        private bool IsSpeaking()
        {
            try
            {
                // SpeechDetected is no help: always false for a tapped participant, always true for
                // this machine's own (Vivox's detection kept firing in silence in the same test).
                float energy = Mathf.Max((float)_participant.AudioEnergy, _vivox.GetHeardEnergy(_participant.PlayerId));
                return energy >= speakingEnergy;
            }
            catch (System.NullReferenceException)
            {
                // The participant can go invalid between leaving the channel and this frame.
                _participant = null;
                return false;
            }
        }

        private void TryBind()
        {
            if (voiceIdentity == null) return;
            if (Time.unscaledTime < _nextBindTime) return;

            _nextBindTime = Time.unscaledTime + BindRetryInterval;

            if (_vivox == null) return;

            // Empty until the owner's id has replicated.
            string playerId = voiceIdentity.VivoxPlayerId;
            if (string.IsNullOrEmpty(playerId)) return;

            foreach (VivoxParticipant participant in _vivox.CurrentParticipants)
            {
                if (participant.PlayerId != playerId) continue;

                _participant = participant;
                return;
            }
        }

        private void VivoxManager_OnParticipantLeft(VivoxParticipant participant)
        {
            // By reference: the one held here may already be dead, and reading its PlayerId throws.
            if (_participant == null || participant != _participant) return;

            _participant = null;
        }

        /// <summary>
        /// A channel switch drops everyone without a word from Vivox: found again on the next retry.
        /// </summary>
        private void VivoxManager_OnLeftChannel(string channelName)
        {
            _participant = null;
        }

        private void OnDisable()
        {
            if (_vivox != null)
            {
                _vivox.OnParticipantLeftChannel -= VivoxManager_OnParticipantLeft;
                _vivox.OnLeftChannel -= VivoxManager_OnLeftChannel;
            }

            _vivox = null;
            _participant = null;
        }
    }
}
