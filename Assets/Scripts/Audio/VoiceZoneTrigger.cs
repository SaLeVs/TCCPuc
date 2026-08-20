using UnityEngine;

namespace Audio
{
    [RequireComponent(typeof(Collider))]
    public class VoiceZoneTrigger : MonoBehaviour
    {
        [SerializeField] private AudioReverbPreset reverbPreset = AudioReverbPreset.Concerthall;
        [SerializeField] private Collider zoneCollider;

        public AudioReverbPreset ReverbPreset => reverbPreset;
        
        
        private void OnTriggerEnter(Collider other)
        {
            PlayerZoneAudioState playerZoneAudioState = other.GetComponentInChildren<PlayerZoneAudioState>();

            if (playerZoneAudioState == null) return;

            playerZoneAudioState.EnterZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerZoneAudioState playerZoneAudioState = other.GetComponentInChildren<PlayerZoneAudioState>();

            if (playerZoneAudioState == null) return;

            playerZoneAudioState.ExitZone(this);
        }
        
    }
}