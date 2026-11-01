using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Components.Sound
{
    /// <summary>
    /// How Steam Audio treats a family of sources: the HRTF that places them around the head, the
    /// air, the walls in the way and which way the source faces.
    ///
    /// <para>One asset for world sounds and one for voices, so each is tuned in one place instead of
    /// in every script that makes an AudioSource. Which colliders count as walls, and what they are
    /// made of, lives in Steam Audio's own settings asset and on the Steam Audio Geometry components.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SpatialProfile", menuName = "ScriptableObjects/Audio/Spatial Audio Profile")]
    public class SpatialAudioProfileSO : ScriptableObject
    {
        [Header("Direction")]
        [Tooltip("Bilinear costs a little more and keeps a moving source from stepping between HRTF samples.")]
        [SerializeField] private HRTFInterpolation interpolation = HRTFInterpolation.Bilinear;

        [Header("Air")]
        [Tooltip("Distant sounds lose their highs first, the way they do through real air.")]
        [SerializeField] private bool airAbsorption = true;

        [Header("Walls")]
        [SerializeField] private bool occlusion = true;

        [Tooltip("How big the source is, in meters. Rays aim at points spread through this sphere, so a " +
                 "source half behind a corner comes through half muffled instead of flicking on and off.")]
        [SerializeField, Range(0.05f, 2f)] private float sourceRadius = 0.4f;

        [Tooltip("Rays per source per frame. Capped by Max Occlusion Samples in the Steam Audio settings.")]
        [SerializeField, Range(1, 16)] private int occlusionSamples = 12;

        [Tooltip("Sound that goes through the wall instead of around it, filtered by the wall's Steam Audio " +
                 "material: muffled, not silenced.")]
        [SerializeField] private bool transmission = true;

        [Tooltip("How many surfaces in a row count — a wall and then a door, say.")]
        [SerializeField, Range(1, 8)] private int maxTransmissionSurfaces = 2;

        [Tooltip("Least that always gets through walls and doors, per band (low, mid, high), 0..1. Zero " +
                 "leaves it all to the Steam Audio materials, which is right for the world. For voices: " +
                 "the default wall lets ~-25 dB through, so a teammate went from clear to gone the step " +
                 "a wall came between you; a floor makes them muffled instead.")]
        [SerializeField] private Vector3 minimumTransmission;

        [Header("Facing")]
        [Tooltip("0 radiates the same all round. Higher is louder in front: for a voice, talking away " +
                 "from someone makes you quieter to them. Follows the source transform's forward.")]
        [SerializeField, Range(0f, 1f)] private float directivity;

        [Tooltip("How narrow the front lobe is. 1 is a gentle cardioid-like falloff.")]
        [SerializeField, Range(0f, 4f)] private float directivitySharpness = 1f;

        /// <summary>
        /// Turns <paramref name="audioSource"/> into a Steam Audio source configured from this profile.
        /// The AudioSource has to exist first: SteamAudioSource looks for it in its own Awake. Returns
        /// the component so callers can switch it off while idle without referencing Steam Audio.
        /// </summary>
        public Behaviour AttachTo(AudioSource audioSource)
        {
            audioSource.spatialize = true;
            audioSource.spatializePostEffects = false;

            if (!audioSource.TryGetComponent(out SteamAudioSource source))
            {
                source = audioSource.gameObject.AddComponent<SteamAudioSource>();
            }

            Apply(source);
            ApplyTransmissionFloor(source);
            return source;
        }

        private void ApplyTransmissionFloor(SteamAudioSource source)
        {
            bool wanted = transmission && occlusion && minimumTransmission.sqrMagnitude > 0f;
            source.TryGetComponent(out SteamAudioTransmissionFloor floor);

            if (!wanted)
            {
                if (floor != null) floor.enabled = false;
                return;
            }

            if (floor == null) floor = source.gameObject.AddComponent<SteamAudioTransmissionFloor>();

            floor.Floor = minimumTransmission;
            floor.enabled = true;
        }

        private void Apply(SteamAudioSource source)
        {
            source.directBinaural = true;
            source.interpolation = interpolation;

            // Curve-driven: Steam Audio applies the AudioSource's own rolloff, which is where the range
            // of every sound — and the loudness-driven range of every voice — already lives.
            source.distanceAttenuation = true;
            source.distanceAttenuationInput = DistanceAttenuationInput.CurveDriven;

            source.airAbsorption = airAbsorption;
            source.airAbsorptionInput = AirAbsorptionInput.SimulationDefined;

            source.occlusion = occlusion;
            source.occlusionInput = OcclusionInput.SimulationDefined;
            source.occlusionType = OcclusionType.Volumetric;
            source.occlusionRadius = sourceRadius;
            source.occlusionSamples = occlusionSamples;

            source.transmission = occlusion && transmission;
            source.transmissionType = TransmissionType.FrequencyDependent;
            source.transmissionInput = TransmissionInput.SimulationDefined;
            source.maxTransmissionSurfaces = maxTransmissionSurfaces;

            source.directivity = directivity > 0f;
            source.directivityInput = DirectivityInput.SimulationDefined;
            source.dipoleWeight = directivity;
            source.dipolePower = directivitySharpness;

            // Both trace the scene on Steam Audio's worker thread, and this project's scene is Unity's
            // physics, which only answers on the main thread.
            source.reflections = false;
            source.pathing = false;
        }
    }
}
