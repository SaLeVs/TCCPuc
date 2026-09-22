using Chat;
using UnityEngine;

namespace Components
{
    /// <summary>
    /// Drop-in "the player seems stuck" hint, wired entirely in the inspector.
    ///
    /// <para>Call <see cref="Notify"/> from whatever counts as progress - a UnityEvent on a door, a
    /// trigger volume, an animation event - and the chat stays quiet. Stop calling it and the chat
    /// starts helping, louder each time.</para>
    ///
    /// <para>This is the no-code path for hints a tutorial needs, like "the player has not moved in
    /// ten seconds". The hints the game ships with live inside the systems that already track the
    /// state they watch, using the same <see cref="ChatNudge"/> underneath.</para>
    /// </summary>
    public class ChatIdleWatcher : MonoBehaviour
    {
        [SerializeField] private ChatNudge nudge = new();

        [SerializeField]
        [Tooltip("Start the clock as soon as the object is enabled. Off waits for the first Notify.")]
        private bool startArmed = true;

        [SerializeField]
        [Tooltip("Optional. Pasted into any line using the {subject} placeholder.")]
        private string subject;


        private void OnEnable()
        {
            if (startArmed)
            {
                nudge.ReportProgress();
            }
            else
            {
                nudge.Disarm();
            }
        }

        /// <summary>Progress happened. Public so a UnityEvent or any script can reset the clock.</summary>
        public void Notify() => nudge.ReportProgress();

        /// <summary>Stops the hint for good - the step is done.</summary>
        public void Disarm() => nudge.Disarm();

        private void Update() => nudge.Tick(Time.deltaTime, subject);
    }
}
