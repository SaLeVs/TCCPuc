using Components.Sound;
using UnityEngine;
using UnityEngine.Audio;

namespace Ui.Sound
{
    /// <summary>
    /// Which sound each kind of UI press makes, for every button, toggle and slider in the game.
    /// Loaded from Resources/UiSoundSettings, so it works in every scene with no setup.
    /// To change a sound, swap the clip in the Sound Definition it points to (or the definition).
    /// </summary>
    [CreateAssetMenu(fileName = "UiSoundSettings", menuName = "ScriptableObjects/Audio/UI Sound Settings")]
    public class UiSoundSettingsSO : ScriptableObject
    {
        public const string ResourcePath = "UiSoundSettings";

        [Header("Sounds")]
        [Tooltip("Any ordinary button: arrows, open a panel, toggles, dropdowns.")]
        public SoundDefinitionSO click;

        [Tooltip("Buttons that commit to something: ESTOU PRONTO, PRONTO, SALVAR, INICIAR, CRIAR, ENTRAR...")]
        public SoundDefinitionSO confirm;

        [Tooltip("Buttons that back out: VOLTAR, MENU, SAIR, FECHAR, X, OK on a notice, CONTINUAR (closes the pause).")]
        public SoundDefinitionSO close;

        [Tooltip("Each notch a slider passes while the player drags it (or moves it with the keyboard).")]
        public SoundDefinitionSO sliderTick;

        [Tooltip("The mouse arriving over a button, toggle or dropdown that can be clicked. Empty: no hover sound.")]
        public SoundDefinitionSO hover;

        [Header("Hover")]
        [Tooltip("Shortest gap between two hover sounds, in seconds, so sweeping the mouse across a row of buttons does not machine-gun.")]
        [Min(0f)] public float hoverMinInterval = 0.06f;

        [Header("Output")]
        [Tooltip("Mixer group the UI plays through, so the SFX volume in Options applies. Empty plays straight to the listener.")]
        public AudioMixerGroup output;

        [Header("Sliders")]
        [Tooltip("How many notches a slider is split into for the tick (whole-number sliders with fewer steps tick on every step).")]
        [Min(2)] public int sliderNotches = 20;

        [Tooltip("Shortest gap between two ticks, in seconds, so a fast drag does not buzz.")]
        [Min(0f)] public float sliderMinInterval = 0.04f;

        [Tooltip("Tick pitch at the bar's minimum and maximum: the sound climbs as the bar fills.")]
        public Vector2 sliderPitch = new(0.85f, 1.3f);
    }
}
