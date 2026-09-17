using Missions.Donations;
using UnityEngine;

namespace Ui
{
    /// <summary>
    /// The donation feed doesn't just move out of the middle of the screen, it also drops to a stub:
    /// while hidden every popup shows nothing but its expiration icon, so what parks under the
    /// missions panel is small enough to live there.
    /// </summary>
    public class DonationHudPanel : HudPanel
    {
        [Header("Compact mode")]
        [Tooltip("Feed that owns the popups. While this panel is hidden it is told to strip every " +
                 "popup down to the expiration icon.")]
        [SerializeField] private DonationUIController feed;

        private bool _isCompact;

        protected override void ApplyProgress(float hiddenAmount)
        {
            base.ApplyProgress(hiddenAmount);

            // Compacts as soon as it starts moving, so it shrinks on the way to the corner rather
            // than popping once it arrives.
            SetCompact(hiddenAmount > 0f);
        }

        private void SetCompact(bool compact)
        {
            if (_isCompact == compact) return;

            _isCompact = compact;

            if (feed != null) feed.SetCompact(compact);
        }
    }
}
