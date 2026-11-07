using System.Collections.Generic;
using Monster.MonsterSabotages;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Systems
{
    /// <summary>
    /// Finishes the baked bounce light at runtime: adds a faint fill to it, and dims it all while the
    /// monster has the lights out.
    /// </summary>
    /// <remarks>
    /// <para>The light probes hold the light of every lamp bouncing off the walls, and that alone
    /// leaves black what no lamp reaches. They also cannot react to a lamp going out: the level would
    /// keep glowing through a blackout. A light sabotage always takes every lamp at once, so one
    /// switch for the whole level is enough. Purely local, reading the same sabotage state that
    /// switches the lamps on each peer.</para>
    ///
    /// <para>Creates itself in any scene that has both probes and sabotageable lights.</para>
    /// </remarks>
    public class BakedBounceLight : MonoBehaviour
    {
        // Share of the scene's ambient light added to every probe.
        private const float AmbientFill = 0.3f;

        // Not zero: the flashlight is the main light in a blackout, but the player still needs a hint of the room.
        private const float BlackoutScale = 0.25f;

        private const float CheckInterval = 0.25f;
        private const float RescanInterval = 5f;

        private readonly List<LightSabotage> _lights = new();

        private SphericalHarmonicsL2[] _baked;
        private SphericalHarmonicsL2[] _lit;
        private SphericalHarmonicsL2[] _blackout;
        private float _reflectionIntensity;
        private bool _isBlackout;
        private float _nextCheck;
        private float _nextRescan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (LightmapSettings.lightProbes == null || LightmapSettings.lightProbes.count == 0) return;
            if (FindFirstObjectByType<BakedBounceLight>() != null) return;
            if (FindFirstObjectByType<LightSabotage>(FindObjectsInactive.Include) == null) return;

            var go = new GameObject(nameof(BakedBounceLight));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BakedBounceLight>();
        }

        private void Awake()
        {
            _baked = LightmapSettings.lightProbes.bakedProbes;
            _lit = new SphericalHarmonicsL2[_baked.Length];
            _blackout = new SphericalHarmonicsL2[_baked.Length];
            _reflectionIntensity = RenderSettings.reflectionIntensity;

            SphericalHarmonicsL2 fill = RenderSettings.ambientProbe * AmbientFill;

            for (int i = 0; i < _baked.Length; i++)
            {
                _lit[i] = _baked[i] + fill;
                _blackout[i] = _lit[i] * BlackoutScale;
            }

            LightmapSettings.lightProbes.bakedProbes = _lit;
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (now >= _nextRescan)
            {
                _nextRescan = now + RescanInterval;
                _lights.Clear();
                _lights.AddRange(FindObjectsByType<LightSabotage>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            }

            if (now < _nextCheck) return;

            _nextCheck = now + CheckInterval;

            bool lightsOut = AnyLightSabotaged();
            if (lightsOut == _isBlackout) return;

            _isBlackout = lightsOut;
            LightmapSettings.lightProbes.bakedProbes = lightsOut ? _blackout : _lit;
            RenderSettings.reflectionIntensity = _reflectionIntensity * (lightsOut ? BlackoutScale : 1f);
        }

        private bool AnyLightSabotaged()
        {
            foreach (LightSabotage light in _lights)
            {
                if (light != null && light.IsSabotaged) return true;
            }

            return false;
        }

        /// <summary>The probes outlive this object, and in the Editor they outlive Play.</summary>
        private void OnDestroy()
        {
            if (LightmapSettings.lightProbes != null) LightmapSettings.lightProbes.bakedProbes = _baked;

            RenderSettings.reflectionIntensity = _reflectionIntensity;
        }
    }
}
