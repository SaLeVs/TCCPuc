using System.Collections;
using Components;
using Missions;
using Monster;
using Player;
using ScriptableObjects;
using UnityEngine;

public class SfxManager : MonoBehaviour
{
    [SerializeField] private float sfxMinDistance = 1f;
    [SerializeField] private float sfxMaxDistance = 20f;
    
    [SerializeField] private LayerMask occlusionLayers;
    [SerializeField] private float occludedCutoffFrequency = 500f;
    [SerializeField] private float openCutoffFrequency = 22000f;  
    [SerializeField] private float occlusionCheckInterval = 0.1f; 
    
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
        MonsterBrain.OnMonsterFootstepSound += MonsterBrain_OnMonsterFootstepSound;
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
    
    private void MonsterBrain_OnMonsterFootstepSound(Vector3 position)
    {
        PlaySound(audioClipRefsSO.monsterFootsteps, position);
    }

    public void PlaySound(AudioClip[] audioClipArray, Vector3 position, float volume = 1f)
    {
        if (audioClipArray == null || audioClipArray.Length == 0) return;

        bool isUiSound = position == Vector3.zero;

        if (isUiSound)
        {
            position = mainCamera.position;
        }

        AudioClip clip = audioClipArray[Random.Range(0, audioClipArray.Length)];

        GameObject tempGO = new GameObject("TempAudio");
        tempGO.transform.position = position;

        AudioSource aSource = tempGO.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume;
        aSource.spatialBlend = isUiSound ? 0f : 1f; 
        aSource.rolloffMode = AudioRolloffMode.Logarithmic;
        aSource.minDistance = sfxMinDistance;
        aSource.maxDistance = sfxMaxDistance;
        aSource.playOnAwake = false;
        aSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;

        AudioLowPassFilter lowPass = tempGO.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = openCutoffFrequency;

        aSource.Play();

        if (!isUiSound)
        {
            StartCoroutine(HandleOcclusion(tempGO, aSource, lowPass));
        }

        Destroy(tempGO, clip.length);
    }

    private IEnumerator HandleOcclusion(GameObject tempGO, AudioSource aSource, AudioLowPassFilter lowPass)
    {
        while (tempGO != null && aSource.isPlaying)
        {
            Vector3 direction = mainCamera.position - tempGO.transform.position;
            bool occluded = Physics.Raycast(
                tempGO.transform.position,
                direction.normalized,
                direction.magnitude,
                occlusionLayers
            );

            lowPass.cutoffFrequency = occluded ? occludedCutoffFrequency : openCutoffFrequency;

            yield return new WaitForSeconds(occlusionCheckInterval);
        }
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
        MonsterBrain.OnMonsterFootstepSound -= MonsterBrain_OnMonsterFootstepSound;
    }
    
}
