using System.Collections;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// A full-screen black curtain drawn over everything (IMGUI, so it sits above every canvas and
    /// needs nothing in the scene). One per scene: it dies with the scene that created it.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        private static Texture2D _black;

        private float _alpha;
        private Coroutine _running;

        public float Alpha => _alpha;

        /// <summary>A curtain in the active scene, starting at <paramref name="startAlpha"/>.</summary>
        public static ScreenFade Create(float startAlpha)
        {
            GameObject go = new GameObject("ScreenFade");
            ScreenFade fade = go.AddComponent<ScreenFade>();
            fade._alpha = Mathf.Clamp01(startAlpha);
            return fade;
        }

        public void FadeTo(float target, float duration, float delay = 0f)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(FadeRoutine(Mathf.Clamp01(target), duration, delay));
        }

        private IEnumerator FadeRoutine(float target, float duration, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            float from = _alpha;
            float time = 0f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                _alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, time / duration));
                yield return null;
            }

            _alpha = target;
            _running = null;
        }

        private void OnGUI()
        {
            if (_alpha <= 0.001f) return;

            if (_black == null)
            {
                _black = new Texture2D(1, 1);
                _black.SetPixel(0, 0, Color.black);
                _black.Apply();
            }

            GUI.depth = -10000;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);
            GUI.color = previous;
        }
    }
}
