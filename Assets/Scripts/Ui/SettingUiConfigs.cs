using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class SettingUiConfigs : MonoBehaviour
{
    [SerializeField] private AudioMixer musicMixer;
    [SerializeField] private AudioMixer sfxMixer;

    private void Awake()
    {
        ApplyAll();
        SceneManager.sceneLoaded += (_, _) => ApplyAll();
    }

    private void ApplyAll()
    {
        float music = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        float sfx = PlayerPrefs.GetFloat("SoundEffectsVolume", 0.5f);
        float brightness = PlayerPrefs.GetFloat("Brightness", 0.5f);

        musicMixer.SetFloat("musicVolume", LinearToDecibel(music));
        sfxMixer.SetFloat("sfxVolume", LinearToDecibel(sfx));

        var volume = FindFirstObjectByType<Volume>();
        if (volume != null && volume.profile != null && volume.profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments.postExposure.value = Mathf.Lerp(-2f, 2f, brightness);
        }
    }

    private float LinearToDecibel(float linear) =>
        linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
}
