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
    /// The sound check sheet that comes before the main menu the first time on a microphone, in six
    /// steps: choose the mic and hear yourself; meet the one bar the game reads a voice on (whisper,
    /// talk, shout); whisper, talk and shout in turn, five seconds an attempt, the mic level moving
    /// between attempts until the game recognises each; then a suggested level, which the player can
    /// fine-tune hearing themselves, with the suggestion marked on the line.
    ///
    /// <para>The level is saved per microphone by MicSettings and applied everywhere by VivoxManager;
    /// the audio device panel's slider adjusts it afterwards. Vivox's gain control stays off, so this is
    /// all that keeps a hot mic from deafening everyone. MicCalibration has the arithmetic.</para>
    ///
    /// <para>Measured on Vivox's own AudioEnergy, the signal every threshold in the game reads, in the
    /// mic test's echo channel. The echo comes back half a second late, which makes it hard to say the
    /// next sentence, so the player only hears it while choosing the mic and while fine-tuning. The
    /// music is muted throughout, for speakers.</para>
    /// </summary>
    public class MicSetupScreen : MonoBehaviour
    {
        private enum Phase
        {
            Connecting,
            ChooseMic,
            Scale,
            Prepare,
            Listening,
            Feedback,
            Result,
            NoMicrophone
        }

        /// <summary>
        /// For testing in the Editor: set before the scene starts and the sheet reads this instead of
        /// Vivox — a SimulatedMicSetupInput runs the whole flow without anyone talking.
        /// </summary>
        public static IMicSetupInput InputOverride;

        private static readonly string[] StepNames = { "MICROFONE", "ESCALA", "SUSSURRO", "FALA", "GRITO", "RESULTADO" };

        private static readonly string[] Headings =
        {
            "1. ESCOLHA E TESTE O MICROFONE",
            "2. A ESCALA DA SUA VOZ",
            "3. SUSSURRE",
            "4. FALE NORMAL",
            "5. GRITE",
            "6. RESULTADO"
        };

        private static readonly string[] Instructions =
        {
            "Escolha o microfone que você vai usar e fale qualquer coisa. Você vai se ouvir de volta, com um pequeno atraso (melhor com fone de ouvido).",
            "O jogo entende sua voz em três níveis, sempre nesta barra. Fale e veja até onde ela vai. Depois eu peço cada nível, 5 segundos por tentativa.",
            "Sussurre a frase bem baixinho, como se o monstro estivesse na sala.",
            "Leia a frase em voz normal, como numa conversa.",
            "Grite a frase com vontade, como se precisasse de ajuda do outro lado do teatro.",
            "Este é o nível que sugiro. Agora você se ouve de novo: fale, sussurre e grite para conferir. Se quiser, ajuste na linha."
        };

        private static readonly string[] Phrases =
        {
            "“Tem alguém atrás da cortina?”",
            "“Boa noite, Teatro do Grande Vazio!”",
            "“SOCORRO! ME TIRA DAQUI!”"
        };

        private static readonly string[] TierNames = { "sussurro", "fala", "grito" };
        private static readonly string[] TierVerbs = { "sussurre", "fale", "grite" };
        private static readonly string[] MarkLabels = { "seu sussurro", "sua fala", "seu grito" };

        [SerializeField] private VoiceProfileSO voiceProfile;

        [Tooltip("Muted while the sheet is up, so speakers don't feed the music into the mic.")]
        [SerializeField] private AudioMixer musicMixer;

        [Header("Sheet")]
        [SerializeField] private TextMeshProUGUI stepsText;
        [SerializeField] private TextMeshProUGUI headingText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private TextMeshProUGUI phraseText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("1. Microphone")]
        [SerializeField] private GameObject micPanel;

        [Tooltip("One row of the microphone list, kept inactive: the sheet copies it for each microphone.")]
        [SerializeField] private Button deviceRowTemplate;

        [SerializeField, Min(1)] private int maxDeviceRows = 5;
        [SerializeField] private VoiceTraceGraphic trace;

        [Header("2-6. The scale")]
        [SerializeField] private GameObject scalePanel;

        [Tooltip("Stretched from the meter's left edge to the current level, on the 0..1 energy scale.")]
        [SerializeField] private RectTransform meterFill;

        [SerializeField] private RectTransform whisperZone;
        [SerializeField] private RectTransform speechZone;
        [SerializeField] private RectTransform shoutZone;
        [SerializeField] private TextMeshProUGUI levelText;

        [Tooltip("What each level means in the game, under the bar while it is introduced.")]
        [SerializeField] private GameObject zoneDescriptions;

        [Tooltip("Where the player's whisper, talk and shout land at the current level: a tick with a label child.")]
        [SerializeField] private RectTransform[] tierMarks = new RectTransform[3];

        [SerializeField] private GameObject countdown;
        [SerializeField] private RectTransform countdownFill;
        [SerializeField] private TextMeshProUGUI attemptText;

        [Header("Mic level")]
        [SerializeField] private TextMeshProUGUI volumeText;
        [SerializeField] private GameObject fineTuneRow;
        [SerializeField] private Slider fineTuneSlider;
        [SerializeField] private RectTransform suggestedMark;
        [SerializeField] private Button suggestedButton;
        [SerializeField] private Button volumeDownButton;
        [SerializeField] private Button volumeUpButton;

        [Header("Buttons")]
        [SerializeField] private Button mainButton;
        [SerializeField] private TextMeshProUGUI mainButtonText;
        [SerializeField] private Button redoButton;
        [SerializeField] private Button skipButton;

        [Header("Ink")]
        [SerializeField] private Color inkColor = new(0.196f, 0.196f, 0.196f, 1f);
        [SerializeField] private Color mutedColor = new(0.196f, 0.196f, 0.196f, 0.4f);
        [SerializeField] private Color activeColor = new(0.62f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color doneColor = new(0.24f, 0.45f, 0.25f, 1f);
        [SerializeField] private Color whisperColor = new(0.30f, 0.40f, 0.55f, 1f);
        [SerializeField] private Color speechColor = new(0.24f, 0.45f, 0.25f, 1f);
        [SerializeField] private Color shoutColor = new(0.62f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color silenceColor = new(0.45f, 0.45f, 0.45f, 1f);

        [Tooltip("How much of their colour the zones not being asked for keep.")]
        [SerializeField, Range(0f, 1f)] private float otherZonesAlpha = 0.35f;

        [Header("Timing")]
        [Tooltip("Vivox reports AudioEnergy five times a second; sampling faster only repeats it.")]
        [SerializeField, Min(0.05f)] private float sampleInterval = 0.2f;

        [SerializeField, Min(1f)] private float attemptSeconds = 5f;

        [Tooltip("Before the first attempt at each level, to read the sentence.")]
        [SerializeField, Min(0f)] private float prepareSeconds = 2.5f;

        [Tooltip("After an attempt that did not work, to read why and what changed.")]
        [SerializeField, Min(0f)] private float feedbackSeconds = 2.5f;

        [SerializeField, Min(0f)] private float confirmedSeconds = 1.5f;
        [SerializeField, Min(1)] private int maxAttempts = 4;

        [Tooltip("Seconds of voice the first step waits for before the mic counts as working.")]
        [SerializeField, Min(0f)] private float voiceToContinueSeconds = 1f;

        [Tooltip("Without any voice for this long, the first step lets the player go on anyway.")]
        [SerializeField, Min(1f)] private float noVoiceSeconds = 12f;

        [SerializeField, Min(1f)] private float connectTimeoutSeconds = 15f;

        [Header("Fine-tuning")]
        [Tooltip("How far, in dB, the player can move the suggested level here. The audio device panel " +
                 "in Options still goes anywhere.")]
        [SerializeField, Min(1)] private int fineTuneRange = 10;

        [Tooltip("dB per press of the - and + buttons.")]
        [SerializeField, Min(1)] private int volumeStep = 2;

        [Tooltip("Seconds the loudest recent level stays on the label, so a shout can be read.")]
        [SerializeField, Min(0f)] private float labelHoldSeconds = 1f;

        private IMicSetupInput _input;
        private MicCalibration _calibration;
        private Phase _phase = Phase.Connecting;
        private float _phaseTime;
        private float _phaseSeconds;
        private Action _afterFeedback;
        private float _nextSampleTime;

        private MicTier _tier;
        private int _attempt;
        private int _volume;
        private int _suggested;
        private string _deviceName;
        private int _voiceSamples;
        private bool _released;

        // The last ten seconds of energy, for the room's noise floor: there are pauses between words.
        private readonly List<float> _recent = new();
        private readonly List<float> _attemptVoice = new();
        private readonly List<Button> _deviceRows = new();
        private readonly Color[] _zoneColors = new Color[3];

        private float _latestEnergy;
        private float _shownEnergy;
        private float _labelEnergy;
        private float _labelUntil;
        private float _previousMusicVolume;
        private bool _hasPreviousMusicVolume;

        private RectTransform[] Zones => new[] { whisperZone, speechZone, shoutZone };


        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            mainButton.onClick.AddListener(OnMainButton);
            redoButton.onClick.AddListener(EnterChooseMic);
            skipButton.onClick.AddListener(Skip);
            fineTuneSlider.onValueChanged.AddListener(value => SetVolume(Mathf.RoundToInt(value)));
            volumeDownButton.onClick.AddListener(() => fineTuneSlider.value -= volumeStep);
            volumeUpButton.onClick.AddListener(() => fineTuneSlider.value += volumeStep);
            suggestedButton.onClick.AddListener(() => fineTuneSlider.value = _suggested);
            deviceRowTemplate.gameObject.SetActive(false);

            _calibration = new MicCalibration(voiceProfile);
            PlaceZones();
            MuteMusic();

            // Found rather than through VivoxManager.instance, which logs an error when the scene is
            // played on its own, without the network bootstrap.
            _input = InputOverride ?? new VivoxMicSetupInput(FindFirstObjectByType<VivoxManager>());

            if (!_input.IsAvailable)
            {
                EnterNoMicrophone();
                return;
            }

            _input.DevicesChanged += Input_OnDevicesChanged;
            _input.Begin();

            // From what this microphone already has, when the sheet is opened again to redo it.
            _deviceName = _input.CurrentDevice;
            SetVolume(_input.SavedVolume);
            BuildDeviceList();
            SetPhase(Phase.Connecting);
        }

        private void Update()
        {
            _phaseTime += Time.unscaledDeltaTime;

            if (_phase == Phase.Connecting)
            {
                if (_input.IsReady) EnterChooseMic();
                else if (_phaseTime >= connectTimeoutSeconds && _phaseTime - Time.unscaledDeltaTime < connectTimeoutSeconds)
                    statusText.SetText("O teste de microfone não ligou. Dá para pular e ajustar depois em Opções > Microfone.");
            }

            if (_phase is not (Phase.Connecting or Phase.NoMicrophone) && Time.unscaledTime >= _nextSampleTime)
            {
                _nextSampleTime = Time.unscaledTime + sampleInterval;
                Sample(_input.Energy);
            }

            switch (_phase)
            {
                case Phase.ChooseMic:
                    RefreshChooseMicButton();
                    break;
                case Phase.Prepare when _phaseTime >= _phaseSeconds:
                    StartListening();
                    break;
                case Phase.Listening:
                    RefreshCountdown();
                    if (_phaseTime >= attemptSeconds) FinishAttempt();
                    break;
                case Phase.Feedback when _phaseTime >= _phaseSeconds:
                    _afterFeedback?.Invoke();
                    break;
            }

            UpdateMeter();
        }

        private void Sample(float energy)
        {
            _latestEnergy = energy;

            _recent.Add(energy);
            if (_recent.Count > 50) _recent.RemoveAt(0);

            if (energy < NoiseFloor() + MicCalibration.VOICE_MARGIN) return;

            if (_phase == Phase.ChooseMic) _voiceSamples++;

            if (_phase != Phase.Listening) return;

            _attemptVoice.Add(energy);
            if (_calibration.IsConfirmed(_tier, _attemptVoice)) FinishAttempt();
        }

        private float NoiseFloor() => _recent.Count == 0 ? 0f : MicCalibration.Percentile(_recent, 0.1f);

        private void OnMainButton()
        {
            switch (_phase)
            {
                case Phase.ChooseMic:
                    SetPhase(Phase.Scale);
                    break;
                case Phase.Scale:
                    _calibration.Clear();
                    StartTier(MicTier.Whisper);
                    break;
                case Phase.Result:
                    _input.Save(_volume);
                    Debug.Log($"[MicSetup] Saved {MicCalibration.FormatDb(_volume)} for \"{_input.CurrentDevice}\" " +
                              $"(suggested {MicCalibration.FormatDb(_suggested)}).");
                    Leave();
                    break;
                case Phase.NoMicrophone:
                    Leave();
                    break;
            }
        }

        // ---- 1. Microphone ----

        private void EnterChooseMic()
        {
            _voiceSamples = 0;
            _calibration.Clear();
            trace.Clear();
            BuildDeviceList();
            SetPhase(Phase.ChooseMic);
        }

        private void RefreshChooseMicButton()
        {
            bool heard = _voiceSamples * sampleInterval >= voiceToContinueSeconds;
            bool waitedLong = _phaseTime >= noVoiceSeconds;

            mainButton.interactable = heard || waitedLong;
            mainButtonText.SetText(heard ? "ESTÁ FUNCIONANDO" : waitedLong ? "CONTINUAR MESMO ASSIM" : "FALE PARA TESTAR");

            if (heard)
                SetStatus("Estou te ouvindo! Se for este o microfone, é só seguir.", doneColor);
            else if (waitedLong)
                SetStatus("Ainda não te ouvi. Escolha outro microfone na lista ou fale mais perto dele.", activeColor);
            else
                SetStatus("Fale alguma coisa: o traço acima tem que se mexer.", inkColor);
        }

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
            float spacing = ((RectTransform)deviceRowTemplate.transform).rect.height;

            for (int i = 0; i < devices.Count; i++)
            {
                string device = devices[i];
                bool inUse = device == current;

                Button row = Instantiate(deviceRowTemplate, parent);
                row.gameObject.SetActive(true);
                row.name = $"DeviceRow_{i}";

                var rect = (RectTransform)row.transform;
                rect.anchoredPosition = ((RectTransform)deviceRowTemplate.transform).anchoredPosition - new Vector2(0f, spacing * i);

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

            if (_input.CurrentDevice != _deviceName)
            {
                // Each microphone keeps its own level, so the sheet starts over on the new one.
                _deviceName = _input.CurrentDevice;
                _recent.Clear();
                SetVolume(_input.SavedVolume);

                if (_phase is not (Phase.Connecting or Phase.NoMicrophone))
                {
                    EnterChooseMic();
                    return;
                }
            }

            BuildDeviceList();
        }

        // ---- 3-5. Whisper, talk, shout ----

        private void StartTier(MicTier tier)
        {
            _tier = tier;
            _attempt = 0;
            BeginAttempt();

            _phaseSeconds = prepareSeconds;
            SetPhase(Phase.Prepare);
            SetStatus("Prepare-se...", inkColor);
        }

        private void BeginAttempt()
        {
            _attempt++;
            _attemptVoice.Clear();
        }

        private void StartListening()
        {
            SetPhase(Phase.Listening);
            SetStatus($"Agora: {TierVerbs[(int)_tier]} a frase!", activeColor);
        }

        private void FinishAttempt()
        {
            MicCalibration.Verdict verdict = _calibration.Judge(_tier, _volume, _attemptVoice, NoiseFloor());
            RefreshMarks();

            string tierName = TierNames[(int)_tier];
            bool last = _attempt >= maxAttempts;

            if (verdict.Outcome == MicCalibration.Outcome.Confirmed)
            {
                SetStatus(_tier switch
                {
                    MicTier.Whisper => "Isso! Sussurro confirmado.",
                    MicTier.Talk => "Isso! Fala confirmada.",
                    _ => "Isso! Grito confirmado."
                }, doneColor);

                ShowFeedback(confirmedSeconds, NextTier);
                return;
            }

            // A whisper nobody hears even at the highest level is as quiet as it gets: nothing to fix.
            if (_tier == MicTier.Whisper && verdict.Outcome == MicCalibration.Outcome.NotHeard && verdict.Blocked)
            {
                SetStatus("Nem com o microfone no máximo seu sussurro aparece: no jogo só quem estiver colado vai ouvir. Vamos para a fala.", inkColor);
                ShowFeedback(feedbackSeconds + 1f, NextTier);
                return;
            }

            if (last)
            {
                SetStatus($"Não consegui confirmar o {tierName}, mas já tenho o bastante. Vamos em frente.", inkColor);
                ShowFeedback(feedbackSeconds, NextTier);
                return;
            }

            string again = $"{TierVerbs[(int)_tier]} de novo";

            if (verdict.Blocked)
            {
                SetStatus(BlockedMessage(verdict.Outcome, again), activeColor);
            }
            else
            {
                int change = verdict.NextVolume - _volume;
                SetVolume(verdict.NextVolume);

                string came = verdict.Outcome switch
                {
                    MicCalibration.Outcome.NotHeard => "Não deu para te ouvir.",
                    MicCalibration.Outcome.TooLoud when _tier == MicTier.Whisper => "Isso chegou como FALA.",
                    MicCalibration.Outcome.TooLoud => "Sua fala chegou como GRITO.",
                    _ when _tier == MicTier.Talk => "Sua fala chegou como SUSSURRO.",
                    _ => "Seu grito chegou como FALA."
                };

                string moved = change > 0 ? "Aumentei" : "Baixei";
                string softly = _tier == MicTier.Whisper && change < 0 ? ", bem baixinho" : string.Empty;
                SetStatus($"{came} {moved} o microfone para <nobr>{MicCalibration.FormatDb(_volume)}</nobr>: {again}{softly}.", activeColor);
            }

            BeginAttempt();
            ShowFeedback(feedbackSeconds, StartListening);
        }

        private string BlockedMessage(MicCalibration.Outcome outcome, string again)
        {
            return outcome switch
            {
                _ when _tier == MicTier.Shout =>
                    "Seu grito chegou como FALA, e subir mais o microfone faria seu sussurro virar fala. Grite mais forte!",
                MicCalibration.Outcome.TooLoud =>
                    $"Mesmo com o microfone no mínimo ficou alto. Afaste um pouco o microfone e {again}.",
                _ => $"Mesmo com o microfone no máximo ficou baixo. Chegue mais perto do microfone e {again}."
            };
        }

        private void NextTier()
        {
            if (_tier < MicTier.Shout) StartTier(_tier + 1);
            else EnterResult();
        }

        private void ShowFeedback(float seconds, Action next)
        {
            _phaseSeconds = seconds;
            _afterFeedback = next;
            SetPhase(Phase.Feedback);
        }

        private void RefreshCountdown()
        {
            float remaining = Mathf.Max(0f, attemptSeconds - _phaseTime);

            countdownFill.anchorMax = new Vector2(remaining / attemptSeconds, countdownFill.anchorMax.y);
            attemptText.SetText($"TENTATIVA {_attempt}  ·  {Mathf.CeilToInt(remaining)} s");
        }

        // ---- 6. Result ----

        private void EnterResult()
        {
            _suggested = _calibration.Suggest(_volume);

            fineTuneSlider.wholeNumbers = true;
            fineTuneSlider.SetValueWithoutNotify(_suggested);
            fineTuneSlider.minValue = Mathf.Max(MicSettings.MIN_INPUT_VOLUME, _suggested - fineTuneRange);
            fineTuneSlider.maxValue = Mathf.Min(MicSettings.MAX_INPUT_VOLUME, _suggested + fineTuneRange);
            fineTuneSlider.SetValueWithoutNotify(_suggested);

            float at = Mathf.InverseLerp(fineTuneSlider.minValue, fineTuneSlider.maxValue, _suggested);
            suggestedMark.anchorMin = new Vector2(at, suggestedMark.anchorMin.y);
            suggestedMark.anchorMax = new Vector2(at, suggestedMark.anchorMax.y);
            suggestedMark.anchoredPosition = new Vector2(0f, suggestedMark.anchoredPosition.y);

            SetVolume(_suggested);
            SetPhase(Phase.Result);
        }

        private void RefreshResult()
        {
            if (_phase != Phase.Result) return;

            phraseText.SetText($"Sugestão para o seu microfone: <color=#{ColorUtility.ToHtmlStringRGB(activeColor)}>" +
                               $"{MicCalibration.FormatDb(_suggested)}</color>");
            SetStatus($"{Advice()} Você está se ouvindo de novo: o que vale para os outros é a barra, não o volume do seu fone.", inkColor);
        }

        /// <summary>Judges the three where they land at the level now on the line.</summary>
        private string Advice()
        {
            float speech = _calibration.SpeechEnergy;
            float shout = _calibration.ShoutEnergy;

            if (!_calibration.TryPredict(MicTier.Talk, _volume, out float talk))
                return "Não consegui te ouvir falando: confira o microfone ou refaça o teste.";

            bool hasWhisper = _calibration.TryPredict(MicTier.Whisper, _volume, out float whisper);
            bool hasShout = _calibration.TryPredict(MicTier.Shout, _volume, out float shoutLevel);

            if (talk < speech && _volume >= MicSettings.MAX_AUTOMATIC_BOOST)
                return "Seu microfone está baixo mesmo no máximo. Aumente o volume de entrada dele no Windows (Configurações > Som).";
            if (talk >= shout && _volume <= MicSettings.MIN_INPUT_VOLUME)
                return "Seu microfone está alto mesmo no mínimo. Desligue o \"Aumento do microfone\" no Windows (Configurações > Som).";
            if (talk < speech) return "Assim sua fala chega como sussurro: suba o nível.";
            if (talk >= shout) return "Assim sua fala chega como grito: baixe o nível.";
            if (hasWhisper && whisper >= speech) return "Seu sussurro chega como fala: no jogo, sussurre bem baixinho para o monstro não ouvir.";
            if (hasShout && shoutLevel < shout) return "Seu grito ficou perto da fala: no jogo, grite com vontade para ser ouvido de longe.";
            if (!hasWhisper) return "Seu sussurro quase não aparece: no jogo, só quem estiver bem perto vai ouvir.";

            return "Tudo certo: sussurro, fala e grito caem cada um no seu lugar.";
        }

        // ---- Shared ----

        private void SetVolume(int volume)
        {
            volume = MicSettings.Clamp(volume);
            int change = volume - _volume;

            // The noise floor was read at the old level: it moves with the mic, like everything else.
            if (change != 0 && _calibration != null)
            {
                for (int i = 0; i < _recent.Count; i++)
                {
                    if (_recent[i] > 0f) _recent[i] = Mathf.Clamp01(_recent[i] + change / _calibration.DecibelsPerEnergy);
                }
            }

            _volume = volume;
            _input?.SetVolume(volume);

            volumeText.SetText($"NÍVEL DO MICROFONE: {MicCalibration.FormatDb(_volume)}");
            RefreshMarks();
            RefreshResult();
        }

        private void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;

            int step = phase switch
            {
                Phase.Scale => 1,
                Phase.Prepare or Phase.Listening or Phase.Feedback => 2 + (int)_tier,
                Phase.Result => 5,
                _ => 0
            };

            bool tierStep = phase is Phase.Prepare or Phase.Listening or Phase.Feedback;

            micPanel.SetActive(phase is Phase.Connecting or Phase.ChooseMic);
            scalePanel.SetActive(phase is Phase.Scale or Phase.Result || tierStep);
            zoneDescriptions.SetActive(phase == Phase.Scale);
            countdown.SetActive(phase is Phase.Prepare or Phase.Listening);
            fineTuneRow.SetActive(phase == Phase.Result);
            volumeText.gameObject.SetActive(phase != Phase.NoMicrophone);
            redoButton.gameObject.SetActive(phase == Phase.Result);
            skipButton.gameObject.SetActive(phase != Phase.NoMicrophone);
            phraseText.gameObject.SetActive(tierStep || phase == Phase.Result);

            _input?.SetHearSelf(phase is Phase.ChooseMic or Phase.Result);

            stepsText.SetText(StepsLine(step));
            headingText.SetText(Headings[step]);
            instructionText.SetText(phase == Phase.NoMicrophone ? string.Empty : Instructions[step]);
            if (tierStep) phraseText.SetText(Phrases[(int)_tier]);

            for (int i = 0; i < 3; i++) TintZone(i, !tierStep || i == (int)_tier ? 1f : otherZonesAlpha);

            mainButton.interactable = phase is Phase.Scale or Phase.Result or Phase.NoMicrophone;
            mainButtonText.SetText(phase switch
            {
                Phase.Connecting => "LIGANDO O MICROFONE...",
                Phase.ChooseMic => "FALE PARA TESTAR",
                Phase.Scale => "COMEÇAR O TESTE",
                Phase.Result => "ASSINAR E CONTINUAR",
                Phase.NoMicrophone => "CONTINUAR",
                _ => "OUVINDO..."
            });

            switch (phase)
            {
                case Phase.Connecting:
                    SetStatus("Ligando o microfone...", inkColor);
                    break;
                case Phase.ChooseMic:
                    RefreshChooseMicButton();
                    break;
                case Phase.Scale:
                    SetStatus("Quando estiver pronto, aperte COMEÇAR O TESTE.", inkColor);
                    break;
                case Phase.Prepare:
                    RefreshCountdown();
                    attemptText.SetText($"TENTATIVA {_attempt}  ·  PREPARE-SE");
                    break;
                case Phase.Result:
                    RefreshResult();
                    break;
            }

            RefreshMarks();
        }

        private string StepsLine(int step)
        {
            var parts = new string[StepNames.Length];

            for (int i = 0; i < StepNames.Length; i++)
            {
                Color color = i < step ? doneColor : i == step ? activeColor : mutedColor;
                parts[i] = $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{StepNames[i]}</color>";
            }

            return string.Join($"<color=#{ColorUtility.ToHtmlStringRGBA(mutedColor)}>  ·  </color>", parts);
        }

        private void SetStatus(string text, Color color)
        {
            statusText.SetText(text);
            statusText.color = color;
        }

        private void EnterNoMicrophone()
        {
            SetPhase(Phase.NoMicrophone);
            SetStatus("Não encontrei nenhum microfone. Dá para jogar assim e ajustar depois em Opções > Microfone.", inkColor);
        }

        /// <summary>
        /// Puts a tick where each level the player has given lands at the mic level now on, coloured by
        /// the zone it falls in — the fine-tuning line moves them.
        /// </summary>
        private void RefreshMarks()
        {
            if (_calibration == null) return;

            bool show = _phase is Phase.Prepare or Phase.Listening or Phase.Feedback or Phase.Result;

            for (int i = 0; i < tierMarks.Length; i++)
            {
                RectTransform mark = tierMarks[i];
                bool known = _calibration.TryPredict((MicTier)i, _volume, out float energy);

                mark.gameObject.SetActive(show && known);
                if (!show || !known) continue;

                float at = Mathf.Clamp(energy, 0.01f, 0.99f);
                mark.anchorMin = new Vector2(at, mark.anchorMin.y);
                mark.anchorMax = new Vector2(at, mark.anchorMax.y);
                mark.anchoredPosition = new Vector2(0f, mark.anchoredPosition.y);

                Color color = ColorFor(energy);
                foreach (Graphic graphic in mark.GetComponentsInChildren<Graphic>(true)) graphic.color = color;
                mark.GetComponentInChildren<TextMeshProUGUI>(true).SetText(MarkLabels[i]);
            }
        }

        private Color ColorFor(float energy)
        {
            if (energy >= _calibration.ShoutEnergy) return shoutColor;
            if (energy >= _calibration.SpeechEnergy) return speechColor;
            return whisperColor;
        }

        private void UpdateMeter()
        {
            // Vivox updates five times a second: the bar glides between them instead of jumping.
            _shownEnergy = Mathf.Lerp(_shownEnergy, _latestEnergy, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            meterFill.anchorMax = new Vector2(Mathf.Clamp01(_shownEnergy), meterFill.anchorMax.y);

            if (micPanel.activeSelf) trace.SetLevel(_phase == Phase.ChooseMic ? _shownEnergy : 0f);

            if (_latestEnergy >= _labelEnergy || Time.unscaledTime >= _labelUntil)
            {
                _labelEnergy = _latestEnergy;
                _labelUntil = Time.unscaledTime + labelHoldSeconds;
            }

            if (!scalePanel.activeSelf) return;

            if (_labelEnergy < NoiseFloor() + MicCalibration.VOICE_MARGIN) SetLevel("SILÊNCIO", silenceColor);
            else if (_labelEnergy >= _calibration.ShoutEnergy) SetLevel("GRITO", shoutColor);
            else if (_labelEnergy >= _calibration.SpeechEnergy) SetLevel("FALA", speechColor);
            else SetLevel("SUSSURRO", whisperColor);
        }

        private void SetLevel(string label, Color color)
        {
            levelText.SetText(label);
            levelText.color = color;
        }

        private void PlaceZones()
        {
            PlaceZone(whisperZone, 0f, _calibration.SpeechEnergy);
            PlaceZone(speechZone, _calibration.SpeechEnergy, _calibration.ShoutEnergy);
            PlaceZone(shoutZone, _calibration.ShoutEnergy, 1f);

            for (int i = 0; i < 3; i++) _zoneColors[i] = Zones[i].GetComponent<Image>().color;
        }

        private void TintZone(int index, float alpha)
        {
            Color color = _zoneColors[index];
            color.a *= alpha;
            Zones[index].GetComponent<Image>().color = color;

            foreach (TextMeshProUGUI label in Zones[index].GetComponentsInChildren<TextMeshProUGUI>(true))
                label.alpha = Mathf.Lerp(0.45f, 1f, alpha);
        }

        private static void PlaceZone(RectTransform zone, float from, float to)
        {
            zone.anchorMin = new Vector2(from, zone.anchorMin.y);
            zone.anchorMax = new Vector2(to, zone.anchorMax.y);
            zone.offsetMin = new Vector2(0f, zone.offsetMin.y);
            zone.offsetMax = new Vector2(0f, zone.offsetMax.y);
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
    }
}
