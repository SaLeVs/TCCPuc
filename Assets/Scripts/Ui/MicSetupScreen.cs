using System;
using System.Collections.Generic;
using System.Linq;
using Components.Sound;
using Network;
using Systems;
using TMPro;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// The sound check sheet that comes before the main menu the first time on a microphone: the
    /// player says a sentence normally, whispers one and shouts one, and their mic level is set so the
    /// three land where the game tells them apart — VoiceProfile's speech and shout thresholds. Then
    /// they can talk freely and see what the game understands, nudge the level and sign.
    ///
    /// <para>The level is saved per microphone by MicSettings and applied everywhere by VivoxManager;
    /// the audio device panel's slider adjusts it afterwards. Vivox's gain control stays off, so this
    /// is all that keeps a hot mic from deafening everyone.</para>
    ///
    /// <para>Measured on Vivox's own AudioEnergy — the signal every threshold in the game reads — in
    /// the mic test's echo channel, with Vivox's output muted: hearing each sentence come back half a
    /// second late makes it hard to read the next one. The music is muted too, for speakers.</para>
    /// </summary>
    public class MicSetupScreen : MonoBehaviour
    {
        [Serializable]
        public class StepRow
        {
            public TextMeshProUGUI title;
            public TextMeshProUGUI phrase;
            public TextMeshProUGUI mark;
        }

        private enum Phase
        {
            Connecting,
            Intro,
            Measuring,
            Between,
            Review,
            NoMicrophone
        }

        private const int TALK = 0;
        private const int WHISPER = 1;
        private const int SHOUT = 2;

        // What Vivox lists besides the real microphones; they resolve to one of them.
        private static readonly string[] DefaultDeviceNames = { "Default System Device", "Default Communication Device" };

        private static readonly string[] StepManners = { "em voz normal", "sussurrando", "gritando" };

        [SerializeField] private VoiceProfileSO voiceProfile;

        [Tooltip("Muted while the sheet is up, so speakers don't feed the music into the mic.")]
        [SerializeField] private AudioMixer musicMixer;

        [Header("Sheet")]
        [SerializeField] private TextMeshProUGUI deviceText;
        [SerializeField] private Button changeDeviceButton;

        [Tooltip("Talk, whisper, shout — in the order they are asked for.")]
        [SerializeField] private StepRow[] steps = new StepRow[3];

        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject volumeRow;
        [SerializeField] private TextMeshProUGUI volumeText;
        [SerializeField] private Button volumeDownButton;
        [SerializeField] private Button volumeUpButton;
        [SerializeField] private Button mainButton;
        [SerializeField] private TextMeshProUGUI mainButtonText;
        [SerializeField] private Button redoButton;
        [SerializeField] private Button skipButton;

        [Header("Meter")]
        [Tooltip("Stretched from the meter's left edge to the current level, on the 0..1 energy scale.")]
        [SerializeField] private RectTransform meterFill;
        [SerializeField] private RectTransform whisperZone;
        [SerializeField] private RectTransform speechZone;
        [SerializeField] private RectTransform shoutZone;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Ink")]
        [SerializeField] private Color inkColor = new(0.196f, 0.196f, 0.196f, 1f);
        [SerializeField] private Color activeColor = new(0.62f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color doneColor = new(0.24f, 0.45f, 0.25f, 1f);
        [SerializeField] private Color whisperColor = new(0.30f, 0.40f, 0.55f, 1f);
        [SerializeField] private Color speechColor = new(0.24f, 0.45f, 0.25f, 1f);
        [SerializeField] private Color shoutColor = new(0.62f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color silenceColor = new(0.45f, 0.45f, 0.45f, 1f);

        [Header("Measuring")]
        [Tooltip("Vivox reports AudioEnergy five times a second; sampling faster only repeats it.")]
        [SerializeField, Min(0.05f)] private float sampleInterval = 0.2f;

        [Tooltip("How far above the room's noise floor, in energy, a sample has to be to count as voice.")]
        [SerializeField, Range(0.01f, 0.3f)] private float voiceMargin = 0.06f;

        [Tooltip("Samples of voice each step waits for: talk, whisper, shout. At five a second, 12 is " +
                 "2,4 s of talking; a shout is short.")]
        [SerializeField] private int[] voiceSamplesPerStep = { 12, 10, 5 };

        [Tooltip("A whisper may never rise above the noise: after this many seconds it is taken as it is.")]
        [SerializeField, Min(1f)] private float whisperTimeoutSeconds = 10f;

        [Tooltip("Seconds without any voice before the sheet suggests checking the microphone.")]
        [SerializeField, Min(1f)] private float noVoiceHintSeconds = 8f;

        [Tooltip("How far, in energy, normal talking may land from the middle of the speech band to " +
                 "make room for the whisper and the shout. 0,05 is about 6 dB.")]
        [SerializeField, Range(0f, 0.2f)] private float talkTolerance = 0.05f;

        [Tooltip("An adjustment at least this big, in dB, is checked with another round: a mic that " +
                 "far off may have been clipping, and the first estimate is only that.")]
        [SerializeField, Min(1f)] private float recheckDecibels = 10f;

        [Tooltip("Energy at which a mic counts as clipping.")]
        [SerializeField, Range(0.5f, 1f)] private float clippingEnergy = 0.8f;

        [SerializeField, Min(1)] private int maxRounds = 3;

        [Tooltip("dB per press of the - and + buttons.")]
        [SerializeField, Min(1)] private int volumeStep = 2;

        [Tooltip("Seconds the loudest recent level stays on the label, so a shout can be read.")]
        [SerializeField, Min(0f)] private float labelHoldSeconds = 1f;

        private VivoxManager _vivox;
        private Phase _phase = Phase.Connecting;
        private int _step;
        private int _round;
        private float _phaseTime;
        private float _betweenSeconds;
        private float _nextSampleTime;

        // The last ten seconds of energy, for the room's noise floor: there are pauses between words.
        private readonly List<float> _recent = new();
        private readonly List<float> _stepVoice = new();
        private readonly float[] _levels = new float[3];

        private int _volume;
        private int _startVolume;
        private int _measuredAt;
        private bool _mutedOutput;
        private bool _released;
        private float _previousMusicVolume;
        private bool _hasPreviousMusicVolume;

        private float _latestEnergy;
        private float _shownEnergy;
        private float _labelEnergy;
        private float _labelUntil;


        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            changeDeviceButton.onClick.AddListener(ChangeDevice);
            volumeDownButton.onClick.AddListener(() => NudgeVolume(-volumeStep));
            volumeUpButton.onClick.AddListener(() => NudgeVolume(volumeStep));
            mainButton.onClick.AddListener(OnMainButton);
            redoButton.onClick.AddListener(StartRounds);
            skipButton.onClick.AddListener(Skip);

            PlaceZones();
            MuteMusic();

            // Found rather than through VivoxManager.instance, which logs an error when the scene is
            // played on its own, without the network bootstrap.
            _vivox = FindFirstObjectByType<VivoxManager>();

            if (_vivox == null || VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized
                               || MicSettings.CurrentDeviceName == null)
            {
                EnterNoMicrophone();
                return;
            }

            VivoxService.Instance.EffectiveInputDeviceChanged += VivoxService_OnEffectiveInputDeviceChanged;

            if (!VivoxService.Instance.IsOutputDeviceMuted)
            {
                VivoxService.Instance.MuteOutputDevice();
                _mutedOutput = true;
            }

            _vivox.EnterTestVoiceChannel();

            // From what this microphone already has, when the sheet is opened again to redo it.
            _volume = MicSettings.CurrentInputVolume;
            MicSettings.ApplyVolume(_volume);
            RefreshDevice();
            SetPhase(Phase.Connecting);
        }

        private void Update()
        {
            _phaseTime += Time.unscaledDeltaTime;

            if (_phase == Phase.Connecting && _vivox.IsInMicTest)
            {
                SetPhase(Phase.Intro);
            }

            if (_phase is not (Phase.Connecting or Phase.NoMicrophone) && Time.unscaledTime >= _nextSampleTime)
            {
                _nextSampleTime = Time.unscaledTime + sampleInterval;
                Sample(_vivox.GetSelfAudioEnergy());
            }

            if (_phase == Phase.Between && _phaseTime >= _betweenSeconds)
            {
                BeginStep(_step + 1);
            }

            UpdateMeter();
        }

        private void Sample(float energy)
        {
            _latestEnergy = energy;

            _recent.Add(energy);
            if (_recent.Count > 50) _recent.RemoveAt(0);

            if (_phase != Phase.Measuring) return;

            if (energy >= NoiseFloor() + voiceMargin) _stepVoice.Add(energy);

            int needed = voiceSamplesPerStep[_step];
            steps[_step].mark.SetText(Mathf.Min(99, Mathf.RoundToInt(100f * _stepVoice.Count / needed)) + "%");

            if (_stepVoice.Count >= needed || (_step == WHISPER && _phaseTime >= whisperTimeoutSeconds))
            {
                FinishStep();
            }
            else if (_stepVoice.Count == 0 && _phaseTime >= noVoiceHintSeconds)
            {
                statusText.SetText("Não estou te ouvindo. Confira se o microfone certo está escolhido lá em cima e fale mais perto dele.");
            }
        }

        private float NoiseFloor() => _recent.Count == 0 ? 0f : Percentile(_recent, 0.1f);

        private void OnMainButton()
        {
            switch (_phase)
            {
                case Phase.Intro:
                    StartRounds();
                    break;
                case Phase.Review:
                    MicSettings.SetCurrentInputVolume(_volume);
                    PlayerPrefs.Save();
                    Debug.Log($"[MicSetup] Saved {_volume} dB for \"{MicSettings.CurrentDeviceName}\".");
                    Leave();
                    break;
                case Phase.NoMicrophone:
                    Leave();
                    break;
            }
        }

        private void StartRounds()
        {
            _round = 1;
            _startVolume = _volume;
            _measuredAt = _volume;
            BeginStep(TALK);
        }

        private void BeginStep(int step)
        {
            _step = step;
            _stepVoice.Clear();

            for (int i = 0; i < steps.Length; i++)
            {
                if (i >= step)
                {
                    steps[i].mark.SetText(string.Empty);
                    steps[i].mark.color = activeColor;
                }

                steps[i].title.color = i == step ? activeColor : inkColor;
            }

            statusText.SetText($"Leia a frase {step + 1} {StepManners[step]}.");
            SetPhase(Phase.Measuring);
        }

        private void FinishStep()
        {
            _levels[_step] = _step switch
            {
                // A whisper that never rose above the room is as quiet as the room.
                WHISPER => _stepVoice.Count >= 3 ? Percentile(_stepVoice, 0.75f) : NoiseFloor(),
                SHOUT => Percentile(_stepVoice, 0.6f),
                _ => Percentile(_stepVoice, 0.5f)
            };

            steps[_step].mark.SetText("OK");
            steps[_step].mark.color = doneColor;
            steps[_step].title.color = inkColor;

            if (_step < SHOUT)
            {
                statusText.SetText("Boa! Agora a próxima.");
                WaitThenContinue(1.2f);
                return;
            }

            Fit();
        }

        /// <summary>
        /// One mic level moves the whisper, the talk and the shout together, so all it can do is slide
        /// the player's own range along the scale. It puts normal talking in the middle of the speech
        /// band — the loudness everyone hears alike — and, within a few dB of that, keeps the whisper
        /// under the speech threshold and the shout over the shout threshold, as far from both as it
        /// can. When the player's whisper and shout are too close for both, the whisper wins: staying
        /// unheard by the monster is the mechanic, and a shout in the game is usually louder than one
        /// read off a sheet.
        /// </summary>
        private void Fit()
        {
            float whisper = _levels[WHISPER];
            float talk = _levels[TALK];
            float shout = _levels[SHOUT];
            float speech = voiceProfile.SpeechEnergy;
            float shoutThreshold = voiceProfile.ShoutEnergy;

            float byTalk = (speech + shoutThreshold) * 0.5f - talk;
            float whisperLimit = speech - whisper;
            float shoutLimit = shoutThreshold - shout;

            float shift = shoutLimit < whisperLimit
                ? (whisperLimit + shoutLimit) * 0.5f
                : whisperLimit - 0.01f;

            shift = Mathf.Clamp(shift, byTalk - talkTolerance, byTalk + talkTolerance);

            int volume = Mathf.Clamp(Mathf.RoundToInt(_measuredAt + shift * DecibelsPerEnergy()),
                MicSettings.MIN_INPUT_VOLUME, Mathf.Max(MicSettings.MAX_AUTOMATIC_BOOST, _measuredAt));

            Debug.Log($"[MicSetup] Round {_round} at {_measuredAt} dB on \"{MicSettings.CurrentDeviceName}\": " +
                      $"whisper {whisper:0.000}, talk {talk:0.000}, shout {shout:0.000}, floor {NoiseFloor():0.000} " +
                      $"-> {volume} dB.");

            int change = volume - _measuredAt;
            bool clipping = Mathf.Max(talk, shout) >= clippingEnergy;

            _volume = volume;
            MicSettings.ApplyVolume(volume);

            if ((Mathf.Abs(change) >= recheckDecibels || (clipping && change < 0)) && _round < maxRounds)
            {
                _round++;
                _measuredAt = volume;

                statusText.SetText(change < 0
                    ? $"Seu microfone estava muito alto: baixei {-change} dB. Vamos repetir as frases para conferir."
                    : $"Seu microfone estava baixo: subi {change} dB. Vamos repetir as frases para conferir.");

                _step = TALK - 1;
                WaitThenContinue(3f);
                return;
            }

            EnterReview(whisper + (float)change / DecibelsPerEnergy(), talk + (float)change / DecibelsPerEnergy(),
                shout + (float)change / DecibelsPerEnergy());
        }

        /// <summary>Judges the three levels as they will come out at the level just set.</summary>
        private void EnterReview(float whisper, float talk, float shout)
        {
            float speech = voiceProfile.SpeechEnergy;
            float shoutThreshold = voiceProfile.ShoutEnergy;

            bool whisperOk = whisper < speech;
            bool talkOk = talk >= speech && talk < shoutThreshold;
            bool shoutOk = shout >= shoutThreshold;

            Mark(TALK, talkOk);
            Mark(WHISPER, whisperOk);
            Mark(SHOUT, shoutOk);

            string advice;

            if (!talkOk && talk < speech && _volume >= MicSettings.MAX_AUTOMATIC_BOOST)
                advice = "Seu microfone está baixo mesmo no máximo. Aumente o volume de entrada dele no Windows (Configurações > Som).";
            else if (!talkOk && talk >= shoutThreshold && _volume <= MicSettings.MIN_INPUT_VOLUME)
                advice = "Seu microfone está alto mesmo no mínimo. Desligue o \"Aumento do microfone\" no Windows (Configurações > Som).";
            else if (!shoutOk)
                advice = "Seu grito ficou perto da fala: no jogo, grite com vontade para ser ouvido de longe.";
            else if (!whisperOk)
                advice = "Seu sussurro ficou perto da fala: no jogo, sussurre bem baixinho para o monstro não ouvir.";
            else
                advice = "Tudo certo! Teste à vontade: o quadro mostra o que o jogo entende.";

            int change = _volume - _startVolume;
            string adjusted = change == 0 ? "Volume mantido." : $"Ajustei o microfone em {FormatDb(change)}.";
            statusText.SetText($"{adjusted} {advice}");

            SetPhase(Phase.Review);
        }

        private void Mark(int step, bool ok)
        {
            steps[step].mark.SetText(ok ? "OK" : "?");
            steps[step].mark.color = ok ? doneColor : activeColor;
            steps[step].title.color = inkColor;
        }

        private void WaitThenContinue(float seconds)
        {
            SetPhase(Phase.Between);
            _betweenSeconds = seconds;
        }

        private void NudgeVolume(int delta)
        {
            _volume = Mathf.Clamp(_volume + delta, MicSettings.MIN_INPUT_VOLUME, MicSettings.MAX_INPUT_VOLUME);
            MicSettings.ApplyVolume(_volume);
            RefreshVolume();
        }

        /// <summary>
        /// Steps to the next microphone Vivox lists. Each keeps its own level, so the sheet starts over
        /// on the new one.
        /// </summary>
        private async void ChangeDevice()
        {
            try
            {
                List<VivoxInputDevice> devices = RealDevices();
                if (devices.Count < 2) return;

                int index = devices.FindIndex(device => device.DeviceName == MicSettings.CurrentDeviceName);

                changeDeviceButton.interactable = false;
                await VivoxService.Instance.SetActiveInputDeviceAsync(devices[(index + 1) % devices.Count]);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MicSetup] Could not change the microphone: {e.Message}");
            }
            finally
            {
                if (changeDeviceButton != null)
                {
                    changeDeviceButton.interactable = true;
                    RefreshDevice();
                }
            }
        }

        private static List<VivoxInputDevice> RealDevices()
        {
            return VivoxService.Instance.AvailableInputDevices
                .Where(device => !DefaultDeviceNames.Contains(device.DeviceName))
                .ToList();
        }

        private void VivoxService_OnEffectiveInputDeviceChanged()
        {
            // VivoxManager has just put this microphone's own level on.
            _volume = MicSettings.CurrentInputVolume;
            _recent.Clear();
            RefreshDevice();

            if (_phase is Phase.Measuring or Phase.Between or Phase.Review) SetPhase(Phase.Intro);
        }

        private void RefreshDevice()
        {
            deviceText.SetText($"MICROFONE: <size=80%>{MicSettings.CurrentDeviceName ?? "nenhum"}</size>");
            changeDeviceButton.gameObject.SetActive(RealDevices().Count > 1);
        }

        private void RefreshVolume()
        {
            volumeText.SetText($"AJUSTE DO MICROFONE: {FormatDb(_volume)}");
        }

        private void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;

            bool review = phase == Phase.Review;

            volumeRow.SetActive(review);
            redoButton.gameObject.SetActive(review);
            mainButton.interactable = phase is Phase.Intro or Phase.Review or Phase.NoMicrophone;

            mainButtonText.SetText(phase switch
            {
                Phase.Connecting => "LIGANDO O MICROFONE...",
                Phase.Measuring or Phase.Between => "OUVINDO...",
                Phase.Review => "ASSINAR E CONTINUAR",
                Phase.NoMicrophone => "CONTINUAR",
                _ => "COMEÇAR"
            });

            switch (phase)
            {
                case Phase.Connecting:
                    statusText.SetText("Ligando o microfone...");
                    break;
                case Phase.Intro:
                    foreach (StepRow row in steps)
                    {
                        row.mark.SetText(string.Empty);
                        row.title.color = inkColor;
                    }

                    statusText.SetText("Fale qualquer coisa: a barra tem que se mexer. Depois aperte COMEÇAR e leia as três frases.");
                    break;
                case Phase.Review:
                    RefreshVolume();
                    break;
            }
        }

        private void EnterNoMicrophone()
        {
            deviceText.SetText("MICROFONE: <size=80%>nenhum encontrado</size>");
            changeDeviceButton.gameObject.SetActive(false);
            SetPhase(Phase.NoMicrophone);
            statusText.SetText("Não encontrei nenhum microfone. Dá para jogar assim e ajustar depois em Opções > Microfone.");
        }

        private void UpdateMeter()
        {
            // Vivox updates five times a second: the bar glides between them instead of jumping.
            _shownEnergy = Mathf.Lerp(_shownEnergy, _latestEnergy, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            meterFill.anchorMax = new Vector2(Mathf.Clamp01(_shownEnergy), meterFill.anchorMax.y);

            if (_latestEnergy >= _labelEnergy || Time.unscaledTime >= _labelUntil)
            {
                _labelEnergy = _latestEnergy;
                _labelUntil = Time.unscaledTime + labelHoldSeconds;
            }

            if (_phase is Phase.Connecting or Phase.NoMicrophone)
            {
                levelText.SetText(string.Empty);
            }
            else if (_labelEnergy < NoiseFloor() + voiceMargin)
            {
                SetLevel("SILÊNCIO", silenceColor);
            }
            else if (_labelEnergy >= voiceProfile.ShoutEnergy)
            {
                SetLevel("GRITO", shoutColor);
            }
            else if (_labelEnergy >= voiceProfile.SpeechEnergy)
            {
                SetLevel("FALA", speechColor);
            }
            else
            {
                SetLevel("SUSSURRO", whisperColor);
            }
        }

        private void SetLevel(string label, Color color)
        {
            levelText.SetText(label);
            levelText.color = color;
        }

        private void PlaceZones()
        {
            PlaceZone(whisperZone, 0f, voiceProfile.SpeechEnergy);
            PlaceZone(speechZone, voiceProfile.SpeechEnergy, voiceProfile.ShoutEnergy);
            PlaceZone(shoutZone, voiceProfile.ShoutEnergy, 1f);
        }

        private static void PlaceZone(RectTransform zone, float from, float to)
        {
            zone.anchorMin = new Vector2(from, zone.anchorMin.y);
            zone.anchorMax = new Vector2(to, zone.anchorMax.y);
            zone.offsetMin = new Vector2(0f, zone.offsetMin.y);
            zone.offsetMax = new Vector2(0f, zone.offsetMax.y);
        }

        /// <summary>
        /// How many dB of mic level move AudioEnergy by 1: the VoiceProfile's calibration of the heard
        /// voice puts Vivox's energy on a straight line in dB (118 dB from 0 to 1).
        /// </summary>
        private float DecibelsPerEnergy()
        {
            float perDecibel = voiceProfile.EnergyFromHeardLevel(0f) - voiceProfile.EnergyFromHeardLevel(-1f);
            return perDecibel > 0f ? 1f / perDecibel : 118f;
        }

        private static string FormatDb(int decibels) => decibels > 0 ? $"+{decibels} dB" : $"{decibels} dB";

        private static float Percentile(List<float> values, float fraction)
        {
            if (values.Count == 0) return 0f;

            List<float> sorted = new(values);
            sorted.Sort();

            return sorted[Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Count - 1)), 0, sorted.Count - 1)];
        }

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
            MicSettings.ApplyToCurrentDevice();
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

            if (_vivox != null)
            {
                VivoxService.Instance.EffectiveInputDeviceChanged -= VivoxService_OnEffectiveInputDeviceChanged;

                // Even mid-join: the walk towards the echo channel turns around, and the mic is never
                // left talking to the echo alone.
                _vivox.LeaveTestVoiceChannel();

                try
                {
                    if (_mutedOutput) VivoxService.Instance.UnmuteOutputDevice();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MicSetup] Could not unmute the Vivox output: {e.Message}");
                }
            }

            if (musicMixer != null && _hasPreviousMusicVolume) musicMixer.SetFloat("musicVolume", _previousMusicVolume);
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
