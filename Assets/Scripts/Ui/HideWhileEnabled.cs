using UnityEngine;

namespace Ui
{
    /// <summary>
    /// While this object is on, the given groups are invisible and ignore the mouse; when it goes
    /// off they come back. The pause options open over the pause menu instead of replacing it, so
    /// this keeps the pause backdrop (bands, scanlines, dim) and hides only what would be in the way.
    /// </summary>
    public class HideWhileEnabled : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] hiddenWhileEnabled;

        private void OnEnable()
        {
            SetVisible(false);
        }

        private void OnDisable()
        {
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (CanvasGroup group in hiddenWhileEnabled)
            {
                if (group == null) continue;

                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }
        }
    }
}
