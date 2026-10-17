using System;
using Interfaces;
using UnityEngine;

namespace LevelDesign.Gym
{
    /// <summary>
    /// Stand-in for AudienceManager, which needs the whole mission system behind it. The monster
    /// only reads a 0–1 audience to decide whether sabotage is unlocked, so the GYM hands it a
    /// slider: push it above the monster's unlock threshold to test door and light sabotage.
    /// </summary>
    public class GymAudienceProvider : MonoBehaviour, IAudienceProvider
    {
        public event Action<float> OnAudienceChanged;

        [Tooltip("0 = ninguém assistindo, 1 = audiência máxima. O monstro sabota acima do sabotageUnlockThreshold dele (0,5).")]
        [SerializeField, Range(0f, 1f)] private float normalizedAudience;

        private float _lastSent = -1f;

        public float NormalizedAudience => normalizedAudience;

        public void Set(float value)
        {
            normalizedAudience = Mathf.Clamp01(value);
            Notify();
        }

        private void Update()
        {
            // Lets the inspector slider drive it live in Play Mode.
            if (!Mathf.Approximately(_lastSent, normalizedAudience)) Notify();
        }

        private void Notify()
        {
            _lastSent = normalizedAudience;
            OnAudienceChanged?.Invoke(normalizedAudience);
        }
    }
}
