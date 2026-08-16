using Network;
using Unity.Services.Vivox;
using UnityEngine;

namespace Audio
{
    public class RemoteVoiceFilter : MonoBehaviour
    {
        [SerializeField] private PlayerVoiceIdentity voiceIdentity;
        [SerializeField] private PlayerZoneAudioState zoneState;

        private VivoxParticipant _participant;
        private AudioReverbFilter _reverbFilter;
        private AudioSource _audioSource;

        
        private void OnEnable()
        {
            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel += VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel += VivoxManager_OnParticipantLeft;
            }
            
            if (zoneState != null)
            {
                zoneState.OnZonePresetChanged += PlayerZoneAudioState_OnZonePresetChanged;
            }
        }
        

        private void VivoxManager_OnParticipantJoined(VivoxParticipant participant)
        {
            if (participant.IsSelf) return;
            if (voiceIdentity == null) return;
            if (participant.PlayerId != voiceIdentity.VivoxPlayerId) return;
            if (_participant != null) return;
            
            _participant = participant;

            GameObject tapObject = participant.CreateVivoxParticipantTap($"VoiceTap_{participant.DisplayName}", silenceInChannelAudioMix: true);

            if (tapObject == null)
            {
                _participant = null;
                return;
            }

            tapObject.transform.SetParent(transform, false);
            tapObject.transform.localPosition = Vector3.zero;

            _audioSource = tapObject.GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                _audioSource = tapObject.GetComponentInChildren<AudioSource>();
            }

            if (_audioSource == null) return;

            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;

            if (VivoxManager.instance != null)
            {
                _audioSource.minDistance = VivoxManager.instance.ConversationalDistance;
                _audioSource.maxDistance = VivoxManager.instance.AudibleDistance;
            }

            _reverbFilter = _audioSource.gameObject.AddComponent<AudioReverbFilter>();

            AudioReverbPreset initialPreset = zoneState != null ? zoneState.CurrentPreset : AudioReverbPreset.Off;
            _reverbFilter.reverbPreset = initialPreset;
        }

        private void VivoxManager_OnParticipantLeft(VivoxParticipant participant)
        {
            if (_participant == null) return;
            if (participant.PlayerId != _participant.PlayerId) return;

            DestroyTap();
        }
        
        private void PlayerZoneAudioState_OnZonePresetChanged(AudioReverbPreset preset)
        {
            if (_reverbFilter != null)
            {
                _reverbFilter.reverbPreset = preset;
            }
        }
        
        private void DestroyTap()
        {
            _participant?.DestroyVivoxParticipantTap();
            _participant = null;
            _reverbFilter = null;
            _audioSource = null;
        }
        
        
        private void OnDisable()
        {
            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel -= VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel -= VivoxManager_OnParticipantLeft;
            }

            if (zoneState != null)
            {
                zoneState.OnZonePresetChanged -= PlayerZoneAudioState_OnZonePresetChanged;
            }
            
            DestroyTap();
        }
        
    }
}