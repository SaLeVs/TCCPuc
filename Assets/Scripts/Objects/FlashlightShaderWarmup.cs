using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Objects
{
    /// <summary>
    /// Shows the camera an invisible stand-in of the flashlight beam for a few frames after the
    /// player spawns, so the shader variants the beam needs are ready before it is first switched on.
    /// </summary>
    /// <remarks>
    /// <para>The beam is the only light in the level with a cookie and soft shadows. Switching it
    /// on flips <c>_LIGHT_COOKIES</c> and <c>_SHADOWS_SOFT</c> for every material on screen, and the
    /// first time a material is drawn that way its variant has to be compiled (Editor) or its
    /// pipeline state built (D3D12 builds) - the freeze on the first click.</para>
    ///
    /// <para>Unity culls lights below about a thousandth of intensity (measured: 0.0001 is culled,
    /// 0.001 is not), and URP only counts soft shadows for a light with shadow casters in range, so
    /// the stand-in copies the beam's reach and shines just above that, where nobody can see it.
    /// Only the local player's flashlight does it: the camera is theirs.</para>
    /// </remarks>
    public class FlashlightShaderWarmup : MonoBehaviour
    {
        [SerializeField] private Light beam;

        [Tooltip("Seconds after spawning, so the camera is already on the player's view.")]
        [SerializeField, Min(0f)] private float delay = 0.5f;

        [Tooltip("Frames the stand-in stays on.")]
        [SerializeField, Min(1)] private int frames = 3;

        private IEnumerator Start()
        {
            NetworkObject networkObject = GetComponentInParent<NetworkObject>();
            if (networkObject != null && !networkObject.IsOwner) yield break;

            yield return new WaitForSeconds(delay);

            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (view == null) yield break;

            Light standIn = CreateStandIn(view.transform);

            for (int i = 0; i < frames; i++) yield return null;

            Destroy(standIn.gameObject);
        }

        private Light CreateStandIn(Transform view)
        {
            Light standIn = new GameObject("Flashlight Shader Warmup").AddComponent<Light>();
            standIn.transform.SetParent(view, false);

            standIn.type = LightType.Spot;
            standIn.spotAngle = beam.spotAngle;
            standIn.innerSpotAngle = beam.innerSpotAngle;
            standIn.range = beam.range;
            standIn.cookie = beam.cookie;
            standIn.shadows = beam.shadows;
            standIn.intensity = 0.005f;

            return standIn;
        }
    }
}
