using System.Collections;
using UnityEngine;

namespace Systems
{
    public class SceneTransition : MonoBehaviour
    {
        [SerializeField] private CanvasGroup fade;

        public IEnumerator FadeOut()
        {
            fade.gameObject.SetActive(true);

            float timer = 0;

            while (timer < 1)
            {
                timer += Time.deltaTime * 4;

                fade.alpha = timer;

                yield return null;
            }
        }
    
    }
}

