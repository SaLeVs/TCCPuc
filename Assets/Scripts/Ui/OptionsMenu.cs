using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    public static event Action<float> OnSensibilityChanged;
    
    [SerializeField] private Slider sensibilitySlider;
    [SerializeField] private TextMeshProUGUI sensibilityValueText;
    
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeValueText;
    
    [SerializeField] private Slider soundEffectsVolumeSlider;
    [SerializeField] private TextMeshProUGUI soundEffectsVolumeValueText;

    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private TextMeshProUGUI brightnessValueText;
    
    [SerializeField] private GameObject rebindMenu;
    [SerializeField] private GameObject closeMenuButton;
    
    [SerializeField] private AudioMixer musicMixer;
    [SerializeField] private AudioMixer sfxMixer;
    
    [SerializeField] private Image darkPreviewImage;
    [SerializeField] private Image lightPreviewImage;
    [SerializeField] private GameObject brightnessPanel;
    
    
    private const string SENSIBILITY_KEY = "MouseSensibility";
    
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SOUND_EFFECTS_VOLUME_KEY = "SoundEffectsVolume";
    
    private const string BRIGHTNESS_KEY = "Brightness";
    
    
    private float _sensibility;
    
    private float _musicVolume;
    private float _soundEffectsVolume;
    
    private Volume globalVolume;
    private ColorAdjustments _colorAdjustments;
    private float _brightness;


    private void Awake()
    {
        _sensibility = PlayerPrefs.GetFloat(SENSIBILITY_KEY, 0.5f);
        
        _musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.5f);
        _soundEffectsVolume = PlayerPrefs.GetFloat(SOUND_EFFECTS_VOLUME_KEY, 0.5f);
        
        _brightness = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, 0.5f);
        
        musicMixer.SetFloat("musicVolume", LinearToDecibel(_musicVolume));
        sfxMixer.SetFloat("sfxVolume", LinearToDecibel(_soundEffectsVolume));
        
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
        brightnessSlider.SetValueWithoutNotify(_brightness);
        
        ApplyBrightness(_brightness);
        UpdateBrightnessPreview(_brightness);
        
        gameObject.SetActive(false);
    }
    
    private void OnEnable()
    {
        UpdateSensibilityText(sensibilityValueText, _sensibility);
        
        UpdateVolumeText(musicVolumeValueText, _musicVolume);
        UpdateVolumeText(soundEffectsVolumeValueText, _soundEffectsVolume);
        UpdateBrightnessText(_brightness);
        
        SensibilitySlider_OnSensibilityChanged(_sensibility);
    }

    public void SensibilitySlider_OnSensibilityChanged(float sensibilityValue)
    {
        PlayerPrefs.SetFloat(SENSIBILITY_KEY, sensibilityValue);
        UpdateSensibilityText(sensibilityValueText, sensibilityValue);
        OnSensibilityChanged?.Invoke(sensibilityValue);
    }
    

    public void MusicSlider_OnValueChanged(float volumeValue)
    {
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volumeValue);
        UpdateVolumeText(musicVolumeValueText, volumeValue);
        musicMixer.SetFloat("musicVolume", LinearToDecibel(volumeValue));
    }
    
    public void SoundEffectsSlider_OnValueChanged(float volumeValue)
    {
        PlayerPrefs.SetFloat(SOUND_EFFECTS_VOLUME_KEY, volumeValue);
        UpdateVolumeText(soundEffectsVolumeValueText, volumeValue);   
        sfxMixer.SetFloat("sfxVolume", LinearToDecibel(volumeValue));
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

        float postExposure = Mathf.Lerp(-2f, 2f, value);
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
    
}
