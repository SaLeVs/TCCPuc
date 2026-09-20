using UnityEngine;

namespace Chat
{
    /// <summary>
    /// The topic ids the game ships with.
    ///
    /// <para>These are plain strings on purpose. An enum would mean that adding a tutorial step, a
    /// new hint or a one-off scripted beat requires editing this assembly and recompiling, and that
    /// every system wanting to talk to the chat has to reference whatever declares the enum. A
    /// string costs nothing, travels anywhere, and a new topic is a new row in the topic database
    /// plus the lines to go with it - no code at all.</para>
    ///
    /// <para>These constants exist only so the built-in topics are not spelled by hand in five
    /// places. Anything may raise an id that is not listed here.</para>
    /// </summary>
    public static class ChatTopics
    {
        // --- Reactions: something happened ---
        public const string PlayerHurt = "player.hurt";
        public const string PlayerDied = "player.died";
        public const string TeammateDied = "player.teammate_died";
        public const string DonationReceived = "donation.received";
        public const string DonationExpired = "donation.expired";
        public const string MissionCompleted = "mission.completed";
        public const string AudienceSurge = "audience.surge";
        public const string AudienceDrop = "audience.drop";
        public const string LightsRestored = "lights.restored";

        // --- Hints: chat trying to help ---
        public const string HintDoorLocked = "hint.door_locked";
        public const string HintLightsOut = "hint.lights_out";
        public const string HintMissionIdle = "hint.mission_idle";
        public const string HintExplorationIdle = "hint.exploration_idle";
    }

    /// <summary>
    /// One thing worth reacting to. Deliberately a value type with no references into the system
    /// that raised it: the chat must not care whether a donation came from the donation manager or
    /// from a debug key, only that one landed and how big a deal it was.
    /// </summary>
    public readonly struct ChatStimulus
    {
        /// <summary>A <see cref="ChatTopics"/> constant, or any id the topic database knows.</summary>
        public readonly string TopicId;

        /// <summary>
        /// 0..1, where 0.5 is a nominal example of this topic. The topic database sets the ceiling
        /// for the topic; this says how big this particular instance was.
        /// </summary>
        public readonly float Intensity;

        /// <summary>
        /// Who or what this is about - a donor's name, the room to go to. Chat lines paste it in
        /// with the {subject} placeholder. Empty when the topic has no subject.
        /// </summary>
        public readonly string Subject;

        public ChatStimulus(string topicId, float intensity = 0.5f, string subject = null)
        {
            TopicId = topicId;
            Intensity = Mathf.Clamp01(intensity);
            Subject = subject ?? string.Empty;
        }
    }

    /// <summary>
    /// Neutral hand-off point between the systems that know something happened and the chat that
    /// reacts to it.
    ///
    /// <para>This exists because of an assembly cycle, not for elegance alone: Missions already
    /// references Player, so the chat living in Player can never reference Missions back. Both
    /// sides can see Components, so the signal passes through here instead. It also means a system
    /// can feed the chat without taking on any knowledge of what chat is - one static call, no
    /// reference, no interface to implement.</para>
    /// </summary>
    public static class ChatStimulusBus
    {
        public static event System.Action<ChatStimulus> OnStimulus;

        /// <summary>
        /// Viewers watching right now, as an absolute count rather than a fraction.
        ///
        /// <para>The chat is paced off this directly: ten people watching cannot produce the same
        /// wall of text as a thousand, and a fraction of a maximum loses exactly the information
        /// needed to tell those apart.</para>
        /// </summary>
        public static float ViewerCount { get; private set; }

        /// <summary>Audience is bleeding off because nothing interesting is happening.</summary>
        public static bool AudienceDecaying { get; private set; }

        public static void Raise(in ChatStimulus stimulus)
        {
            if (string.IsNullOrWhiteSpace(stimulus.TopicId)) return;

            OnStimulus?.Invoke(stimulus);
        }

        public static void Raise(string topicId, float intensity = 0.5f, string subject = null)
        {
            Raise(new ChatStimulus(topicId, intensity, subject));
        }

        public static void ReportAudience(float viewerCount, bool decaying)
        {
            ViewerCount = Mathf.Max(0f, viewerCount);
            AudienceDecaying = decaying;
        }

        /// <summary>
        /// Static events outlive a play session when the editor is set to enter play mode without a
        /// domain reload, which leaves the previous run's listeners subscribed and firing into dead
        /// objects. Clearing on subsystem registration puts the bus back to empty every run.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            OnStimulus = null;
            ViewerCount = 0f;
            AudienceDecaying = false;
        }
    }
}
