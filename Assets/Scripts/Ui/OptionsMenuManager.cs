using System;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OptionsMenuManager : MonoBehaviour
{
    public static event Action<float> OnSensibilityChanged;
    
    [SerializeField] private Slider sensibilitySlider;
    [SerializeField] private TextMeshProUGUI sensibilityValueText;
    
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeValueText;
    
    [SerializeField] private Slider soundEffectsVolumeSlider;
    [SerializeField] private TextMeshProUGUI soundEffectsVolumeValueText;

    [Tooltip("Every other player's voice, through the voice mixer's voiceVolume.")]
    [SerializeField] private Slider voiceVolumeSlider;
    [SerializeField] private TextMeshProUGUI voiceVolumeValueText;

    [Tooltip("The donation readings only, wherever they come from.")]
    [SerializeField] private Slider donationTtsVolumeSlider;
    [SerializeField] private TextMeshProUGUI donationTtsVolumeValueText;
    [SerializeField] private TextMeshProUGUI donationTtsToggleText;
    [SerializeField] private string donationTtsOnLabel = "LER DOAÇÕES: SIM";
    [SerializeField] private string donationTtsOffLabel = "LER DOAÇÕES: NÃO";

    [Tooltip("Optional: the label of a button that runs ToggleMicTest, for a device panel that is always on screen.")]
    [SerializeField] private TextMeshProUGUI micTestButtonText;
    [SerializeField] private string startMicTestLabel = "TESTAR MICROFONE";
    [SerializeField] private string stopMicTestLabel = "PARAR TESTE";

    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private TextMeshProUGUI brightnessValueText;
    
    [SerializeField] private GameObject rebindMenu;
    [SerializeField] private GameObject closeMenuButton;
    
    [SerializeField] private AudioMixer musicMixer;
    [SerializeField] private AudioMixer sfxMixer;
    [SerializeField] private AudioMixer voiceMixer;

    [SerializeField] private Image darkPreviewImage;
    [SerializeField] private Image lightPreviewImage;
    [SerializeField] private GameObject brightnessPanel;
    
    [SerializeField] private GameObject audioDevicePanel;
    [SerializeField] private GameObject creditsPanel;
    
    
    private const string SENSIBILITY_KEY = "MouseSensibility";
    
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SOUND_EFFECTS_VOLUME_KEY = "SoundEffectsVolume";
    private const string VOICE_VOLUME_KEY = "VoiceVolume";

    private const string BRIGHTNESS_KEY = "Brightness";
    
    
    private float _sensibility;
    
    private float _musicVolume;
    private float _soundEffectsVolume;
    private float _voiceVolume;
    private bool _isMicTestRunning;

    private Volume globalVolume;
    private ColorAdjustments _colorAdjustments;
    private float _brightness;


    private void Awake()
    {
        _sensibility = PlayerPrefs.GetFloat(SENSIBILITY_KEY, 0.5f);
        
        _musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.5f);
        _soundEffectsVolume = PlayerPrefs.GetFloat(SOUND_EFFECTS_VOLUME_KEY, 0.5f);
        _voiceVolume = PlayerPrefs.GetFloat(VOICE_VOLUME_KEY, 1f);

        _brightness = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, 0.5f);

        musicMixer.SetFloat("musicVolume", LinearToDecibel(_musicVolume));
        sfxMixer.SetFloat("sfxVolume", LinearToDecibel(_soundEffectsVolume));
        if (voiceMixer != null) voiceMixer.SetFloat("voiceVolume", LinearToDecibel(_voiceVolume));
        
        if (globalVolume == null)
        {
            globalVolume = FindFirstObjectByType<Volume>();
        }

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _colorAdjustments);
        }
        
        sensibilitySlider.value = _sensibility;
        
        musicVolumeSlider.value = _musicVolume;
        soundEffectsVolumeSlider.value = _soundEffectsVolume;
        if (voiceVolumeSlider != null) voiceVolumeSlider.SetValueWithoutNotify(_voiceVolume);
        if (donationTtsVolumeSlider != null) donationTtsVolumeSlider.SetValueWithoutNotify(VoiceChatSettings.DonationTtsVolume);
        brightnessSlider.SetValueWithoutNotify(_brightness);
        
        ApplyBrightness(_brightness);
        UpdateBrightnessPreview(_brightness);
        
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnEnable()
    {
        UpdateSensibilityText(sensibilityValueText, _sensibility);
        
        UpdateVolumeText(musicVolumeValueText, _musicVolume);
        UpdateVolumeText(soundEffectsVolumeValueText, _soundEffectsVolume);
        UpdateVolumeText(voiceVolumeValueText, _voiceVolume);
        UpdateVolumeText(donationTtsVolumeValueText, VoiceChatSettings.DonationTtsVolume);
        UpdateDonationTtsToggleText();
        UpdateMicTestText();
        UpdateBrightnessText(_brightness);

        SensibilitySlider_OnSensibilityChanged(_sensibility);
    }

    public void SensibilitySlider_OnSensibilityChanged(float sensibilityValue)
    {
        _sensibility = sensibilityValue;
        PlayerPrefs.SetFloat(SENSIBILITY_KEY, sensibilityValue);
        SensibilitySettings.Set(sensibilityValue);
        UpdateSensibilityText(sensibilityValueText, sensibilityValue);
        OnSensibilityChanged?.Invoke(sensibilityValue);
    }

    public void MusicSlider_OnValueChanged(float volumeValue)
    {
        _musicVolume = volumeValue;
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volumeValue);
        UpdateVolumeText(musicVolumeValueText, volumeValue);
        musicMixer.SetFloat("musicVolume", LinearToDecibel(volumeValue));
    }
    
    public void SoundEffectsSlider_OnValueChanged(float volumeValue)
    {
        _soundEffectsVolume = volumeValue;
        PlayerPrefs.SetFloat(SOUND_EFFECTS_VOLUME_KEY, volumeValue);
        UpdateVolumeText(soundEffectsVolumeValueText, volumeValue);
        sfxMixer.SetFloat("sfxVolume", LinearToDecibel(volumeValue));
    }

    public void VoiceSlider_OnValueChanged(float volumeValue)
    {
        _voiceVolume = volumeValue;
        PlayerPrefs.SetFloat(VOICE_VOLUME_KEY, volumeValue);
        UpdateVolumeText(voiceVolumeValueText, volumeValue);
        if (voiceMixer != null) voiceMixer.SetFloat("voiceVolume", LinearToDecibel(volumeValue));
    }

    public void DonationTtsSlider_OnValueChanged(float volumeValue)
    {
        VoiceChatSettings.SetDonationTtsVolume(volumeValue);
        UpdateVolumeText(donationTtsVolumeValueText, volumeValue);
    }

    public void ToggleDonationTts()
    {
        VoiceChatSettings.SetDonationTtsEnabled(!VoiceChatSettings.DonationTtsEnabled);
        UpdateDonationTtsToggleText();
    }

    private void UpdateDonationTtsToggleText()
    {
        if (donationTtsToggleText == null) return;

        donationTtsToggleText.SetText(VoiceChatSettings.DonationTtsEnabled ? donationTtsOnLabel : donationTtsOffLabel);
    }
    
    public void BrightnessSlider_OnValueChanged(float value)
    {
        _brightness = value;
        PlayerPrefs.SetFloat(BRIGHTNESS_KEY, value);

        UpdateBrightnessText(value);
        ApplyBrightness(value);
        UpdateBrightnessPreview(value);
    }
    
    private void ApplyBrightness(float value)
    {
        if (_colorAdjustments == null) return;

        float postExposure = SettingUiConfigs.BrightnessToPostExposure(value);
        _colorAdjustments.postExposure.value = postExposure;
    }

    private void UpdateBrightnessPreview(float value)
    {
        if (darkPreviewImage != null)
        {
            Color color = darkPreviewImage.color;
            color.a = 1f - value;
            darkPreviewImage.color = color;
        }

        if (lightPreviewImage != null)
        {
            Color color = lightPreviewImage.color;
            color.a = value;
            lightPreviewImage.color = color;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _colorAdjustments = null;
        globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _colorAdjustments);
        }

        ApplyBrightness(_brightness);
    }
    
    private void UpdateBrightnessText(float value)
    {
        int percent = Mathf.RoundToInt(value * 100f);
        brightnessValueText.SetText(percent + "%");
    }

    private void UpdateSensibilityText(TextMeshProUGUI sensibilityText,float savedSensibility)
    {
        sensibilityText.SetText(savedSensibility.ToString("F2"));
    }

    private void UpdateVolumeText(TextMeshProUGUI volumeText, float savedVolume)
    {
        if (volumeText == null) return;

        int percent = Mathf.RoundToInt(savedVolume * 100f);
        volumeText.SetText(percent + "%");
    }
    
    
    private float LinearToDecibel(float linear)
    {
        if (linear <= 0.0001f)
            return -80f;

        return Mathf.Log10(linear) * 20f;
    }
    
    public void CloseOptionsMenu()
    {
        gameObject.SetActive(false);
    }
    
    public void OpenRebindMenu()
    {
        rebindMenu.SetActive(true);
        closeMenuButton.SetActive(false);
    }
    
    public void CloseRebindMenu()
    {
        closeMenuButton.SetActive(true);
        rebindMenu.SetActive(false);
    }

    public void OpenBrightnessPanel()
    {
        brightnessPanel.SetActive(true);
    }

    public void CloseBrightnessPanel()
    {
        brightnessPanel.SetActive(false);
    }

    public void OpenAudioDevicePanel()
    {
        audioDevicePanel.SetActive(true);
        StartMicTest();
    }

    public void CloseAudioDevicePanel()
    {
        audioDevicePanel.SetActive(false);
        StopMicTest();
    }

    /// <summary>
    /// For a device panel that is always on screen, like the main menu's: starts and stops the echo
    /// test without opening or closing anything.
    /// </summary>
    public void ToggleMicTest()
    {
        if (_isMicTestRunning) StopMicTest();
        else StartMicTest();
    }

    private void StartMicTest()
    {
        if (_isMicTestRunning) return;

        _isMicTestRunning = true;
        VivoxManager.instance?.EnterTestVoiceChannel();
        UpdateMicTestText();
    }

    private void StopMicTest()
    {
        if (!_isMicTestRunning) return;

        _isMicTestRunning = false;
        VivoxManager.instance?.LeaveTestVoiceChannel();
        UpdateMicTestText();
    }

    private void UpdateMicTestText()
    {
        if (micTestButtonText == null) return;

        micTestButtonText.SetText(_isMicTestRunning ? stopMicTestLabel : startMicTestLabel);
    }
    
    public void OpenCreditsPanel()
    {
        creditsPanel.SetActive(true);
    }
    
    public void CloseCreditsPanel()
    {
        creditsPanel.SetActive(false);
    }

    /// <summary>
    /// Closes whatever was opened inside the options through its own close, so the next opening
    /// starts on the main page — and an open mic test gives the voice channel back.
    /// </summary>
    public void CloseSubPanels()
    {
        if (rebindMenu != null && rebindMenu.activeSelf) CloseRebindMenu();
        if (brightnessPanel != null && brightnessPanel.activeSelf) CloseBrightnessPanel();
        if (audioDevicePanel != null && audioDevicePanel.activeSelf) CloseAudioDevicePanel();
        if (creditsPanel != null && creditsPanel.activeSelf) CloseCreditsPanel();
    }

    private void OnDisable()
    {
        // However the options were closed — Fechar, the pause closing, a scene change — a running
        // test hands the mic back to the lobby or the match.
        StopMicTest();
        PlayerPrefs.Save();
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
}


