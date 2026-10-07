using Components.Perception;
using UnityEngine;

namespace Components.Sound
{
    [CreateAssetMenu(fileName = "Sound", menuName = "ScriptableObjects/Audio/Sound Definition")]
    public class SoundDefinitionSO : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("One is picked at random every time, so a repeated sound doesn't sound mechanical.")]
        [SerializeField] private AudioClip[] clips;

        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        [Tooltip("Random pitch between these two, per play. (1, 1) keeps it untouched.")]
        [SerializeField] private Vector2 pitchRange = Vector2.one;

        [Tooltip("Same scale as AudioSource.priority: 0 matters most, 256 least. When every voice is " +
                 "busy, the least important sound playing gives its voice up — never a more important one.")]
        [SerializeField, Range(0, 256)] private int priority = 128;

        [Header("World sounds only")]
        [Tooltip("How far it carries, in meters. Past this nobody hears it — not the players and not the " +
                 "monster. A louder sound is also louder at every distance, not only audible further.")]
        [SerializeField, Min(0.1f)] private float range = 10f;

        [Tooltip("Meters above the point it is played at. Lifts a roar from the monster's feet to its " +
                 "mouth, and a footstep just off the floor.")]
        [SerializeField] private float heightOffset;

        [Tooltip("Keeps following whoever made it while it plays — a roar moves with the monster.")]
        [SerializeField] private bool followEmitter;

        [Header("Monster hearing")]
        [Tooltip("Reported to the monster's hearing with the same range. Off for the monster's own sounds, " +
                 "or it would go and investigate itself.")]
        [SerializeField] private bool alertsMonster;

        [SerializeField] private NoiseType noiseType = NoiseType.Interaction;

        public float Volume => volume;
        public int Priority => priority;
        public float Range => range;
        public float HeightOffset => heightOffset;
        public bool FollowEmitter => followEmitter;
        public bool AlertsMonster => alertsMonster;
        public NoiseType NoiseType => noiseType;

        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;

            return clips[Random.Range(0, clips.Length)];
        }

        public float PickPitch()
        {
            return Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        }

        private void OnValidate()
        {
            // A pitch of 0 never finishes playing, and a negative one plays backwards.
            pitchRange.x = Mathf.Max(0.05f, pitchRange.x);
            pitchRange.y = Mathf.Max(0.05f, pitchRange.y);
        }
    }
}
