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

        
        private void OnEnable()
        {
            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel += VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel += VivoxManager_OnParticipantLeft;
            }

            zoneState.OnZonePresetChanged += PlayerZoneAudioState_OnZonePresetChanged;
        }
        

        private void VivoxManager_OnParticipantJoined(VivoxParticipant participant)
        {
            if (participant.IsSelf) return;
            if (voiceIdentity == null || participant.PlayerId != voiceIdentity.VivoxPlayerId) return;
            if (_participant != null) return;

            _participant = participant;

            GameObject tapObject = participant.CreateVivoxParticipantTap($"VoiceTap_{participant.DisplayName}", silenceInChannelAudioMix: true);

            if (tapObject == null)
            {
                Debug.LogError($"Failed when creating VivoxParticipantTap for {participant.DisplayName}");
                _participant = null;
                return;
            }

            tapObject.transform.SetParent(transform, worldPositionStays: false);
            tapObject.transform.localPosition = Vector3.zero;

            if (tapObject.TryGetComponent(out AudioSource audioSource))
            {
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = VivoxManager.instance != null ? VivoxManager.instance.ConversationalDistance : 3;
                audioSource.maxDistance = VivoxManager.instance != null ? VivoxManager.instance.AudibleDistance : 15;
            }

            _reverbFilter = tapObject.AddComponent<AudioReverbFilter>();
            _reverbFilter.reverbPreset = zoneState.CurrentPreset;
        }

        private void VivoxManager_OnParticipantLeft(VivoxParticipant participant)
        {
            if (_participant == null || participant.PlayerId != _participant.PlayerId) return;
            
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
        }

        
        private void OnDisable()
        {
            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel -= VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel -= VivoxManager_OnParticipantLeft;
            }

            zoneState.OnZonePresetChanged -= PlayerZoneAudioState_OnZonePresetChanged;
            DestroyTap();
        }
        
        
    }
}