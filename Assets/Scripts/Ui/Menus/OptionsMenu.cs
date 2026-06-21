using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    public static event Action<float> OnSensibilityChanged;
    public static event Action<float> OnAimSensibilityChanged;
    
    [SerializeField] private Slider sensibilitySlider;
    [SerializeField] private Slider aimSensibilitySlider;
    
    [SerializeField] private TextMeshProUGUI sensibilityValueText;
    [SerializeField] private TextMeshProUGUI aimSensibilityValueText;
    
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider soundEffectsVolumeSlider;
    
    [SerializeField] private TextMeshProUGUI musicVolumeValueText;
    [SerializeField] private TextMeshProUGUI soundEffectsVolumeValueText;

    [SerializeField] private GameObject rebindMenu;
    [SerializeField] private GameObject closeMenuButton;
    
    [SerializeField] private AudioMixer musicMixer;
    [SerializeField] private AudioMixer sfxMixer;
    
    private const string _sensibilityKey = "MouseSensibility";
    private const string _aimSensibilityKey = "AimMouseSensibility";
    
    private const string _musicVolumeKey = "MusicVolume";
    private const string _soundEffectsVolumeKey = "SoundEffectsVolume";
    
    
    private float _sensibility;
    private float _aimSensibility;
    
    private float _musicVolume;
    private float _soundEffectsVolume;
    
    
    private void OnEnable()
    {
        _sensibility = PlayerPrefs.GetFloat(_sensibilityKey, 0.5f);
        _aimSensibility = PlayerPrefs.GetFloat(_aimSensibilityKey, 0.5f);
        
        _musicVolume = PlayerPrefs.GetFloat(_musicVolumeKey, 1f);
        _soundEffectsVolume = PlayerPrefs.GetFloat(_soundEffectsVolumeKey, 1f);
        
        musicMixer.SetFloat("musicVolume", LinearToDecibel(_musicVolume));
        sfxMixer.SetFloat("sfxVolume", LinearToDecibel(_soundEffectsVolume));
        
        sensibilitySlider.value = _sensibility;
        aimSensibilitySlider.value = _aimSensibility;
        
        musicVolumeSlider.value = _musicVolume;
        soundEffectsVolumeSlider.value = _soundEffectsVolume;
        
        
        UpdateSensibilityText(sensibilityValueText, _sensibility);
        UpdateSensibilityText(aimSensibilityValueText, _aimSensibility);
        
        UpdateVolumeText(musicVolumeValueText, _musicVolume);
        UpdateVolumeText(soundEffectsVolumeValueText, _soundEffectsVolume);
        
        SensibilitySlider_OnSensibilityChanged(_sensibility);
        AimSensibilitySlider_OnSensibilityChanged(_aimSensibility);
    }

    public void SensibilitySlider_OnSensibilityChanged(float sensibilityValue)
    {
        PlayerPrefs.SetFloat(_sensibilityKey, sensibilityValue);
        UpdateSensibilityText(sensibilityValueText, sensibilityValue);
        OnSensibilityChanged?.Invoke(sensibilityValue);
    }
    
    public void AimSensibilitySlider_OnSensibilityChanged(float aimSensibilityValue)
    {
        PlayerPrefs.SetFloat(_aimSensibilityKey, aimSensibilityValue);
        UpdateSensibilityText(aimSensibilityValueText, aimSensibilityValue);
        OnAimSensibilityChanged?.Invoke(aimSensibilityValue);
    }

    public void MusicSlider_OnValueChanged(float volumeValue)
    {
        PlayerPrefs.SetFloat(_musicVolumeKey, volumeValue);
        UpdateVolumeText(musicVolumeValueText, volumeValue);
        musicMixer.SetFloat("musicVolume", LinearToDecibel(volumeValue));
    }
    
    public void SoundEffectsSlider_OnValueChanged(float volumeValue)
    {
        PlayerPrefs.SetFloat(_soundEffectsVolumeKey, volumeValue);
        UpdateVolumeText(soundEffectsVolumeValueText, volumeValue);   
        sfxMixer.SetFloat("sfxVolume", LinearToDecibel(volumeValue));
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
}
