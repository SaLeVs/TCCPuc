using System;
using UnityEngine;

namespace Chat
{
    /// <summary>
    /// A clock that makes the chat help when nothing has happened for too long.
    ///
    /// <para>Every "the player seems stuck" hint has the same shape: something resets a timer, and
    /// when it runs out the chat says something - louder each time it goes unanswered. This holds
    /// that shape in one place so the exploration hint, the mission hint and anything a tutorial
    /// adds later behave identically and are tuned the same way.</para>
    ///
    /// <para>Serializable rather than a component so it can sit as a field on whatever already owns
    /// the state that resets it, instead of forcing that state to be exposed.</para>
    /// </summary>
    [Serializable]
    public class ChatNudge
    {
        [SerializeField]
        [Tooltip("Topic id raised when the player has been stuck too long.")]
        private string topicId;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds of no progress before the first nudge.")]
        private float idleSeconds = 60f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds between repeats while the player is still stuck. 0 nudges once and then " +
                 "waits for progress.")]
        private float repeatSeconds = 45f;

        [SerializeField]
        [Tooltip("Push harder the longer it goes unanswered. The first nudge is a passing remark, " +
                 "the third is chat losing patience - which is also what makes it read as people " +
                 "rather than as a hint system.")]
        private bool escalate = true;

        [SerializeField, Range(0f, 1f)] private float startIntensity = 0.3f;
        [SerializeField, Range(0f, 1f)] private float maxIntensity = 0.7f;

        [SerializeField, Min(1)]
        [Tooltip("Unanswered nudges it takes to reach the maximum intensity.")]
        private int escalationSteps = 3;

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
        public ChatNudge(string topicId, float idleSeconds, float repeatSeconds,
            float startIntensity = 0.3f, float maxIntensity = 0.7f)
        {
            this.topicId = topicId;
            this.idleSeconds = Mathf.Max(1f, idleSeconds);
            this.repeatSeconds = Mathf.Max(0f, repeatSeconds);
            this.startIntensity = Mathf.Clamp01(startIntensity);
            this.maxIntensity = Mathf.Clamp01(maxIntensity);
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

            ChatStimulusBus.Raise(topicId, CurrentIntensity(), subject);

            _nudges++;
        }

        private float CurrentIntensity()
        {
            if (!escalate) return startIntensity;

            return Mathf.Lerp(startIntensity, maxIntensity, Mathf.Clamp01(_nudges / (float)escalationSteps));
        }
    }
}
