using System;
using UnityEngine;

namespace Chat
{
    /// <summary>
    /// A clock that makes the chat help when nothing has happened for too long.
    ///
    /// <para>Every "the player seems stuck" hint has the same shape: something resets a timer, and
    /// when it runs out the chat says something. This holds that shape in one place so the
    /// exploration hint, the mission hint and anything a tutorial adds later behave identically and
    /// are tuned the same way.</para>
    ///
    /// <para>Deliberately only four knobs. An earlier version escalated the intensity with each
    /// unanswered nudge, across four more fields - but intensity only changes how fast the chat
    /// talks afterwards, never what it says, so the whole thing bought a difference nobody could
    /// see. A hint that should get blunter earns a second topic with blunter lines, which is data
    /// rather than dials.</para>
    ///
    /// <para>Serializable rather than a component so it can sit as a field on whatever already owns
    /// the state that resets it, instead of forcing that state to be exposed.</para>
    /// </summary>
    [Serializable]
    public class ChatNudge
    {
        [SerializeField]
        [Tooltip("Topic id raised when the player has been stuck too long")]
        private string topicId;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds of no progress before the first nudge")]
        private float idleSeconds = 60f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds between repeats while the player is still stuck. 0 nudges once and then " +
                 "waits for progress. This is the throttle for a hint - the topic's own cooldown is " +
                 "only a safety net for topics raised from several places at once.")]
        private float repeatSeconds = 45f;

        /// <summary>
        /// Nominal. Raising at 0.5 lands exactly on whatever the topic entry's own intensity is
        /// set to, which is why there is no intensity field here: a second number would only have
        /// multiplied into the first one, and "how big a deal is this" already has an owner.
        /// </summary>
        private const float NominalIntensity = 0.5f;

        private bool _armed = true;
        private float _timer;
        private int _nudges;

        /// <summary>Required by Unity serialization, and by anything filling this in the inspector.</summary>
        public ChatNudge()
        {
        }

        /// <summary>
        /// Preset for a nudge that has no inspector to be tuned in - the bridges create themselves
        /// at runtime, so their timings have to come from code. A component that a person places in
        /// a scene should use the empty constructor and be filled in the inspector instead.
        /// </summary>
        public ChatNudge(string topicId, float idleSeconds, float repeatSeconds)
        {
            this.topicId = topicId;
            this.idleSeconds = Mathf.Max(1f, idleSeconds);
            this.repeatSeconds = Mathf.Max(0f, repeatSeconds);
        }

        /// <summary>How many nudges have gone unanswered. Zero means the player is doing fine.</summary>
        public int UnansweredNudges => _nudges;

        public bool HasTopic => !string.IsNullOrWhiteSpace(topicId);

        /// <summary>
        /// Progress happened. Resets the clock and the escalation, so the next stall starts gentle
        /// again rather than picking up where the last one left off.
        /// </summary>
        public void ReportProgress()
        {
            _armed = true;
            _timer = 0f;
            _nudges = 0;
        }

        /// <summary>Stops nudging until <see cref="ReportProgress"/> starts the clock again.</summary>
        public void Disarm()
        {
            _armed = false;
            _timer = 0f;
            _nudges = 0;
        }

        /// <summary>Raises the topic when the clock runs out. Call once a frame.</summary>
        public void Tick(float deltaTime, string subject = null)
        {
            if (!_armed || !HasTopic) return;

            // repeatSeconds of 0 means one nudge, then silence until progress happens.
            if (_nudges > 0 && repeatSeconds <= 0f) return;

            _timer += deltaTime;

            float threshold = _nudges == 0 ? idleSeconds : repeatSeconds;

            if (_timer < threshold) return;

            _timer = 0f;

            // Nominal intensity on purpose: the topic entry already says how big a deal it is, and
            // a second number here only multiplied into the first one.
            ChatStimulusBus.Raise(topicId, NominalIntensity, subject);

            _nudges++;
        }
    }
}
