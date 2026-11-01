using System;
using System.Reflection;
using Audio;
using Components.Sound;
using Network;
using SteamAudio;
using UnityEngine;
using UnityEngine.Audio;
using Vector3 = UnityEngine.Vector3;

namespace LevelDesign.Gym.VoiceLab
{
    public enum LabVoiceLevel
    {
        [InspectorName("Sussurro")] Whisper,
        [InspectorName("Fala")] Speech,
        [InspectorName("Grito")] Shout,
        [InspectorName("Leitura de doação (TTS)")] Donation,
        [InspectorName("Gravação do microfone")] Recording,
    }

    public enum LabCurve
    {
        [InspectorName("Atual (igual ao jogo)")] Current,
        [InspectorName("Proposta")] Proposal,
    }

    /// <summary>Everything that takes level off the voice this frame, one term per stage.</summary>
    [Serializable]
    public struct LabReading
    {
        public float Distance;
        public float Energy;
        public float Range;
        public float TargetRange;
        public float CurveDb;
        public float WindowDb;
        public float Occlusion;
        public float TransmissionLow;
        public float TransmissionMid;
        public float TransmissionHigh;
        public float WallsDb;
        public float DirectivityDb;
        public float AirDb;
        public float SourceGainDb;
        public float MixerDb;
        public float PredictedDb;
        public float SourceDbfs;
        public float EarDbfs;
        public AudioReverbPreset Reverb;
    }

    /// <summary>
    /// A stand-in for another player's voice: the AudioSource chain RemoteVoiceFilter builds on the
    /// Vivox participant tap — voice mixer group, Steam Audio voice profile, VoiceGain, reverb of the
    /// speaker's zone and the distance window driven by how loudly they talk — fed by a recorded line
    /// instead of the network.
    ///
    /// <para>"Current" reads every number from the game's own assets (the Player prefab's
    /// RemoteVoiceFilter, its voice profile and HostManager's VivoxManager) and repeats
    /// RemoteVoiceFilter's maths step by step, so editing those assets is heard here straight away.
    /// "Proposal" is the alternative tuned on this component. Nothing in the game is changed.</para>
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class GymVoiceLabSpeaker : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Vivox refreshes a participant's AudioEnergy five times a second (VivoxManager logs in with
        // ParticipantPropertyUpdateFrequency.FivePerSecond), so the simulated energy steps at that rate too.
        private const float EnergyInterval = 0.2f;
        private const float EnvelopeWindow = 0.01f;
        private const int MeterSamples = 4096;

        [Header("Jogo (lido dos assets reais)")]
        [Tooltip("RemoteVoiceFilter do Player.prefab: mixer, perfil espacial, perfil de voz e envelope do alcance vêm dele.")]
        [SerializeField] private RemoteVoiceFilter gameVoiceFilter;

        [Tooltip("VivoxManager do HostManager.prefab: o Audible Distance dele é o alcance do grito (LoudRange).")]
        [SerializeField] private VivoxManager gameVivoxSettings;

        [Header("Boca e falas")]
        [SerializeField] private Transform mouth;
        [SerializeField] private AudioClip speechClip;
        [SerializeField] private AudioClip donationClip;

        [Header("Quanto o companheiro fala alto (simulado)")]
        [SerializeField] private LabVoiceLevel level = LabVoiceLevel.Speech;

        [Tooltip("Pico da energia do Vivox (AudioEnergy, 0..1) que cada intensidade alcança nas sílabas mais fortes. " +
                 "Entre palavras a energia cai, como no microfone de verdade. Compare com o medidor do Options → Testar microfone.")]
        [SerializeField, Range(0f, 1f)] private float whisperPeakEnergy = 0.2f;
        [SerializeField, Range(0f, 1f)] private float speechPeakEnergy = 0.5f;
        [SerializeField, Range(0f, 1f)] private float shoutPeakEnergy = 0.85f;

        [Tooltip("Nível da voz na fonte, em dB, para cada intensidade: com o AGC do Vivox desligado, quem grita chega mais alto no stream.")]
        [SerializeField] private float whisperGainDb = -12f;
        [SerializeField] private float speechGainDb;
        [SerializeField] private float shoutGainDb = 6f;
        [SerializeField] private float donationGainDb;

        [Tooltip("Volume que o ouvinte escolheu para este jogador no pause (-50..50, o mesmo slider).")]
        [SerializeField, Range(-50, 50)] private int playerVolume;

        [Header("Curva")]
        [SerializeField] private LabCurve curve = LabCurve.Current;

        [Tooltip("Até aqui (m) a voz chega com o volume em que foi falada. Hoje: Near Field do VoiceProfile (1 m).")]
        [SerializeField, Min(0.1f)] private float proposalNearField = 2f;

        [Tooltip("Quanto a voz perde a cada vez que a distância dobra, depois do Near Field. Hoje: 6 dB (lei do inverso da distância).")]
        [SerializeField, Range(1f, 6f)] private float proposalDbPerDoubling = 4.5f;

        [Tooltip("Alcance de um sussurro (m). Hoje: Whisper Range do VoiceProfile (8 m).")]
        [SerializeField, Min(1f)] private float proposalWhisperRange = 8f;

        [Tooltip("Alcance da fala normal (m). Hoje não existe: a fala fica entre o sussurro e o grito conforme a energia de cada sílaba.")]
        [SerializeField, Min(1f)] private float proposalSpeechRange = 24f;

        [Tooltip("Alcance do grito (m). Hoje: Audible Distance do VivoxManager (32 m).")]
        [SerializeField, Min(1f)] private float proposalShoutRange = 40f;

        [Tooltip("Fração do alcance onde a borda começa a fechar. Hoje a janela (1 - x²)² age no alcance inteiro: já tira 3 dB a 40% e 8 dB a 60%.")]
        [SerializeField, Range(0f, 0.95f)] private float proposalFadeStart = 0.6f;

        [SerializeField, Min(0.01f)] private float proposalAttackSeconds = 0.08f;

        [Tooltip("Segundos para o alcance recolher. Hoje: 0,9 s para o vão inteiro, então ele encolhe entre as sílabas.")]
        [SerializeField, Min(0.01f)] private float proposalReleaseSeconds = 2.5f;

        [Tooltip("Enquanto houver fala nos últimos N segundos o alcance não cai abaixo do da fala (como o hold de um noise gate).")]
        [SerializeField, Min(0f)] private float proposalHoldSeconds = 1.5f;

        [Tooltip("Tamanho da fonte para a oclusão (m). Hoje: Source Radius do VoiceSpatialProfile (0,25 m). Maior = a voz 'vaza' pelo vão da porta aos poucos.")]
        [SerializeField, Range(0.05f, 2f)] private float proposalSourceRadius = 1f;

        [Tooltip("Diretividade da boca. Hoje: 0,3 (≈ -8 dB de costas).")]
        [SerializeField, Range(0f, 1f)] private float proposalDirectivity = 0.15f;

        [Tooltip("Mínimo que passa por paredes/portas (graves, médios, agudos). Hoje: material Wall = 0,056 / 0,056 / 0,004 (≈ -25 dB).")]
        [SerializeField] private Vector3 proposalWallFloor = new Vector3(0.22f, 0.15f, 0.05f);

        private VoiceProfileSO _voiceProfile;
        private SpatialAudioProfileSO _spatialProfile;
        private AudioMixerGroup _mixerGroup;
        private bool _rangeScalesWithVoice = true;
        private float _attackSeconds = 0.08f;
        private float _releaseSeconds = 0.9f;

        private AudioSource _source;
        private SteamAudioSource _steam;
        private VoiceGain _gain;
        private AudioReverbFilter _reverb;
        private AudioListener _listener;
        private LabCurve _appliedCurve = (LabCurve)(-1);
        private AnimationCurve _proposalRolloff;
        private bool _settingsDirty;
        private float _nextRefreshTime;

        private AudioClip _recordedClip;
        private float[] _envelope;
        private float _envelopeReferenceDb;
        private AudioClip _envelopeClip;

        private float _range;
        private float _energy;
        private float _nextEnergyTime;
        private float _lastSpeechTime = -100f;
        private float _nextZoneTime;
        private readonly float[] _meter = new float[MeterSamples];

        private LabReading _reading;

        public LabReading Reading => _reading;
        public LabVoiceLevel Level => level;
        public LabCurve Curve => curve;
        public bool IsReady => _source != null;
        public bool IsPlaying => _source != null && _source.isPlaying;
        public bool HasRecording => _recordedClip != null;
        public AudioSource Source => _source;
        public Transform Mouth => mouth;

        public float WhisperRange => curve == LabCurve.Proposal ? proposalWhisperRange : QuietRange();
        public float SpeechRange => curve == LabCurve.Proposal ? proposalSpeechRange : Mathf.Lerp(QuietRange(), LoudRange(), 0.5f);
        public float ShoutRange => curve == LabCurve.Proposal ? proposalShoutRange : LoudRange();

        public string MissingReferences
        {
            get
            {
                if (gameVoiceFilter == null) return "RemoteVoiceFilter do Player.prefab";
                if (gameVivoxSettings == null) return "VivoxManager do HostManager.prefab";
                if (_voiceProfile == null) return "VoiceProfile no RemoteVoiceFilter";
                if (_spatialProfile == null) return "VoiceSpatialProfile no RemoteVoiceFilter";
                if (speechClip == null) return "clipe de fala";
                return null;
            }
        }

        public void Configure(RemoteVoiceFilter voiceFilter, VivoxManager vivoxSettings, Transform mouthPoint, AudioClip speech, AudioClip donation)
        {
            gameVoiceFilter = voiceFilter;
            gameVivoxSettings = vivoxSettings;
            mouth = mouthPoint;
            speechClip = speech;
            donationClip = donation;
        }

        private void Awake()
        {
            ReadGameSettings();
        }

        private void Start()
        {
            if (MissingReferences != null)
            {
                Debug.LogWarning($"Laboratório de voz: falta {MissingReferences}. Gere o laboratório de novo pelo menu Tools/TCC/Level Design/Laboratório de voz.");
                return;
            }

            BuildVoiceChain();
        }

        /// <summary>The same fields RemoteVoiceFilter serialises, read from the Player prefab's instance.</summary>
        private void ReadGameSettings()
        {
            if (gameVoiceFilter == null) return;

            Type type = typeof(RemoteVoiceFilter);

            _voiceProfile = type.GetField("voiceProfile", Private)?.GetValue(gameVoiceFilter) as VoiceProfileSO;
            _spatialProfile = type.GetField("spatialProfile", Private)?.GetValue(gameVoiceFilter) as SpatialAudioProfileSO;
            _mixerGroup = type.GetField("voiceMixerGroup", Private)?.GetValue(gameVoiceFilter) as AudioMixerGroup;

            if (type.GetField("rangeScalesWithVoice", Private)?.GetValue(gameVoiceFilter) is bool scales) _rangeScalesWithVoice = scales;
            if (type.GetField("rangeAttackSeconds", Private)?.GetValue(gameVoiceFilter) is float attack) _attackSeconds = attack;
            if (type.GetField("rangeReleaseSeconds", Private)?.GetValue(gameVoiceFilter) is float release) _releaseSeconds = release;
        }

        /// <summary>
        /// RemoteVoiceFilter.VivoxManager_OnParticipantJoined, on a plain AudioSource instead of the
        /// tap Vivox creates: same order, same settings.
        /// </summary>
        private void BuildVoiceChain()
        {
            var tap = new GameObject("VoiceTap_Laboratorio");
            tap.transform.SetParent(mouth != null ? mouth : transform, false);

            _source = tap.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.clip = CurrentClip();

            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.outputAudioMixerGroup = _mixerGroup;

            _steam = _spatialProfile.AttachTo(_source) as SteamAudioSource;

            _source.minDistance = _voiceProfile.NearField;
            _source.maxDistance = LoudRange();

            _gain = tap.AddComponent<VoiceGain>();
            _reverb = tap.AddComponent<AudioReverbFilter>();
            _reverb.reverbPreset = AudioReverbPreset.Off;

            _range = _rangeScalesWithVoice ? QuietRange() : LoudRange();
            _appliedCurve = (LabCurve)(-1);

            PrepareEnvelope(_source.clip);
            _source.Play();
        }

        public void TogglePlaying()
        {
            if (_source == null) return;

            if (_source.isPlaying) _source.Pause();
            else _source.Play();
        }

        public void SetLevel(LabVoiceLevel newLevel)
        {
            if (newLevel == LabVoiceLevel.Recording && _recordedClip == null) newLevel = LabVoiceLevel.Speech;

            level = newLevel;
            SwapClip();
        }

        public void CycleLevel()
        {
            int count = Enum.GetValues(typeof(LabVoiceLevel)).Length;
            var next = (LabVoiceLevel)(((int)level + 1) % count);

            if (next == LabVoiceLevel.Recording && _recordedClip == null) next = LabVoiceLevel.Whisper;

            SetLevel(next);
        }

        public void SetCurve(LabCurve newCurve) => curve = newCurve;

        public void ToggleCurve() => curve = curve == LabCurve.Current ? LabCurve.Proposal : LabCurve.Current;

        public void UseRecording(AudioClip clip)
        {
            _recordedClip = clip;
            SetLevel(clip != null ? LabVoiceLevel.Recording : LabVoiceLevel.Speech);
        }

        private AudioClip CurrentClip()
        {
            switch (level)
            {
                case LabVoiceLevel.Donation: return donationClip != null ? donationClip : speechClip;
                case LabVoiceLevel.Recording: return _recordedClip != null ? _recordedClip : speechClip;
                default: return speechClip;
            }
        }

        private void SwapClip()
        {
            if (_source == null) return;

            AudioClip clip = CurrentClip();
            if (_source.clip == clip) return;

            bool wasPlaying = _source.isPlaying;
            _source.Stop();
            _source.clip = clip;
            PrepareEnvelope(clip);

            if (wasPlaying) _source.Play();
        }

        private void Update()
        {
            if (_source == null) return;

            ApplyCurveSettings();
            UpdateZone();
            UpdateEnergy();
            UpdateRange();
            ApplyRange();
        }

        /// <summary>
        /// After SteamAudioManager.LateUpdate wrote this frame's simulation into the source, so the
        /// floor reaches the spatializer on the next SteamAudioSource.Update.
        /// </summary>
        private void LateUpdate()
        {
            if (_steam == null) return;

            if (curve == LabCurve.Proposal && _steam.transmission)
            {
                _steam.transmissionLow = Mathf.Max(_steam.transmissionLow, proposalWallFloor.x);
                _steam.transmissionMid = Mathf.Max(_steam.transmissionMid, proposalWallFloor.y);
                _steam.transmissionHigh = Mathf.Max(_steam.transmissionHigh, proposalWallFloor.z);
            }

            Measure();
        }

        private void OnValidate() => _settingsDirty = true;

        /// <summary>
        /// Re-applied when the curve flips, when this component is edited, and once a second anyway,
        /// so tuning the game's own assets during Play is heard without restarting.
        /// </summary>
        private void ApplyCurveSettings()
        {
            if (_appliedCurve == curve && !_settingsDirty && Time.unscaledTime < _nextRefreshTime) return;

            _settingsDirty = false;
            _nextRefreshTime = Time.unscaledTime + 1f;
            ReadGameSettings();

            if (curve == LabCurve.Current)
            {
                // Back to exactly what the game builds: the profile re-applied over every Steam Audio field.
                _spatialProfile.AttachTo(_source);
                _source.rolloffMode = AudioRolloffMode.Logarithmic;
                _source.minDistance = _voiceProfile.NearField;
                _source.maxDistance = LoudRange();
            }
            else
            {
                _spatialProfile.AttachTo(_source);

                _source.rolloffMode = AudioRolloffMode.Custom;
                _source.maxDistance = Mathf.Max(proposalShoutRange, proposalNearField + 1f);
                _proposalRolloff = BuildProposalRolloff(_source.maxDistance);
                _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _proposalRolloff);

                if (_steam != null)
                {
                    _steam.occlusionRadius = proposalSourceRadius;
                    _steam.directivity = proposalDirectivity > 0f;
                    _steam.dipoleWeight = proposalDirectivity;
                }
            }

            _appliedCurve = curve;
        }

        /// <summary>Flat to the near field, then a fixed number of dB per doubling of distance.</summary>
        private AnimationCurve BuildProposalRolloff(float maxDistance)
        {
            var keys = new Keyframe[40];

            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / (keys.Length - 1f);
                float distance = Mathf.Max(0.01f, Mathf.Pow(t, 2f) * maxDistance);
                keys[i] = new Keyframe(distance / maxDistance, ProposalDistanceGain(distance));
            }

            var rolloff = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) rolloff.SmoothTangents(i, 0f);

            return rolloff;
        }

        private float ProposalDistanceGain(float distance)
        {
            if (distance <= proposalNearField) return 1f;

            float doublings = Mathf.Log(distance / proposalNearField, 2f);
            return Mathf.Pow(10f, -proposalDbPerDoubling * doublings / 20f);
        }

        /// <summary>
        /// The speaker's zone, which is what RemoteVoiceFilter's reverb follows. In the game a zone
        /// counts once the player's capsule touches its trigger, so a capsule of the same size is used.
        /// </summary>
        private void UpdateZone()
        {
            if (Time.time < _nextZoneTime || _reverb == null) return;

            _nextZoneTime = Time.time + 0.5f;

            AudioReverbPreset preset = AudioReverbPreset.Off;
            Vector3 feet = transform.position;

            foreach (Collider hit in Physics.OverlapCapsule(feet + Vector3.up * 0.3f, feet + Vector3.up * 1.12f, 0.3f, ~0, QueryTriggerInteraction.Collide))
            {
                var zone = hit.GetComponent<VoiceZoneTrigger>();
                if (zone == null) continue;

                preset = zone.ReverbPreset;
                break;
            }

            if (_reverb.reverbPreset != preset) _reverb.reverbPreset = preset;
        }

        /// <summary>
        /// Stands in for VivoxParticipant.AudioEnergy: the line's own loudness, sampled five times a
        /// second, scaled so its strongest syllables reach the chosen level's peak.
        /// </summary>
        private void UpdateEnergy()
        {
            if (Time.time < _nextEnergyTime) return;

            _nextEnergyTime = Time.time + EnergyInterval;

            if (!_source.isPlaying || _envelope == null)
            {
                _energy = 0f;
                return;
            }

            float rmsDb = EnvelopeDb(EnergyInterval);

            if (level == LabVoiceLevel.Recording)
            {
                // A real recording has its own level: mapped straight from dBFS (−56 → 0, −8 → 1).
                _energy = Mathf.Clamp01((rmsDb + 56f) / 48f);
            }
            else
            {
                float relative = Mathf.Clamp01(1f + (rmsDb - _envelopeReferenceDb) / 36f);
                _energy = rmsDb < -60f ? 0f : PeakEnergy() * relative;
            }

            if (_voiceProfile != null && _energy >= _voiceProfile.SpeechEnergy) _lastSpeechTime = Time.time;
        }

        private float PeakEnergy()
        {
            switch (level)
            {
                case LabVoiceLevel.Whisper: return whisperPeakEnergy;
                case LabVoiceLevel.Shout: return shoutPeakEnergy;
                default: return speechPeakEnergy;
            }
        }

        private float LevelGainDb()
        {
            switch (level)
            {
                case LabVoiceLevel.Whisper: return whisperGainDb;
                case LabVoiceLevel.Shout: return shoutGainDb;
                case LabVoiceLevel.Donation: return donationGainDb;
                case LabVoiceLevel.Recording: return 0f;
                default: return speechGainDb;
            }
        }

        /// <summary>RemoteVoiceFilter.Update for "Current"; the hold-and-floor version for "Proposal".</summary>
        private void UpdateRange()
        {
            bool reading = level == LabVoiceLevel.Donation;
            float target;

            if (curve == LabCurve.Current)
            {
                float quiet = QuietRange();
                float loud = LoudRange();

                if (reading)
                {
                    _range = loud;
                    target = loud;
                }
                else if (_rangeScalesWithVoice)
                {
                    float loudness = _voiceProfile.Loudness(_energy);
                    target = Mathf.Lerp(quiet, loud, loudness);

                    float seconds = target > _range ? _attackSeconds : _releaseSeconds;
                    float rate = Mathf.Max(loud - quiet, 0.01f) / seconds;

                    _range = Mathf.MoveTowards(_range, target, rate * Time.deltaTime);
                }
                else
                {
                    target = loud;
                    _range = loud;
                }
            }
            else
            {
                float whisper = proposalWhisperRange;
                float speech = Mathf.Max(proposalSpeechRange, whisper);
                float shout = Mathf.Max(proposalShoutRange, speech);

                if (reading)
                {
                    target = shout;
                }
                else if (_energy >= _voiceProfile.SpeechEnergy)
                {
                    target = Mathf.Lerp(speech, shout, _voiceProfile.Loudness(_energy));
                }
                else
                {
                    target = Time.time - _lastSpeechTime <= proposalHoldSeconds ? speech : whisper;
                }

                float seconds = target > _range ? proposalAttackSeconds : proposalReleaseSeconds;
                float rate = Mathf.Max(shout - whisper, 0.01f) / seconds;

                _range = Mathf.MoveTowards(_range, target, rate * Time.deltaTime);
            }

            _reading.TargetRange = target;
        }

        /// <summary>RemoteVoiceFilter.ApplyRange: the window on the source volume, the chosen volume on the gain.</summary>
        private void ApplyRange()
        {
            float distance = 0f;
            float window = 0f;

            if (TryGetListener(out AudioListener listener))
            {
                distance = Vector3.Distance(listener.transform.position, _source.transform.position);
                float x = distance / Mathf.Max(_range, 0.01f);

                if (curve == LabCurve.Current)
                {
                    if (x < 1f)
                    {
                        window = 1f - x * x;
                        window *= window;
                    }
                }
                else
                {
                    if (x <= proposalFadeStart) window = 1f;
                    else if (x < 1f) window = 0.5f * (1f + Mathf.Cos(Mathf.PI * (x - proposalFadeStart) / (1f - proposalFadeStart)));
                }
            }

            float levelGain = Mathf.Pow(10f, LevelGainDb() / 20f);
            float chosen = ConvertVivoxVolumeToLinear(playerVolume);

            _source.volume = window;
            _gain.Gain = levelGain * chosen;

            _reading.Distance = distance;
            _reading.Energy = _energy;
            _reading.Range = _range;
            _reading.CurveDb = ToDb(CurveGain(distance));
            _reading.WindowDb = ToDb(window);
            _reading.SourceGainDb = ToDb(levelGain * chosen);
        }

        /// <summary>The source's distance curve at <paramref name="distance"/>, as Unity evaluates it.</summary>
        private float CurveGain(float distance)
        {
            if (_source.rolloffMode == AudioRolloffMode.Custom)
            {
                AnimationCurve custom = _source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                return custom.Evaluate(Mathf.Clamp01(distance / _source.maxDistance));
            }

            float min = _source.minDistance;
            float max = Mathf.Max(_source.maxDistance, min);
            float clamped = Mathf.Clamp(distance, min, max);

            return min / Mathf.Max(clamped, 0.0001f);
        }

        /// <summary>
        /// Reads what Steam Audio actually applied (it writes its simulation into the source's public
        /// fields) and what reaches the listener, so the panel shows the engine's numbers, not a model.
        /// </summary>
        private void Measure()
        {
            if (_steam != null)
            {
                float occlusion = _steam.occlusion ? _steam.occlusionValue : 1f;
                float low = _steam.transmission ? _steam.transmissionLow : 0f;
                float mid = _steam.transmission ? _steam.transmissionMid : 0f;
                float high = _steam.transmission ? _steam.transmissionHigh : 0f;

                _reading.Occlusion = occlusion;
                _reading.TransmissionLow = low;
                _reading.TransmissionMid = mid;
                _reading.TransmissionHigh = high;
                _reading.WallsDb = ToDb(occlusion + (1f - occlusion) * mid);
                _reading.DirectivityDb = _steam.directivity ? ToDb(_steam.directivityValue) : 0f;
                _reading.AirDb = _steam.airAbsorption ? ToDb(_steam.airAbsorptionMid) : 0f;
            }

            _reading.MixerDb = 0f;

            if (_mixerGroup != null && _mixerGroup.audioMixer != null && _mixerGroup.audioMixer.GetFloat("voiceVolume", out float mixerDb))
            {
                _reading.MixerDb = mixerDb;
            }

            _reading.Reverb = _reverb != null ? _reverb.reverbPreset : AudioReverbPreset.Off;

            _reading.PredictedDb = _reading.CurveDb + _reading.WindowDb + _reading.WallsDb + _reading.DirectivityDb +
                                   _reading.AirDb + _reading.SourceGainDb + _reading.MixerDb;

            _reading.SourceDbfs = _source.isPlaying && _envelope != null ? EnvelopeDb(0.2f) + _reading.SourceGainDb : -120f;
            _reading.EarDbfs = ListenerDbfs();
        }

        private float ListenerDbfs()
        {
            double power = 0;

            for (int channel = 0; channel < 2; channel++)
            {
                AudioListener.GetOutputData(_meter, channel);

                for (int i = 0; i < _meter.Length; i++) power += _meter[i] * _meter[i];
            }

            power /= _meter.Length * 2;

            return power <= 1e-12 ? -120f : 10f * (float)Math.Log10(power);
        }

        private void PrepareEnvelope(AudioClip clip)
        {
            _envelope = null;
            _envelopeClip = clip;

            if (clip == null) return;

            int channels = clip.channels;
            int frames = clip.samples;
            var data = new float[frames * channels];

            if (!clip.GetData(data, 0)) return;

            int window = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * EnvelopeWindow));
            int count = Mathf.CeilToInt(frames / (float)window);
            _envelope = new float[count];

            for (int w = 0; w < count; w++)
            {
                double sum = 0;
                int start = w * window;
                int end = Mathf.Min(frames, start + window);

                for (int f = start; f < end; f++)
                {
                    float sample = data[f * channels];
                    sum += sample * sample;
                }

                _envelope[w] = (float)(sum / Mathf.Max(1, end - start));
            }

            // Reference: the strongest 10% of voiced windows, so "peak energy" means the loud syllables.
            var voiced = new System.Collections.Generic.List<float>();
            foreach (float power in _envelope)
            {
                if (power > 1e-6f) voiced.Add(power);
            }

            voiced.Sort();
            float reference = voiced.Count > 0 ? voiced[Mathf.Clamp(Mathf.RoundToInt(voiced.Count * 0.9f), 0, voiced.Count - 1)] : 1f;
            _envelopeReferenceDb = 10f * Mathf.Log10(Mathf.Max(reference, 1e-12f));
        }

        /// <summary>Mean level of the line over the last <paramref name="seconds"/> before the play head, in dBFS.</summary>
        private float EnvelopeDb(float seconds)
        {
            if (_envelope == null || _envelope.Length == 0 || _source.clip != _envelopeClip) return -120f;

            int head = Mathf.FloorToInt(_source.time / EnvelopeWindow);
            int span = Mathf.Max(1, Mathf.RoundToInt(seconds / EnvelopeWindow));
            double sum = 0;

            for (int i = 0; i < span; i++)
            {
                int index = ((head - i) % _envelope.Length + _envelope.Length) % _envelope.Length;
                sum += _envelope[index];
            }

            double power = sum / span;
            return power <= 1e-12 ? -120f : 10f * (float)Math.Log10(power);
        }

        private bool TryGetListener(out AudioListener listener)
        {
            if (_listener == null || !_listener.isActiveAndEnabled)
            {
                _listener = FindFirstObjectByType<AudioListener>();
            }

            listener = _listener;
            return listener != null;
        }

        private float LoudRange()
        {
            if (gameVivoxSettings == null) return 32f;

            return gameVivoxSettings.AudibleDistance;
        }

        private float QuietRange()
        {
            float loud = LoudRange();
            float nearField = _voiceProfile != null ? _voiceProfile.NearField : 1f;
            float floor = nearField + 0.5f;

            if (floor >= loud) return loud;

            float whisper = _voiceProfile != null ? _voiceProfile.WhisperRange : loud;
            return Mathf.Clamp(whisper, floor, loud);
        }

        /// <summary>VivoxManager.ConvertVivoxVolumeToLinear: -50..0 → 0..1, 0..50 → 1..2.</summary>
        private static float ConvertVivoxVolumeToLinear(int vivoxVolume)
        {
            vivoxVolume = Mathf.Clamp(vivoxVolume, -50, 50);

            return vivoxVolume >= 0 ? Mathf.Lerp(1f, 2f, vivoxVolume / 50f) : Mathf.Lerp(0f, 1f, (vivoxVolume + 50f) / 50f);
        }

        private static float ToDb(float gain) => gain <= 1e-6f ? -120f : 20f * Mathf.Log10(gain);
    }
}
