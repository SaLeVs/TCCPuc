using ScriptableObjects;
using UnityEngine;

public class SfxManager : MonoBehaviour
{
    [SerializeField] private AudioClipsRefsSO audioClipRefsSO;
    [SerializeField] private Transform mainCamera;
    [SerializeField] private AudioSource audioSource;

    
    private void Start()
    {
        
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

        aSource.Play();
        Destroy(tempGO, clip.length);
    }

    
    private void OnDestroy()
    {
        
    }
    
}
