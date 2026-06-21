using Components;
using Missions;
using Monster;
using Player;
using ScriptableObjects;
using UnityEngine;

public class SfxManager : MonoBehaviour
{
    [SerializeField] private AudioClipsRefsSO audioClipRefsSO;
    [SerializeField] private Transform mainCamera;
    [SerializeField] private AudioSource audioSource;

    
    private void Start()
    {
        PlayerMovement.OnFootstepSound += PlayerMovement_OnFootStepSound;
        PlayerDead.OnDeathSound += PlayerDead_OnDeathSound;
        Health.OnDamageSound += PlayerHealth_OnDamageSound;
        
        PlayerMissionHolder.OnMissionRecievedSound += PlayerMissionHolder_OnMissionReceivedSound;
        PlayerMissionHolder.OnMissionCompletedSound += PlayerMissionHolder_OnMissionCompletedSound;
        
        MonsterAttack.OnMonsterAttackSound += MonsterAttack_OnMonsterAttack;
        MonsterSabotage.OnSabotageSound += MonsterSabotage_OnSabotageSound;
        MonsterChase.OnMonsterSeeTargetSound += MonsterChase_OnMonsterSeeTargetSound;
        MonsterWander.OnMonsterFootstepSound += MonsterWander_OnMonsterFootstepSound;
    }


    private void PlayerMovement_OnFootStepSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.playerFootsteps, position);
    }
    
    private void PlayerDead_OnDeathSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.playerDeath, position);
    }
    
    private void PlayerHealth_OnDamageSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.playerDamage, position);
    }
    
    private void PlayerMissionHolder_OnMissionReceivedSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.missionReceived, Vector3.zero);
    }
    
    private void PlayerMissionHolder_OnMissionCompletedSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.missionSuccess, Vector3.zero);
    }
    
    private void MonsterAttack_OnMonsterAttack(Vector3 position)
    {
        PlaySound(audioClipRefsSO.monsterAttack, position);
    }
    
    private void MonsterSabotage_OnSabotageSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.monsterSabotage, position);
    }

    private void MonsterChase_OnMonsterSeeTargetSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.monsterSeeTarget, position);
    }
    
    private void MonsterWander_OnMonsterFootstepSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.monsterFootsteps, position);
    }

    public void PlaySound(AudioClip[] audioClipArray, Vector3 position, float volume = 1f)
    {
        if (audioClipArray == null || audioClipArray.Length == 0) return;

        if (position == Vector3.zero)
            position = mainCamera.position;

        AudioClip clip = audioClipArray[Random.Range(0, audioClipArray.Length)];

        GameObject tempGO = new GameObject("TempAudio");
        tempGO.transform.position = position;

        AudioSource aSource = tempGO.AddComponent<AudioSource>();
        aSource.clip = clip;
    
        aSource.volume = volume;
        aSource.spatialBlend = 1f;     
        aSource.rolloffMode = AudioRolloffMode.Linear;
        aSource.minDistance = 10f;           
        aSource.maxDistance = 50f;            
        aSource.playOnAwake = false;
        aSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;

        aSource.Play();
        Destroy(tempGO, clip.length);
    }

    
    private void OnDestroy()
    {
        PlayerMovement.OnFootstepSound -= PlayerMovement_OnFootStepSound;
        PlayerDead.OnDeathSound -= PlayerDead_OnDeathSound;
        Health.OnDamageSound -= PlayerHealth_OnDamageSound;
        
        PlayerMissionHolder.OnMissionRecievedSound -= PlayerMissionHolder_OnMissionReceivedSound;
        PlayerMissionHolder.OnMissionCompletedSound -= PlayerMissionHolder_OnMissionCompletedSound;
        
        MonsterAttack.OnMonsterAttackSound -= MonsterAttack_OnMonsterAttack;
        MonsterSabotage.OnSabotageSound -= MonsterSabotage_OnSabotageSound;
        MonsterChase.OnMonsterSeeTargetSound -= MonsterChase_OnMonsterSeeTargetSound;
        MonsterWander.OnMonsterFootstepSound -= MonsterWander_OnMonsterFootstepSound;
    }
    
}
