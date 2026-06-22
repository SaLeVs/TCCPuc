using System.Collections;
using UnityEngine;

namespace Systems
{
    public class SceneTransition : MonoBehaviour
    {
        [SerializeField] private CanvasGroup fade;

        [Header("Settings")]
        [SerializeField] private float timeToFade = 1f;
        [SerializeField] private bool wantToFadeIn = true;

        public float FadeDuration => timeToFade;
        private Coroutine _currentFade;

        
        private void Start()
        {
            if (wantToFadeIn)
            {
                PlayFadeIn();
            }
        }

        public void PlayFadeIn()
        {
            StartFade(FadeIn());
        }
        

        private void StartFade(IEnumerator routine)
        {
            if (_currentFade != null)
            {
                StopCoroutine(_currentFade);
            }

            _currentFade = StartCoroutine(routine);
        }

        public IEnumerator FadeOut()
        {
            fade.gameObject.SetActive(true);

            float timer = 0f;

            fade.alpha = 0f;

            while (timer < timeToFade)
            {
                timer += Time.deltaTime;

                fade.alpha = Mathf.Lerp(0f, 1f, timer / timeToFade);

                yield return null;
            }

            fade.alpha = 1f;

            _currentFade = null;
        }

        private IEnumerator FadeIn()
        {
            fade.gameObject.SetActive(true);

            float timer = 0f;
            fade.alpha = 1f;

            while (timer < timeToFade)
            {
                timer += Time.deltaTime;

                fade.alpha = Mathf.Lerp(1f, 0f, timer / timeToFade);

                yield return null;
            }

            fade.alpha = 0f;

            fade.gameObject.SetActive(false);

            _currentFade = null;
        }
    }
}