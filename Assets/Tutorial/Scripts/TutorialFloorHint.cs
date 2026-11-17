using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Tutorial
{
    /// <summary>
    /// Marca de ajuda no chão (fita amarela, "ANDE ATÉ AQUI", o X de gravação...). Só serve enquanto
    /// a etapa dela não acabou: o diretor chama <see cref="Hide"/> e ela some com um fade curto.
    /// </summary>
    public class TutorialFloorHint : MonoBehaviour
    {
        [SerializeField] private string hintId;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;

        private static readonly Dictionary<string, List<TutorialFloorHint>> ById = new();

        public void Configure(string id) => hintId = id;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(hintId)) return;

            if (!ById.TryGetValue(hintId, out List<TutorialFloorHint> list)) ById[hintId] = list = new List<TutorialFloorHint>();
            if (!list.Contains(this)) list.Add(this);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(hintId) && ById.TryGetValue(hintId, out List<TutorialFloorHint> list)) list.Remove(this);
        }

        /// <summary>Some com as marcas de <paramref name="id"/>.</summary>
        public static void Hide(string id)
        {
            if (string.IsNullOrEmpty(id) || !ById.TryGetValue(id, out List<TutorialFloorHint> list)) return;

            foreach (TutorialFloorHint hint in list.ToArray())
            {
                if (hint != null && hint.isActiveAndEnabled) hint.StartCoroutine(hint.FadeOut());
            }
        }

        private IEnumerator FadeOut()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            Color[] textStart = new Color[texts.Length];
            for (int i = 0; i < texts.Length; i++) textStart[i] = texts[i].color;

            Vector3[] scaleStart = new Vector3[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) scaleStart[i] = renderers[i].transform.localScale;

            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                float alpha = 1f - t / fadeSeconds;

                for (int i = 0; i < texts.Length; i++)
                {
                    Color c = textStart[i];
                    c.a *= alpha;
                    texts[i].color = c;
                }

                // As fitas são Unlit opacas (sem transparência): encolhem até sumir.
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].GetComponent<TMP_Text>() != null) continue;

                    renderers[i].transform.localScale = scaleStart[i] * alpha;
                }

                yield return null;
            }

            gameObject.SetActive(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ById.Clear();
    }
}
