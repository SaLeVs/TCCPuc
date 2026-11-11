using System;
using System.Collections.Generic;
using Components.Sound;
using Network;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// The sound check sheet that comes before the main menu the first time on a microphone. One page,
    /// the microphone open the whole time: the player talks, whispers and shouts, watches the bar the
    /// game reads their voice on, and moves the mic level until each lands in its colour. Signing
    /// keeps the level, per microphone (MicSettings), and applies it everywhere (VivoxManager).
    ///
    /// <para>The bar reads the microphone itself every frame — Vivox only reports five times a second,
    /// through its noise suppression — at the level Vivox would send, on the game's AudioEnergy
    /// scale. While the mic test's echo channel is up, Vivox's own reading anchors that scale.</para>
    ///
    /// <para>On signing, the loudest of what the player did at the chosen level is checked: past a
    /// ceiling over the shout threshold it is only volume, nothing the game reads, so the excess is
    /// published as a trim that everyone else takes off this voice where they hear it
    /// (VivoxManager.GetSpeakerTrim). The player's own level stays, and with it where their whisper,
    /// talk and shout land.</para>
    /// </summary>
    public class MicSetupScreen : MonoBehaviour
    {
        private enum Phase
        {
            Adjust,
            Done,
            NoMicrophone
        }

        /// <summary>
        /// For testing in the Editor: set before the scene starts and the sheet reads this instead of
        /// the microphone and Vivox — a SimulatedMicSetupInput runs it without anyone talking.
        /// </summary>
        public static IMicSetupInput InputOverride;

        [SerializeField] private VoiceProfileSO voiceProfile;

        [Tooltip("Muted while the sheet is up, so speakers don't feed the music into the mic.")]
        [SerializeField] private AudioMixer musicMixer;

        [Header("Sheet")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Tooltip("One row of the microphone list, kept inactive: the sheet copies it for each microphone.")]
        [SerializeField] private Button deviceRowTemplate;

        [SerializeField, Min(1)] private int maxDeviceRows = 4;

        [Header("The bar")]
        [Tooltip("Stretched from the meter's left edge to the current level, on the 0..1 energy scale.")]
        [SerializeField] private RectTransform meterFill;

        [SerializeField] private RectTransform whisperZone;
        [SerializeField] private RectTransform speechZone;
        [SerializeField] private RectTransform shoutZone;

        [Tooltip("The loudest moment of the last second or so.")]
        [SerializeField] private RectTransform peakMark;

        [Tooltip("Where the ceiling for other people's ears sits.")]
        [SerializeField] private RectTransform ceilingMark;

        [Header("Mic level")]
        [SerializeField] private TextMeshProUGUI volumeText;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Button volumeDownButton;
        [SerializeField] private Button volumeUpButton;
        [SerializeField] private int sliderMin = -30;
        [SerializeField] private int sliderMax = 30;

        [Tooltip("dB per press of the - and + buttons.")]
        [SerializeField, Min(1)] private int volumeStep = 2;

        [Header("Buttons")]
        [SerializeField] private Button mainButton;
        [SerializeField] private TextMeshProUGUI mainButtonText;
        [SerializeField] private Button hearSelfButton;
        [SerializeField] private TextMeshProUGUI hearSelfText;
        [SerializeField] private Button skipButton;

        [Header("Ink")]
        [SerializeField] private Color inkColor = new(0.196f, 0.196f, 0.196f, 1f);
        [SerializeField] private Color activeColor = new(0.62f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color whisperColor = new(0.30f, 0.40f, 0.55f, 1f);
        [SerializeField] private Color speechColor = new(0.24f, 0.45f, 0.25f, 1f);
        [SerializeField] private Color shoutColor = new(0.62f, 0.12f, 0.12f, 1f);

        [Header("Meter")]
        [Tooltip("Seconds of sound each reading of the microphone averages: about a syllable's attack.")]
        [SerializeField, Range(0.01f, 0.2f)] private float windowSeconds = 0.05f;

        [Tooltip("How fast the bar falls back, in seconds. It rises at once.")]
        [SerializeField, Min(0.01f)] private float releaseSeconds = 0.12f;

        [SerializeField, Min(0f)] private float peakHoldSeconds = 1.2f;

        [Tooltip("A reading this many dB over the room's noise counts as voice.")]
        [SerializeField, Min(1f)] private float voiceMarginDb = 8f;

        [Header("Other people's ears")]
        [Tooltip("The ceiling, in energy over the shout threshold: 0,10 is about 12 dB. A shout is a " +
                 "shout from the threshold up, so past this it is only volume that hurts.")]
        [SerializeField, Range(0f, 0.3f)] private float ceilingOverShout = 0.10f;

        [Tooltip("Which of the loud readings has to stay under the ceiling: the 98th percentile ignores " +
                 "a single knock on the mic.")]
        [SerializeField, Range(0.5f, 1f)] private float loudestPercentile = 0.98f;

        private IMicSetupInput _input;
        private Phase _phase = Phase.Adjust;
        private int _volume;
        private int _trim;
        // Off until asked for: the sheet opens without the player's own voice coming back at them.
        private bool _hearSelf;
        private string _deviceName;
        private bool _released;

        private float _decibelsPerEnergy;
        private float _energyAtZeroDb;

        // Vivox's reading minus the microphone's, in dB: where the bar's scale meets the game's.
        private float _offsetDb;
        private float _loggedOffsetDb;
        private readonly List<float> _offsetReadings = new();

        // Every 50 ms: the last ten seconds of levels, for the room's noise; and every reading of
        // voice since the sheet opened, before the mic level, for the check on signing.
        private const float TICK_SECONDS = 0.05f;
        private float _nextTick;
        private readonly List<float> _recentDb = new();
        private readonly List<float> _voiceDb = new();
        private float _lastClipTime = -100f;
        private float _lastOverCeilingTime = -100f;

        private float _shown;
        private float _peak;
        private float _peakTime;
        private float _previousMusicVolume;
        private bool _hasPreviousMusicVolume;
        private readonly List<Button> _deviceRows = new();

        private float Ceiling => voiceProfile.ShoutEnergy + ceilingOverShout;


        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // The VoiceProfile maps dB to energy on a straight line: one dB moves it by this much.
            _energyAtZeroDb = voiceProfile.EnergyFromHeardLevel(0f);
            float perDecibel = _energyAtZeroDb - voiceProfile.EnergyFromHeardLevel(-1f);
            _decibelsPerEnergy = perDecibel > 0f ? 1f / perDecibel : 118f;

            mainButton.onClick.AddListener(OnMainButton);
            skipButton.onClick.AddListener(Skip);
            hearSelfButton.onClick.AddListener(ToggleHearSelf);
            volumeSlider.wholeNumbers = true;
            volumeSlider.minValue = sliderMin;
            volumeSlider.maxValue = sliderMax;
            volumeSlider.onValueChanged.AddListener(value => SetVolume(Mathf.RoundToInt(value)));
            volumeDownButton.onClick.AddListener(() => volumeSlider.value -= volumeStep);
            volumeUpButton.onClick.AddListener(() => volumeSlider.value += volumeStep);
            deviceRowTemplate.gameObject.SetActive(false);

            PlaceZones();
            MuteMusic();

            // Found rather than through VivoxManager.instance, which logs an error when the scene is
            // played on its own, without the network bootstrap.
            _input = InputOverride ?? new VivoxMicSetupInput(FindFirstObjectByType<VivoxManager>(), windowSeconds);

            if (!_input.IsAvailable)
            {
                EnterNoMicrophone();
                return;
            }

            _input.DevicesChanged += Input_OnDevicesChanged;
            _input.Begin();
            _input.SetHearSelf(_hearSelf);

            // From what this microphone already has, when the sheet is opened again to redo it.
            _deviceName = _input.CurrentDevice;
            SetVolume(_input.SavedVolume);
            BuildDeviceList();
            RefreshHearSelf();
            SetPhase(Phase.Adjust);
        }

        private void Update()
        {
            if (_phase == Phase.NoMicrophone) return;

            _input.Tick();

            if (Time.unscaledTime >= _nextTick)
            {
                _nextTick = Time.unscaledTime + TICK_SECONDS;
                Record(_input.LevelDb);
            }

            UpdateMeter(Mathf.Clamp01(Energy(_input.LevelDb)));

            if (_phase == Phase.Adjust) RefreshHint();
        }

        /// <summary>The game's energy for a microphone level, at the mic level now on. Unclamped.</summary>
        private float Energy(float levelDb) => _energyAtZeroDb + (levelDb + _volume + _offsetDb) / _decibelsPerEnergy;

        private void Record(float levelDb)
        {
            _recentDb.Add(levelDb);
            if (_recentDb.Count > 200) _recentDb.RemoveAt(0);

            if (_input.Peak >= 0.98f) _lastClipTime = Time.unscaledTime;

            if (levelDb < NoiseFloorDb() + voiceMarginDb) return;

            _voiceDb.Add(levelDb);
            if (_voiceDb.Count > 3000) _voiceDb.RemoveAt(0);

            if (Energy(levelDb) > Ceiling) _lastOverCeilingTime = Time.unscaledTime;

            AnchorToVivox();
        }

        private float NoiseFloorDb() => _recentDb.Count < 10 ? -90f : Percentile(_recentDb, 0.1f);

        /// <summary>
        /// Lines the bar up with what the game reads: whenever Vivox reports voice, the gap between its
        /// AudioEnergy and the microphone's level over the same moment, as a median of the last forty.
        /// </summary>
        private void AnchorToVivox()
        {
            float game = _input.GameEnergy;
            if (game < 0.12f || _recentDb.Count < 4) return;

            // Vivox reads about every 200 ms: the same stretch of microphone.
            float sum = 0f;
            for (int i = _recentDb.Count - 4; i < _recentDb.Count; i++) sum += _recentDb[i];
            float microphone = _energyAtZeroDb + (sum / 4f + _volume) / _decibelsPerEnergy;
            if (microphone < 0.12f) return;

            _offsetReadings.Add((game - microphone) * _decibelsPerEnergy);
            if (_offsetReadings.Count > 40) _offsetReadings.RemoveAt(0);
            if (_offsetReadings.Count < 8) return;

            _offsetDb = Mathf.Clamp(Mathf.Lerp(_offsetDb, Percentile(_offsetReadings, 0.5f), 0.25f), -30f, 30f);

            if (Mathf.Abs(_offsetDb - _loggedOffsetDb) >= 3f)
            {
                _loggedOffsetDb = _offsetDb;
                Debug.Log($"[MicSetup] Microphone vs Vivox: {_offsetDb:+0.0;-0.0} dB over {_offsetReadings.Count} readings.");
            }
        }

        private void OnMainButton()
        {
            switch (_phase)
            {
                case Phase.Adjust:
                    Finish();
                    break;
                case Phase.Done:
                case Phase.NoMicrophone:
                    Leave();
                    break;
            }
        }

        /// <summary>
        /// Keeps the level and checks the loudest of what the player did at it: past the ceiling it
        /// would only hurt whoever listens, so that much comes off for them, not for the player.
        /// </summary>
        private void Finish()
        {
            float loudest = float.NaN;
            _trim = 0;

            if (_voiceDb.Count >= 20)
            {
                var energies = new List<float>(_voiceDb.Count);
                foreach (float levelDb in _voiceDb) energies.Add(Energy(levelDb));

                loudest = Percentile(energies, loudestPercentile);
                if (loudest > Ceiling) _trim = MicSettings.ClampListenerTrim(Mathf.FloorToInt((Ceiling - loudest) * _decibelsPerEnergy));
            }

            _input.Save(_volume, _trim);

            Debug.Log($"[MicSetup] Saved {FormatDb(_volume)} for \"{_input.CurrentDevice}\": loudest {loudest:0.00} " +
                      $"over {_voiceDb.Count} readings of voice (ceiling {Ceiling:0.00}) -> listeners {FormatDb(_trim)}; " +
                      $"microphone vs Vivox {_offsetDb:+0.0;-0.0} dB ({_offsetReadings.Count} readings).");

            if (_trim == 0)
            {
                Leave();
                return;
            }

            SetPhase(Phase.Done);
        }

        // ---- Microphones ----

        private void BuildDeviceList()
        {
            foreach (Button row in _deviceRows) Destroy(row.gameObject);
            _deviceRows.Clear();

            List<string> devices = new(_input.Devices);
            string current = _input.CurrentDevice;

            // Past the rows there is room for, the one in use still shows.
            if (devices.Count > maxDeviceRows)
            {
                devices.Remove(current);
                devices = devices.GetRange(0, maxDeviceRows - 1);
                if (current != null) devices.Insert(0, current);
            }

            Transform parent = deviceRowTemplate.transform.parent;
            var template = (RectTransform)deviceRowTemplate.transform;

            for (int i = 0; i < devices.Count; i++)
            {
                string device = devices[i];
                bool inUse = device == current;

                Button row = Instantiate(deviceRowTemplate, parent);
                row.gameObject.SetActive(true);
                row.name = $"DeviceRow_{i}";
                ((RectTransform)row.transform).anchoredPosition = template.anchoredPosition - new Vector2(0f, template.rect.height * i);

                var label = row.GetComponentInChildren<TextMeshProUGUI>();
                label.SetText(inUse ? $"•  {device}  <size=75%>(EM USO)</size>" : $"•  <u>{device}</u>");
                label.color = inUse ? activeColor : inkColor;

                row.interactable = !inUse;
                row.onClick.AddListener(() => SelectDevice(device));
                _deviceRows.Add(row);
            }
        }

        private async void SelectDevice(string device)
        {
            try
            {
                foreach (Button row in _deviceRows) row.interactable = false;
                await _input.SelectDevice(device);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MicSetup] Could not change the microphone: {e.Message}");
            }
            finally
            {
                if (this != null) BuildDeviceList();
            }
        }

        private void Input_OnDevicesChanged()
        {
            if (_input.CurrentDevice == null)
            {
                EnterNoMicrophone();
                return;
            }

            if (_input.CurrentDevice != _deviceName || _phase == Phase.NoMicrophone)
            {
                volumeText.gameObject.SetActive(true);
                volumeSlider.transform.parent.gameObject.SetActive(true);

                // Each microphone keeps its own level, and nothing heard on the last one counts.
                _deviceName = _input.CurrentDevice;
                _recentDb.Clear();
                _voiceDb.Clear();
                _offsetReadings.Clear();
                _offsetDb = 0f;
                SetVolume(_input.SavedVolume);
                SetPhase(Phase.Adjust);
            }

            BuildDeviceList();
        }

        // ---- Level, hearing, phases ----

        private void SetVolume(int volume)
        {
            _volume = MicSettings.Clamp(volume);
            _input?.SetVolume(_volume);

            volumeSlider.SetValueWithoutNotify(_volume);
            volumeText.SetText($"NÍVEL DO MICROFONE: {FormatDb(_volume)}");

            // A new level is a new check.
            if (_phase == Phase.Done) SetPhase(Phase.Adjust);
        }

        private void ToggleHearSelf()
        {
            _hearSelf = !_hearSelf;
            _input?.SetHearSelf(_hearSelf);
            RefreshHearSelf();
        }

        private void RefreshHearSelf()
        {
            hearSelfText.SetText(_hearSelf ? "<u>OUVIR MINHA VOZ: SIM</u>" : "<u>OUVIR MINHA VOZ: NÃO</u>");
        }

        private void SetPhase(Phase phase)
        {
            _phase = phase;

            mainButton.interactable = true;
            mainButtonText.SetText(phase == Phase.Adjust ? "PRONTO" : "CONTINUAR");
            skipButton.gameObject.SetActive(phase != Phase.NoMicrophone);
            hearSelfButton.gameObject.SetActive(phase != Phase.NoMicrophone);

            if (phase == Phase.Done)
            {
                SetStatus($"Seu grito passou do LIMITE. Para não estourar o ouvido de ninguém, quem te ouvir vai te " +
                          $"ouvir <nobr>{-_trim} dB</nobr> mais baixo. Para você e para o jogo nada muda: cada nível " +
                          "continua onde está na barra.", activeColor);
            }
        }

        /// <summary>What the sheet says while the player adjusts, from what it hears right now.</summary>
        private void RefreshHint()
        {
            float now = Time.unscaledTime;

            if (now - _lastClipTime < 1.5f)
                SetStatus("Seu microfone está saturando antes do jogo: baixe o volume de entrada dele no Windows (Configurações > Som).", activeColor);
            else if (now - _lastOverCeilingTime < 1.5f)
                SetStatus("Isso passou do LIMITE: estouraria o ouvido de quem te ouve. Quando você terminar, eu abaixo para eles.", activeColor);
            else
                SetStatus(string.Empty, inkColor);
        }

        private void SetStatus(string text, Color color)
        {
            statusText.SetText(text);
            statusText.color = color;
        }

        private void EnterNoMicrophone()
        {
            SetPhase(Phase.NoMicrophone);
            meterFill.anchorMax = new Vector2(0f, meterFill.anchorMax.y);
            peakMark.gameObject.SetActive(false);

            // Nothing to set a level for: the line, its buttons and its labels go.
            volumeText.gameObject.SetActive(false);
            volumeSlider.transform.parent.gameObject.SetActive(false);
            SetStatus("Não encontrei nenhum microfone. Dá para jogar assim e ajustar depois em Opções > Microfone.", inkColor);
        }

        // ---- The bar ----

        private void UpdateMeter(float energy)
        {
            float now = Time.unscaledTime;

            // Rises at once, falls back a little slower: fast enough to follow syllables, slow enough to read.
            _shown = energy >= _shown ? energy : Mathf.Lerp(_shown, energy, 1f - Mathf.Exp(-Time.unscaledDeltaTime / releaseSeconds));
            meterFill.anchorMax = new Vector2(_shown, meterFill.anchorMax.y);

            if (_shown >= _peak)
            {
                _peak = _shown;
                _peakTime = now;
            }
            else if (now - _peakTime > peakHoldSeconds)
            {
                _peak = Mathf.MoveTowards(_peak, _shown, 0.5f * Time.unscaledDeltaTime);
            }

            PlaceMark(peakMark, _peak);
            peakMark.gameObject.SetActive(_peak > 0.02f);
            foreach (Graphic graphic in peakMark.GetComponentsInChildren<Graphic>()) graphic.color = ColorFor(_peak);
        }

        private Color ColorFor(float energy)
        {
            if (energy >= voiceProfile.ShoutEnergy) return shoutColor;
            if (energy >= voiceProfile.SpeechEnergy) return speechColor;
            return whisperColor;
        }

        private void PlaceZones()
        {
            PlaceZone(whisperZone, 0f, voiceProfile.SpeechEnergy);
            PlaceZone(speechZone, voiceProfile.SpeechEnergy, voiceProfile.ShoutEnergy);
            PlaceZone(shoutZone, voiceProfile.ShoutEnergy, 1f);
            PlaceMark(ceilingMark, Ceiling);
        }

        private static void PlaceZone(RectTransform zone, float from, float to)
        {
            zone.anchorMin = new Vector2(from, zone.anchorMin.y);
            zone.anchorMax = new Vector2(to, zone.anchorMax.y);
            zone.offsetMin = new Vector2(0f, zone.offsetMin.y);
            zone.offsetMax = new Vector2(0f, zone.offsetMax.y);
        }

        private static void PlaceMark(RectTransform mark, float at)
        {
            at = Mathf.Clamp01(at);
            mark.anchorMin = new Vector2(at, mark.anchorMin.y);
            mark.anchorMax = new Vector2(at, mark.anchorMax.y);
            mark.anchoredPosition = new Vector2(0f, mark.anchoredPosition.y);
        }

        // ---- Leaving ----

        private void MuteMusic()
        {
            if (musicMixer == null) return;

            _hasPreviousMusicVolume = musicMixer.GetFloat("musicVolume", out _previousMusicVolume);
            musicMixer.SetFloat("musicVolume", -80f);
        }

        private void Skip()
        {
            // Back to the level saved before (0 if none): skipping signs nothing, and the sheet comes
            // back next time on this microphone.
            _input?.RestoreSaved();
            Leave();
        }

        private void Leave()
        {
            Release();
            Loader.Load(Loader.Scene.MainMenu);
        }

        /// <summary>Gives the mic, the speakers and the music back, however the sheet is left.</summary>
        private void Release()
        {
            if (_released) return;
            _released = true;

            if (_input != null)
            {
                _input.DevicesChanged -= Input_OnDevicesChanged;
                _input.End();
            }

            if (musicMixer != null && _hasPreviousMusicVolume) musicMixer.SetFloat("musicVolume", _previousMusicVolume);
        }

        private void OnDestroy()
        {
            Release();
        }

        private static string FormatDb(int decibels) => decibels > 0 ? $"+{decibels} dB" : $"{decibels} dB";

        private static float Percentile(List<float> values, float fraction)
        {
            if (values.Count == 0) return 0f;

            List<float> sorted = new(values);
            sorted.Sort();

            return sorted[Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Count - 1)), 0, sorted.Count - 1)];
        }
    }
}
