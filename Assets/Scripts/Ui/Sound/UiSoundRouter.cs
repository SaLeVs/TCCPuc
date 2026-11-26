using Components.Sound;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ui.Sound
{
    /// <summary>
    /// Gives every Selectable in the game a <see cref="UiButtonSound"/> and plays what they ask for.
    /// Starts itself on load (no object to place in any scene) and survives scene changes.
    /// </summary>
    /// <remarks>
    /// <para>Scenes are swept once when they load (inactive panels included); after that the active
    /// selectables are checked every frame, which catches UI built or spawned at runtime - the
    /// player's HUD, lobby slots, dropdown lists - before anyone can click it.</para>
    ///
    /// <para>It plays through its own small pool of 2D sources rather than the world's
    /// <see cref="SoundPlayer"/>, which only exists in some scenes; they ignore the listener pause,
    /// so the pause menu still clicks.</para>
    /// </remarks>
    public class UiSoundRouter : MonoBehaviour
    {
        private const int Voices = 6;

        private static UiSoundRouter _instance;

        private UiSoundSettingsSO _settings;
        private AudioSource[] _sources;
        private int _next;
        private float _lastHoverTime = -10f;
        private Selectable[] _buffer = new Selectable[64];

        public static UiSoundSettingsSO Settings => _instance != null ? _instance._settings : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;

            UiSoundSettingsSO settings = Resources.Load<UiSoundSettingsSO>(UiSoundSettingsSO.ResourcePath);
            if (settings == null) return;

            GameObject host = new("UiSoundRouter");
            DontDestroyOnLoad(host);

            UiSoundRouter router = host.AddComponent<UiSoundRouter>();
            router._settings = settings;
            router.BuildSources();

            _instance = router;
            router.SweepAll();
        }

        private void BuildSources()
        {
            _sources = new AudioSource[Voices];

            for (int i = 0; i < Voices; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                source.ignoreListenerVolume = false;
                source.priority = 32;
                source.outputAudioMixerGroup = _settings.output;
                _sources[i] = source;
            }
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SweepAll();

        /// <summary>Every selectable loaded, hidden panels included, plus what their dropdowns will clone.</summary>
        private void SweepAll()
        {
            foreach (Selectable selectable in FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Attach(selectable);
            }
        }

        private void Update()
        {
            int count = Selectable.allSelectableCount;
            if (_buffer.Length < count) _buffer = new Selectable[Mathf.NextPowerOfTwo(count)];

            int filled = Selectable.AllSelectablesNoAlloc(_buffer);
            for (int i = 0; i < filled; i++)
            {
                Selectable selectable = _buffer[i];
                if (selectable != null && !selectable.TryGetComponent(out UiButtonSound _)) Attach(selectable);
                _buffer[i] = null;
            }
        }

        private static void Attach(Selectable selectable)
        {
            if (selectable == null || selectable.TryGetComponent(out UiButtonSound _)) return;

            selectable.gameObject.AddComponent<UiButtonSound>();

            // The list a dropdown opens is cloned from its template: hook the template and every
            // clone comes with its sound.
            RectTransform template = selectable switch
            {
                TMPro.TMP_Dropdown tmp => tmp.template,
                Dropdown legacy => legacy.template,
                _ => null
            };

            if (template == null) return;

            foreach (Selectable item in template.GetComponentsInChildren<Selectable>(true))
            {
                if (!item.TryGetComponent(out UiButtonSound _)) item.gameObject.AddComponent<UiButtonSound>();
            }
        }

        public static void Play(UiSoundKind kind)
        {
            if (_instance == null) return;

            UiSoundSettingsSO settings = _instance._settings;
            SoundDefinitionSO sound = kind switch
            {
                UiSoundKind.Click => settings.click,
                UiSoundKind.Confirm => settings.confirm,
                UiSoundKind.Close => settings.close,
                _ => null
            };

            _instance.PlayDefinition(sound, 1f);
        }

        public static void PlayTick(float pitch)
        {
            if (_instance == null) return;

            _instance.PlayDefinition(_instance._settings.sliderTick, pitch);
        }

        public static void PlayHover()
        {
            if (_instance == null) return;

            UiSoundSettingsSO settings = _instance._settings;
            if (settings.hover == null) return;

            // Sweeping the mouse across a row of buttons gives one sound per button, not a burst.
            if (Time.unscaledTime - _instance._lastHoverTime < settings.hoverMinInterval) return;
            _instance._lastHoverTime = Time.unscaledTime;

            _instance.PlayDefinition(settings.hover, 1f);
        }

        private void PlayDefinition(SoundDefinitionSO sound, float pitchScale)
        {
            if (sound == null) return;

            AudioClip clip = sound.PickClip();
            if (clip == null) return;

            AudioSource source = _sources[_next];
            _next = (_next + 1) % _sources.Length;

            source.Stop();
            source.clip = clip;
            source.volume = sound.Volume;
            source.pitch = sound.PickPitch() * pitchScale;
            source.Play();
        }
    }
}
