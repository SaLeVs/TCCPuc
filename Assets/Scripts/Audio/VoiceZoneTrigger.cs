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
            other.GetComponentInChildren<PlayerZoneAudioState>().EnterZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            other.GetComponentInChildren<PlayerZoneAudioState>().ExitZone(this);
        }
        
    }
}