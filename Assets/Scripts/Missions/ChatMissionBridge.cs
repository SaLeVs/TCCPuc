using Chat;
using Missions.Donations;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    /// <summary>
    /// Feeds donations and mission progress to the chat, and nags the player when that progress
    /// stops.
    ///
    /// <para>Same reason as the audience bridge: Missions already references Player, so the chat
    /// living in Player cannot reference Missions back without closing a cycle.</para>
    ///
    /// <para>Donations are read off the replicated list rather than DonationManager's own events.
    /// Those are raised from inside server-only paths, so subscribing to them would have produced
    /// chat on the host and silence on every other client. The NetworkList is the only version of
    /// this that every client actually sees.</para>
    /// </summary>
    public class ChatMissionBridge : MonoBehaviour
    {
        /// <summary>Donation size that counts as a full-blown reaction.</summary>
        private const float BigDonation = 100f;

        /// <summary>Seconds between attempts to find the manager. It only appears once a match starts.</summary>
        private const float BindRetryInterval = 1f;

        [SerializeField]
        [Tooltip("Nudges the player when no mission has been picked up or finished for a while. " +
                 "Reset by any mission event.")]
        private ChatNudge missionNudge = new(ChatTopics.HintMissionIdle, 90f, 60f, 0.3f, 0.7f);

        private DonationManager _manager;
        private float _bindTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject host = new GameObject(nameof(ChatMissionBridge));

            host.AddComponent<ChatMissionBridge>();

            DontDestroyOnLoad(host);
        }

        private void OnEnable()
        {
            // Both are static and raised behind an IsOwner guard, so they arrive exactly once, on
            // the client whose mission it was.
            PlayerMissionHolder.OnMissionCompletedSound += PlayerMissionHolder_OnMissionCompleted;
            PlayerMissionHolder.OnMissionRecievedSound += PlayerMissionHolder_OnMissionReceived;

            missionNudge.ReportProgress();
        }

        private void Update()
        {
            if (_manager == null)
            {
                // Unbound means no match is running. The nudge stays frozen rather than counting
                // down in the main menu and firing a "go find a mission" hint into an empty lobby.
                _bindTimer -= Time.deltaTime;

                if (_bindTimer <= 0f)
                {
                    _bindTimer = BindRetryInterval;
                    TryBind();
                }

                return;
            }

            missionNudge.Tick(Time.deltaTime);
        }

        private void TryBind()
        {
            DonationManager manager = DonationManager.Instance;

            if (manager == null) return;

            _manager = manager;
            _manager.NetworkStates.OnListChanged += NetworkStates_OnListChanged;

            // The clock starts when the match does, not when the process did.
            missionNudge.ReportProgress();
        }

        private void NetworkStates_OnListChanged(NetworkListEvent<DonationNetworkState> changeEvent)
        {
            switch (changeEvent.Type)
            {
                case NetworkListEvent<DonationNetworkState>.EventType.Add:
                case NetworkListEvent<DonationNetworkState>.EventType.Insert:
                    Announce(ChatTopics.DonationReceived, changeEvent.Value);
                    break;

                case NetworkListEvent<DonationNetworkState>.EventType.Value:
                    // Only the transition matters. The list also ticks progress through this same
                    // event, and reacting to every tick would bury the chat.
                    if (changeEvent.Value.State == changeEvent.PreviousValue.State) break;

                    if (changeEvent.Value.State == DonationState.Expired)
                    {
                        Announce(ChatTopics.DonationExpired, changeEvent.Value);
                    }

                    break;
            }
        }

        private static void Announce(string topicId, DonationNetworkState state)
        {
            ChatStimulusBus.Raise(topicId, Mathf.Clamp01(state.Amount / BigDonation),
                state.DonorName.ToString());
        }

        private void PlayerMissionHolder_OnMissionCompleted(Vector3 _)
        {
            missionNudge.ReportProgress();

            ChatStimulusBus.Raise(ChatTopics.MissionCompleted, 0.7f);
        }

        // Picking one up counts as progress too - the player is clearly not lost, so the hint
        // should not be counting down at them while they walk to it.
        private void PlayerMissionHolder_OnMissionReceived(Vector3 _) => missionNudge.ReportProgress();

        private void OnDisable()
        {
            PlayerMissionHolder.OnMissionCompletedSound -= PlayerMissionHolder_OnMissionCompleted;
            PlayerMissionHolder.OnMissionRecievedSound -= PlayerMissionHolder_OnMissionReceived;

            missionNudge.Disarm();

            if (_manager == null) return;

            _manager.NetworkStates.OnListChanged -= NetworkStates_OnListChanged;
            _manager = null;
        }
    }
}
