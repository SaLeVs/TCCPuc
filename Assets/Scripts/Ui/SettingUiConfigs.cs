using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class SettingUiConfigs : MonoBehaviour
{
    // Post Exposure, in stops, with the brightness slider at 0%, 50% and 100%.
    private const float MIN_POST_EXPOSURE = -0.75f;
    private const float DEFAULT_POST_EXPOSURE = 0.75f;
    private const float MAX_POST_EXPOSURE = 1.25f;

    [SerializeField] private AudioMixer musicMixer;
    [SerializeField] private AudioMixer sfxMixer;
    [SerializeField] private AudioMixer voiceMixer;

    private void Awake()
    {
        ApplyAll();
        SceneManager.sceneLoaded += (_, _) => ApplyAll();
    }

    private void ApplyAll()
    {
        float music = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        float sfx = PlayerPrefs.GetFloat("SoundEffectsVolume", 0.5f);
        float voice = PlayerPrefs.GetFloat("VoiceVolume", 1f);
        float brightness = PlayerPrefs.GetFloat("Brightness", 0.5f);

        musicMixer.SetFloat("musicVolume", LinearToDecibel(music));
        sfxMixer.SetFloat("sfxVolume", LinearToDecibel(sfx));
        if (voiceMixer != null) voiceMixer.SetFloat("voiceVolume", LinearToDecibel(voice));

        var volume = FindFirstObjectByType<Volume>();
        if (volume != null && volume.profile != null && volume.profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments.postExposure.value = BrightnessToPostExposure(brightness);
        }
    }

    public static float BrightnessToPostExposure(float brightness) =>
        brightness < 0.5f
            ? Mathf.Lerp(MIN_POST_EXPOSURE, DEFAULT_POST_EXPOSURE, brightness * 2f)
            : Mathf.Lerp(DEFAULT_POST_EXPOSURE, MAX_POST_EXPOSURE, (brightness - 0.5f) * 2f);

    private float LinearToDecibel(float linear) =>
        linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
}
