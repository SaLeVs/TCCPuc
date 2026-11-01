using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Components.Sound
{
    /// <summary>
    /// Keeps a Steam Audio source's transmission from dropping below a floor, per band.
    ///
    /// <para>The materials decide how much of a sound goes through a wall, and for the world that is
    /// right. A teammate's voice two rooms away is different: the default wall lets through about
    /// −25 dB with no highs, so the moment a wall came between two players the voice went from clear
    /// to gone in one step. The floor turns that into muffled. Added and set by
    /// SpatialAudioProfileSO.AttachTo; nothing else needs to know about it.</para>
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class SteamAudioTransmissionFloor : MonoBehaviour
    {
        private SteamAudioSource _source;

        /// <summary>Least transmission per band: low, mid, high, each 0..1.</summary>
        public Vector3 Floor { get; set; }

        private void Awake()
        {
            _source = GetComponent<SteamAudioSource>();
        }

        // After SteamAudioManager.LateUpdate wrote this frame's simulation into the source; the
        // source hands the fields to the spatializer in its next Update.
        private void LateUpdate()
        {
            if (_source == null || !_source.transmission) return;

            _source.transmissionLow = Mathf.Max(_source.transmissionLow, Floor.x);
            _source.transmissionMid = Mathf.Max(_source.transmissionMid, Floor.y);
            _source.transmissionHigh = Mathf.Max(_source.transmissionHigh, Floor.z);
        }
    }
}
