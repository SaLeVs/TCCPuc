using System.Collections.Generic;
using Monster.MonsterSabotages;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Switches off room lights whose reach ends far from the local camera, and back on as the
    /// player comes closer. Purely local: every peer culls around its own view.
    /// </summary>
    /// <remarks>
    /// <para>The level carries around 250 realtime lights. Forward+ only discards the ones outside
    /// the view frustum, so every lamp in every room along the line of sight - behind walls
    /// included - was still evaluated for the pixels it could touch. Measured from the room the
    /// players start in, cutting the ones twenty metres out saved about twelve percent of the GPU
    /// frame.</para>
    ///
    /// <para>Only room lights are touched. Anything under a <see cref="NetworkBehaviour"/> - the
    /// flashlight, lamp totems, the lamps puzzle - has gameplay deciding whether it is on, and a
    /// culler switching it back on would overrule that. Sabotaged lights are the other owner: the
    /// culler never relights one the monster has put out.</para>
    /// </remarks>
    public class LightDistanceCuller : MonoBehaviour
    {
        private struct Entry
        {
            public Light Light;
            public LightSabotage Sabotage;
            public bool CulledByUs;
        }

        [Tooltip("Metres between the camera and the edge of a light's range before it is switched off.")]
        [SerializeField, Min(0f)] private float cullDistance = 20f;

        [Tooltip("Extra metres a culled light waits before coming back, so one on the boundary does not blink.")]
        [SerializeField, Min(0f)] private float hysteresis = 3f;

        [Tooltip("Seconds between passes. The player cannot cover much ground in a quarter of a second.")]
        [SerializeField, Min(0.05f)] private float interval = 0.25f;

        [Tooltip("Seconds between looking for lights again. Rooms are spawned at runtime, after this starts.")]
        [SerializeField, Min(0.5f)] private float rescanInterval = 5f;

        private readonly List<Entry> _entries = new();
        private readonly HashSet<Light> _known = new();

        private Camera _viewer;
        private float _nextPass;
        private float _nextRescan;

        private void Update()
        {
            float now = Time.unscaledTime;

            if (now >= _nextRescan)
            {
                _nextRescan = now + rescanInterval;
                Rescan();
            }

            if (now < _nextPass) return;

            _nextPass = now + interval;

            if (_viewer == null || !_viewer.isActiveAndEnabled) _viewer = Camera.main;
            if (_viewer == null) return;

            Cull(_viewer.transform.position);
        }

        private void Rescan()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Light != null) continue;

                _entries.RemoveAt(i);
            }

            _known.RemoveWhere(light => light == null);

            foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (_known.Contains(light)) continue;

                _known.Add(light);

                if (!IsRoomLight(light)) continue;

                _entries.Add(new Entry
                {
                    Light = light,
                    Sabotage = light.GetComponent<LightSabotage>()
                });
            }
        }

        private static bool IsRoomLight(Light light)
        {
            if (light.type == LightType.Directional) return false;

            return light.GetComponentInParent<NetworkBehaviour>(true) == null;
        }

        private void Cull(Vector3 eye)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                Light light = entry.Light;

                if (light == null) continue;

                float gap = Vector3.Distance(eye, light.transform.position) - light.range;

                if (entry.CulledByUs)
                {
                    if (gap > cullDistance - hysteresis) continue;

                    // Handed back either way: from here on the light is whatever its owner says.
                    entry.CulledByUs = false;

                    bool putOut = entry.Sabotage != null && entry.Sabotage.IsSabotaged;
                    if (!putOut) light.enabled = true;
                }
                else
                {
                    if (!light.enabled || gap <= cullDistance) continue;

                    entry.CulledByUs = true;
                    light.enabled = false;
                }

                _entries[i] = entry;
            }
        }

        /// <summary>Leaves every light as its owner wants it, so disabling this is always safe.</summary>
        private void OnDisable()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (!entry.CulledByUs || entry.Light == null) continue;

                bool putOut = entry.Sabotage != null && entry.Sabotage.IsSabotaged;
                if (!putOut) entry.Light.enabled = true;

                entry.CulledByUs = false;
                _entries[i] = entry;
            }
        }
    }
}
