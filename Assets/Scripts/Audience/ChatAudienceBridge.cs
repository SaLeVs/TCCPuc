using Chat;
using UnityEngine;

namespace Audience
{
    /// <summary>
    /// Feeds the audience to the chat: how many people are watching, and when that number moves.
    ///
    /// <para>The headcount matters more than anything else here. The chat paces itself off it
    /// directly, so this is what makes ten viewers produce a trickle and a full house produce a
    /// wall. CurrentAudience is the same number the HUD prints, which keeps what the player reads
    /// on the bar and what they read in the chat telling the same story.</para>
    ///
    /// <para>It exists as a separate object rather than as a reference inside the chat because of an
    /// assembly cycle: Audience references Missions, Missions references Player, and the chat lives
    /// in Player. Pushing through <see cref="ChatStimulusBus"/> keeps the dependency pointing one
    /// way. Creates itself at startup, so nothing has to be wired in a scene.</para>
    /// </summary>
    public class ChatAudienceBridge : MonoBehaviour
    {
        /// <summary>The bar moves constantly; the chat only needs a coarse reading of it.</summary>
        private const float ReportInterval = 0.25f;

        /// <summary>Viewers gained or lost in one event before chat bothers to mention it.</summary>
        private const float NoticeableChange = 12f;

        /// <summary>Change that counts as a full-blown reaction.</summary>
        private const float BigChange = 60f;

        /// <summary>Seconds between attempts to find the manager. It only appears once a match starts.</summary>
        private const float BindRetryInterval = 1f;

        private AudienceManager _manager;
        private float _timer;
        private float _bindTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject host = new GameObject(nameof(ChatAudienceBridge));

            host.AddComponent<ChatAudienceBridge>();

            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            if (_manager == null)
            {
                // No match running. Report an empty room so the chat falls silent between matches
                // instead of pacing itself off whatever the last one ended on.
                ChatStimulusBus.ReportAudience(0f, false);

                _bindTimer -= Time.deltaTime;

                if (_bindTimer <= 0f)
                {
                    _bindTimer = BindRetryInterval;
                    TryBind();
                }

                return;
            }

            _timer -= Time.deltaTime;

            if (_timer > 0f) return;

            _timer = ReportInterval;

            ChatStimulusBus.ReportAudience(_manager.CurrentAudience, _manager.IsDecaying);
        }

        private void TryBind()
        {
            AudienceManager manager = AudienceManager.Instance;

            if (manager == null) return;

            _manager = manager;

            _manager.OnAudienceGained += Manager_OnAudienceGained;
            _manager.OnAudienceLost += Manager_OnAudienceLost;
        }

        private void Manager_OnAudienceGained(float delta) => Report(ChatTopics.AudienceSurge, delta);

        private void Manager_OnAudienceLost(float delta) => Report(ChatTopics.AudienceDrop, delta);

        private static void Report(string topicId, float delta)
        {
            float amount = Mathf.Abs(delta);

            if (amount < NoticeableChange) return;

            ChatStimulusBus.Raise(topicId, Mathf.Clamp01(amount / BigChange));
        }

        private void OnDestroy()
        {
            if (_manager == null) return;

            _manager.OnAudienceGained -= Manager_OnAudienceGained;
            _manager.OnAudienceLost -= Manager_OnAudienceLost;
        }
    }
}
