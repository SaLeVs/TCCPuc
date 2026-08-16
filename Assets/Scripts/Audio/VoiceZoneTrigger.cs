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
            if (other.TryGetComponent(out PlayerZoneAudioState zoneState))
            {
                zoneState.EnterZone(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlayerZoneAudioState zoneState))
            {
                zoneState.ExitZone(this);
            }
        }
        
    }
}