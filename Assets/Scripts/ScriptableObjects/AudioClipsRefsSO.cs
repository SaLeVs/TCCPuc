using UnityEngine;


namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "AudioClips", menuName = "ScriptableObjects/AudioClipsSO")]
    public class AudioClipsRefsSO : ScriptableObject
    {
        public AudioClip[] playerFootsteps;
        public AudioClip[] playerDamage;
        public AudioClip[] playerDeath;
        public AudioClip[] playerGetObject;
        
        public AudioClip[] monsterFootsteps;
        public AudioClip[] monsterDamage;
        public AudioClip[] monsterSabotage;
        public AudioClip[] monsterSeeTarget;
        public AudioClip[] monsterBerserkTarget;
        
        public AudioClip[] lightSabotage;
        
        public AudioClip[] missionSuccess;
        public AudioClip[] missionReceived;
        
    }
}

