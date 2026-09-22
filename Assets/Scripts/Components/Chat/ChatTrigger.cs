using Chat;
using UnityEngine;
using UnityEngine.Events;

namespace Components
{
    /// <summary>
    /// Makes the chat say something, wired entirely in the inspector.
    ///
    /// <para>This is the no-code path into the chat. Drop it on a trigger volume at the top of the
    /// tutorial, set the topic id, and the chat greets the player - no script, no reference, no
    /// assembly to touch. The same component covers scripted beats, one-off warnings and anything
    /// else that wants a line without earning a system of its own.</para>
    ///
    /// <para>The topic id has to exist in the topic database or nothing is said; the database is
    /// also where the lines, the volume and the mood for that topic live.</para>
    /// </summary>
    public class ChatTrigger : MonoBehaviour
    {
        public enum RaiseMode
        {
            /// <summary>Only when something calls Raise - a UnityEvent, an animation event, code.</summary>
            Manual,

            /// <summary>As soon as the object becomes active.</summary>
            OnEnable,

            /// <summary>When something on the matching layers enters the trigger.</summary>
            OnTriggerEnter
        }

        [SerializeField]
        [Tooltip("Topic id to raise. Must match a row in the topic database. Built-in ids look " +
                 "like hint.door_locked; a new one can be anything, for example tutorial.welcome.")]
        private string topicId;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How big an example of this topic it is. 0.5 is nominal - the topic database " +
                 "holds the actual ceiling.")]
        private float intensity = 0.5f;

        [SerializeField]
        [Tooltip("Optional. Pasted into any line that uses the {subject} placeholder.")]
        private string subject;

        [SerializeField] private RaiseMode raiseOn = RaiseMode.Manual;

        [SerializeField]
        [Tooltip("Layers that set this off in OnTriggerEnter mode. Leave as Everything to accept any collider.")]
        private LayerMask triggerLayers = ~0;

        [SerializeField]
        [Tooltip("Fire at most once for the lifetime of this object.")]
        private bool once = true;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds before this can fire again. Ignored when Once is on.")]
        private float cooldown = 0f;

        [SerializeField]
        [Tooltip("Runs alongside the chat line, for anything else this moment should set off.")]
        private UnityEvent onRaised;

        private bool _fired;
        private float _nextAllowedTime;


        private void OnEnable()
        {
            if (raiseOn == RaiseMode.OnEnable) Raise();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (raiseOn != RaiseMode.OnTriggerEnter) return;

            if ((triggerLayers.value & (1 << other.gameObject.layer)) == 0) return;

            Raise();
        }

        /// <summary>Public so a UnityEvent, an animation event or any script can set it off.</summary>
        public void Raise()
        {
            if (once && _fired) return;
            if (Time.time < _nextAllowedTime) return;

            if (string.IsNullOrWhiteSpace(topicId))
            {
                Debug.LogWarning($"{nameof(ChatTrigger)} on {name} has no topic id, so it says nothing.", this);
                return;
            }

            _fired = true;
            _nextAllowedTime = Time.time + cooldown;

            ChatStimulusBus.Raise(topicId, intensity, subject);

            onRaised?.Invoke();
        }

        /// <summary>Lets a once-only trigger be used again, for a retried tutorial step.</summary>
        public void Rearm()
        {
            _fired = false;
            _nextAllowedTime = 0f;
        }
    }
}
