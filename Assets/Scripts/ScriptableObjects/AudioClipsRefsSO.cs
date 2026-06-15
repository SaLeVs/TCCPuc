using UnityEngine;


namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "AudioClips", menuName = "ScriptableObjects/Game/AudioClipsSO")]
    public class AudioClipsRefsSO : ScriptableObject
    {
        public AudioClip[] playerFootsteps;
        public AudioClip[] playerDamage;
        public AudioClip[] playerDeath;
        
        public AudioClip[] monsterFootsteps;
        public AudioClip[] monsterAttack;
        public AudioClip[] monsterSabotage;
        public AudioClip[] monsterSeeTarget;
        
        public AudioClip[] missionSuccess;
        public AudioClip[] missionReceived;
        
    }
}

