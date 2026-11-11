using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Network;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;

public class AudioDeviceSettings : MonoBehaviour
{
    public Dropdown InputDeviceDropdown;
    public Dropdown OutputDeviceDropdown;

    public Slider InputDeviceVolume;
    public Slider OutputDeviceVolume;

    public Image DeviceEnergyMask;

    public Text EffectiveInputDeviceText;
    public Text EffectiveOutputDeviceText;

    // Setting based on the min and max acceptable values for Vivox but the range can be adjusted.
    const float k_minSliderVolume = -50;
    const float k_maxSliderVolume = 50;
    const float k_voiceMeterSpeed = 3;
    const string k_effectiveDevicePrefix = "Dispositivo em uso:";
    const string k_unknownDevice = "-";

    // Vivox exists as a service only once UnityServices has come up, and it lists no device and
    // hands out no device object until it is initialized. This panel is on screen in the main menu
    // from the moment the scene loads, so it used to ask too early: Start threw before it could
    // attach the dropdown listeners, and OnEnable threw on EffectiveInputDevice - after clearing
    // the input list and before the output list was built, which left the output dropdown on the
    // three placeholder options from the prefab. Nothing retried, so the panel stayed that way for
    // the rest of the session. Both halves are done lazily now, the first frame Vivox is ready.
    private bool _wired;
    private bool _devicesBuilt;

    private void Start()
    {
        TryWire();
    }

    private void TryWire()
    {
        if (_wired) return;

        IVivoxService vivox = VivoxService.Instance;
        if (vivox == null) return;

        vivox.AvailableInputDevicesChanged += RefreshInputDeviceList;
        vivox.AvailableOutputDevicesChanged += RefreshOutputDeviceList;
        vivox.EffectiveInputDeviceChanged += EffectiveInputDeviceChanged;
        vivox.EffectiveOutputDeviceChanged += EffectiveOutputDeviceChanged;

        InputDeviceDropdown.onValueChanged.AddListener((i) =>
        {
            InputDeviceValueChanged(i);
        });
        InputDeviceVolume.onValueChanged.AddListener((val) =>
        {
            OnInputVolumeChanged(val);
        });
        InputDeviceVolume.minValue = k_minSliderVolume;
        InputDeviceVolume.maxValue = k_maxSliderVolume;

        OutputDeviceDropdown.onValueChanged.AddListener((i) =>
        {
            OutputDeviceValueChanged(i);
        });
        OutputDeviceVolume.onValueChanged.AddListener((val) =>
        {
            OnOutputVolumeChanged(val);
        });
        OutputDeviceVolume.minValue = k_minSliderVolume;
        OutputDeviceVolume.maxValue = k_maxSliderVolume;

        _wired = true;
    }

    /// <summary>
    /// Fills both lists, the two levels and the two "device in use" labels. Does nothing until
    /// Vivox is initialized, and Update keeps asking until it is.
    /// </summary>
    private void TryBuildDeviceUi()
    {
        if (_devicesBuilt) return;

        IVivoxService vivox = VivoxService.Instance;
        if (vivox == null || vivox.InitializationState != VivoxInitializationState.Initialized) return;

        RefreshInputDeviceList();
        RefreshOutputDeviceList();

        // Without notifying: only the player moving the slider saves a level for their mic, and
        // the output level was being written straight back to Vivox every time the panel opened.
        InputDeviceVolume.SetValueWithoutNotify(vivox.InputDeviceVolume);
        OutputDeviceVolume.SetValueWithoutNotify(vivox.OutputDeviceVolume);

        EffectiveInputDeviceText.text = $"{k_effectiveDevicePrefix} {DeviceLabel(vivox.EffectiveInputDevice)}";
        EffectiveOutputDeviceText.text = $"{k_effectiveDevicePrefix} {DeviceLabel(vivox.EffectiveOutputDevice)}";

        _devicesBuilt = true;
    }

    /// <summary>
    /// A device's name, for a device Vivox may not have yet. The wrapper object can come back
    /// non-null around an empty device - that is what the effective device is until the first
    /// successful refresh - and reading its name there throws.
    /// </summary>
    private static string DeviceLabel(VivoxInputDevice device) =>
        device == null ? k_unknownDevice : SafeName(() => device.DeviceName);

    private static string DeviceLabel(VivoxOutputDevice device) =>
        device == null ? k_unknownDevice : SafeName(() => device.DeviceName);

    private static string SafeName(Func<string> read)
    {
        try
        {
            string name = read();
            return string.IsNullOrEmpty(name) ? k_unknownDevice : name;
        }
        catch (NullReferenceException)
        {
            return k_unknownDevice;
        }
    }

    void OnEnable()
    {
        DeviceEnergyMask.fillAmount = 0;

        // Re-read on every open: the player may have plugged a headset in since the last time.
        _devicesBuilt = false;

        TryWire();
        TryBuildDeviceUi();
    }

    void OnDestroy()
    {
        // Unbind all UI actions
        InputDeviceDropdown.onValueChanged.RemoveAllListeners();
        InputDeviceVolume.onValueChanged.RemoveAllListeners();
        OutputDeviceDropdown.onValueChanged.RemoveAllListeners();
        OutputDeviceVolume.onValueChanged.RemoveAllListeners();

        // The pause menu's copy is destroyed with the player: Vivox must not call into it afterwards.
        IVivoxService service = VivoxService.Instance;
        if (service == null) return;

        service.AvailableInputDevicesChanged -= RefreshInputDeviceList;
        service.AvailableOutputDevicesChanged -= RefreshOutputDeviceList;
        service.EffectiveInputDeviceChanged -= EffectiveInputDeviceChanged;
        service.EffectiveOutputDeviceChanged -= EffectiveOutputDeviceChanged;
    }

    private void Update()
    {
        // Vivox comes up while this panel is already on screen, so both are kept waiting for it.
        TryWire();
        TryBuildDeviceUi();

        // Vivox has no channel list until it has logged in, and in the main menu it has not yet:
        // reading ActiveChannels there threw a NullReferenceException every frame.
        IVivoxService vivox = VivoxService.Instance;
        if (vivox == null || !vivox.IsLoggedIn || vivox.ActiveChannels == null) return;

        // The mic test now sits next to the match's channel and the mic only reaches the echo, so
        // the first channel may read silence: the loudest of this machine's own entries is the mic.
        float energy = 0f;
        bool found = false;

        foreach (KeyValuePair<string, ReadOnlyCollection<VivoxParticipant>> channel in vivox.ActiveChannels)
        {
            VivoxParticipant localParticipant = channel.Value.FirstOrDefault(p => p.IsSelf);
            if (localParticipant == null) continue;

            energy = Mathf.Max(energy, (float)localParticipant.AudioEnergy);
            found = true;
        }

        if (found)
        {
            DeviceEnergyMask.fillAmount = Mathf.Lerp(DeviceEnergyMask.fillAmount, energy, Time.deltaTime * k_voiceMeterSpeed);
        }
    }

    private void RefreshInputDeviceList()
    {
        IVivoxService vivox = VivoxService.Instance;
        if (vivox == null || vivox.InitializationState != VivoxInitializationState.Initialized) return;

        InputDeviceDropdown.Hide();
        InputDeviceDropdown.ClearOptions();

        InputDeviceDropdown.options.AddRange(vivox.AvailableInputDevices.Select(v => new Dropdown.OptionData() { text = v.DeviceName }));
        InputDeviceDropdown.SetValueWithoutNotify(InputDeviceDropdown.options.FindIndex(option => option.text == DeviceLabel(vivox.ActiveInputDevice)));
        InputDeviceDropdown.RefreshShownValue();
    }

    private void RefreshOutputDeviceList()
    {
        IVivoxService vivox = VivoxService.Instance;
        if (vivox == null || vivox.InitializationState != VivoxInitializationState.Initialized) return;

        OutputDeviceDropdown.Hide();
        OutputDeviceDropdown.ClearOptions();

        OutputDeviceDropdown.options.AddRange(vivox.AvailableOutputDevices.Select(v => new Dropdown.OptionData() { text = v.DeviceName }));
        OutputDeviceDropdown.SetValueWithoutNotify(OutputDeviceDropdown.options.FindIndex(option => option.text == DeviceLabel(vivox.ActiveOutputDevice)));
        OutputDeviceDropdown.RefreshShownValue();
    }

    // Both lists are built in the order Vivox hands the devices over, so the row the player picked
    // is that device. Looking the name up instead picked the wrong one whenever two devices share
    // a display name - this machine reports its monitor's audio output twice, under one name.
    void InputDeviceValueChanged(int index)
    {
        ReadOnlyCollection<VivoxInputDevice> devices = VivoxService.Instance.AvailableInputDevices;
        if (index < 0 || index >= devices.Count) return;

        VivoxService.Instance.SetActiveInputDeviceAsync(devices[index]);
    }

    void EffectiveInputDeviceChanged()
    {
        EffectiveInputDeviceText.text = $"{k_effectiveDevicePrefix} {DeviceLabel(VivoxService.Instance.EffectiveInputDevice)}";

        // Each mic keeps its own level, which VivoxManager puts back when it becomes the one in use.
        InputDeviceVolume.SetValueWithoutNotify(MicSettings.CurrentInputVolume);
    }

    void OutputDeviceValueChanged(int index)
    {
        ReadOnlyCollection<VivoxOutputDevice> devices = VivoxService.Instance.AvailableOutputDevices;
        if (index < 0 || index >= devices.Count) return;

        VivoxService.Instance.SetActiveOutputDeviceAsync(devices[index]);
    }

    void EffectiveOutputDeviceChanged()
    {
        EffectiveOutputDeviceText.text = $"{k_effectiveDevicePrefix} {DeviceLabel(VivoxService.Instance.EffectiveOutputDevice)}";
    }

    private void OnInputVolumeChanged(float val)
    {
        // Saved for the mic in use: the next session, the lobbies and the match all start from it.
        MicSettings.SetCurrentInputVolume((int)val);
    }

    private void OnOutputVolumeChanged(float val)
    {
        VivoxService.Instance.SetOutputDeviceVolume((int)val);
    }
    
}
