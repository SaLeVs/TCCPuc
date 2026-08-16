using System;
using System.Collections.Generic;
using UnityEngine;

namespace Audio
{
    public class PlayerZoneAudioState : MonoBehaviour
    {
        public event Action<AudioReverbPreset> OnZonePresetChanged;
        public AudioReverbPreset CurrentPreset => _activeZones.Count > 0 ? _activeZones.Peek().ReverbPreset : AudioReverbPreset.Off;
        
        private readonly Stack<VoiceZoneTrigger> _activeZones = new Stack<VoiceZoneTrigger>();
        
        
        public void EnterZone(VoiceZoneTrigger zone)
        {
            _activeZones.Push(zone);
            OnZonePresetChanged?.Invoke(CurrentPreset);
        }

        public void ExitZone(VoiceZoneTrigger zone)
        {
            if (!_activeZones.Contains(zone)) return;

            VoiceZoneTrigger[] remaining = _activeZones.ToArray();

            _activeZones.Clear();

            for (int i = remaining.Length - 1; i >= 0; i--)
            {
                if (remaining[i] != zone)
                {
                    _activeZones.Push(remaining[i]);
                }
            }

            OnZonePresetChanged?.Invoke(CurrentPreset);
        }
        
    }
}
