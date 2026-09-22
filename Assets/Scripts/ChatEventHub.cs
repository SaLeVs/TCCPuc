using Audience;
using Chat;
using Missions;
using Missions.Donations;
using Monster;
using Monster.MonsterSabotages;
using Objects;
using Player.Chat;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The one place where the game tells the chat that something happened.
///
/// <para>Every system that the chat reacts to is listened to from here and translated into a topic
/// on the <see cref="ChatStimulusBus"/>. Nothing in this file knows what the chat will actually
/// say - the lines, the volume, the mood and the priority all live in the topic database, and the
/// only thing crossing over is a string id.</para>
///
/// <para>It lives in the Game assembly for the same reason <see cref="SfxManager"/> does: Game sits
/// at the top of the dependency graph, referencing nearly everything while nothing references it,
/// so it is the only place a listener can see Audience, Missions, Monster and Objects at once. The
/// chat itself lives in Player, which Missions already references - so the chat can never reach
/// back the other way without closing a cycle. The bus is what keeps that arrow pointing one way.
/// </para>
///
/// <para>Lives as an object in the match scene, not as something spawned behind the scenes. That
/// keeps it visible in the hierarchy, breakpoint, and - because its hint timings are serialized
/// fields - tunable in the inspector like everything else. It also means it exists exactly while a
/// match does, so none of its clocks can run in the main menu.</para>
///
/// <para>Each source below still waits for what it listens to: the managers spawn through netcode
/// a moment after the scene loads, not with it.</para>
/// </summary>
public class ChatEventHub : MonoBehaviour
{
    // Sampling and retry rates. Constants on purpose: they trade CPU against how quickly the chat
    // notices a change, which is not a decision anyone tuning how the chat feels would ever make.
    // The values people do tune - how big something has to be before the chat cares - are fields.

    /// <summary>The bar moves constantly; the chat only needs a coarse reading of it.</summary>
    private const float AUDIENCE_REPORT_INTERVAL = 0.25f;

    /// <summary>Re-resolving the player object costs a lookup; it does not change often.</summary>
    private const float PLAYER_REFRESH_INTERVAL = 2f;

    private const float SABOTAGE_POLL_INTERVAL = 0.5f;

    /// <summary>Slower cadence while no match is running and there is nothing to find.</summary>
    private const float UNBOUND_SABOTAGE_POLL_INTERVAL = 2f;

    /// <summary>Seconds between attempts to find a manager. They only appear once a match starts.</summary>
    private const float BIND_RETRY_INTERVAL = 1f;


    [Header("Hints")]
    [SerializeField]
    [Tooltip("Nudges the player when no mission has been picked up or finished for a while. " +
             "Reset by any mission event.")]
    private ChatNudge missionNudge = new(ChatTopics.HintMissionIdle, 90f, 60f);

    [SerializeField]
    [Tooltip("Keeps reminding the player where the lights come back on. Armed while the lights " +
             "are out, reset the moment they come back.")]
    private ChatNudge lightsNudge = new(ChatTopics.HintLightsOut, 12f, 25f);

    [SerializeField]
    [Tooltip("Nudges the player to go look at something new. Reset whenever the chat reacts to a " +
             "target it has not covered recently.")]
    private ChatNudge explorationNudge = new(ChatTopics.HintExplorationIdle, 75f, 50f);

    // A [Header] has to sit on a field with no Min/Range, or Odin draws the title twice. Bounds are
    // enforced in OnValidate, which also keeps the two audience thresholds from crossing.

    [Header("Thresholds")]
    [SerializeField]
    [Tooltip("Viewers gained or lost in one event before the chat bothers to mention it")]
    private float noticeableAudienceChange = 12f;

    [SerializeField]
    [Tooltip("Audience change that counts as a full-blown reaction. Anything at or above this " +
             "lands on the topic's maximum intensity")]
    private float bigAudienceChange = 60f;

    [SerializeField]
    [Tooltip("Donation amount that counts as a full-blown reaction. Match it to the values in " +
             "your donation definitions")]
    private float bigDonation = 100f;

    [SerializeField]
    [Tooltip("How close a rattling door has to be to the local player to be worth commenting on")]
    private float doorCommentRange = 14f;

    private AudienceManager _audience;
    private DonationManager _donations;
    private MonsterSabotage _sabotage;
    private ChatManager _chat;
    private Transform _localPlayer;

    private float _bindTimer;
    private float _audienceReportTimer;
    private float _sabotageTimer;
    private float _nextPlayerRefresh;
    private bool _lightsOut;


    /// <summary>
    /// Keeps the thresholds usable and stops the two audience ones from crossing. With a big change
    /// below the noticeable one, every reported change would land on maximum intensity; with a zero
    /// donation, the division that scales it would come back as infinity.
    /// </summary>
    private void OnValidate()
    {
        noticeableAudienceChange = Mathf.Max(0f, noticeableAudienceChange);
        bigAudienceChange = Mathf.Max(noticeableAudienceChange + 1f, bigAudienceChange);

        bigDonation = Mathf.Max(1f, bigDonation);
        doorCommentRange = Mathf.Max(0f, doorCommentRange);
    }

    private void OnEnable()
    {
        // All three are static and already client-side, so they need no manager to be found first.
        // The two mission ones are raised behind an IsOwner guard, so they arrive exactly once, on
        // the client whose mission it was.
        PlayerMissionHolder.OnMissionCompletedSound += PlayerMissionHolder_OnMissionCompleted;
        PlayerMissionHolder.OnMissionRecievedSound += PlayerMissionHolder_OnMissionReceived;
        Door.OnDoorBlockedSound += Door_OnBlocked;
        missionNudge.ReportProgress();
    }

    private void OnDisable()
    {
        PlayerMissionHolder.OnMissionCompletedSound -= PlayerMissionHolder_OnMissionCompleted;
        PlayerMissionHolder.OnMissionRecievedSound -= PlayerMissionHolder_OnMissionReceived;
        Door.OnDoorBlockedSound -= Door_OnBlocked;

        UnbindAudience();

        if (_donations != null)
        {
            _donations.NetworkStates.OnListChanged -= NetworkStates_OnListChanged;
            _donations = null;
        }

        if (_chat != null)
        {
            _chat.OnExploredSomethingNew -= Chat_OnExploredSomethingNew;
            _chat = null;
        }

        missionNudge.Disarm();
        lightsNudge.Disarm();
        explorationNudge.Disarm();

        _sabotage = null;
        _lightsOut = false;

        // The bus keeps whatever was last reported, and leaving the match should not leave the chat
        // believing a full house is still watching.
        ChatStimulusBus.ReportAudience(0f, false);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        TickBinding(deltaTime);
        TickAudience(deltaTime);
        TickMissions(deltaTime);
        TickExploration(deltaTime);
        TickLights(deltaTime);
    }

    /// <summary>Hunts for the managers that only exist once a match is running.</summary>
    private void TickBinding(float deltaTime)
    {
        if (_audience != null && _donations != null && _chat != null) return;

        _bindTimer -= deltaTime;

        if (_bindTimer > 0f) return;

        _bindTimer = BIND_RETRY_INTERVAL;

        if (_audience == null) TryBindAudience();
        if (_donations == null) TryBindDonations();
        if (_chat == null) TryBindChat();
    }

    /// <summary>
    /// Hooks the local player's chat so the exploration hint can live here with the other two.
    ///
    /// <para>The chat itself only reports that the player looked at something new; deciding to nag
    /// about it is a hint decision, and all three hints belong in one inspector.</para>
    /// </summary>
    private void TryBindChat()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || manager.SpawnManager == null) return;

        NetworkObject playerObject = manager.SpawnManager.GetPlayerNetworkObject(manager.LocalClientId);

        if (playerObject == null) return;

        _chat = playerObject.GetComponentInChildren<ChatManager>(true);

        if (_chat == null) return;

        _chat.OnExploredSomethingNew += Chat_OnExploredSomethingNew;

        explorationNudge.ReportProgress();
    }

    private void Chat_OnExploredSomethingNew() => explorationNudge.ReportProgress();


    // ------------------------------------------------------------------ Audience
    //
    // The headcount matters more than anything else here: the chat paces itself off it directly,
    // so this is what makes ten viewers produce a trickle and a full house produce a wall.
    // CurrentAudience is the same number the HUD prints, which keeps the bar and the chat telling
    // the same story.

    private void TickAudience(float deltaTime)
    {
        if (_audience == null)
        {
            // No match running. Report an empty room so the chat falls silent between matches
            // instead of pacing itself off whatever the last one ended on.
            ChatStimulusBus.ReportAudience(0f, false);
            return;
        }

        _audienceReportTimer -= deltaTime;

        if (_audienceReportTimer > 0f) return;

        _audienceReportTimer = AUDIENCE_REPORT_INTERVAL;

        ChatStimulusBus.ReportAudience(_audience.CurrentAudience, _audience.IsDecaying);
    }

    private void TryBindAudience()
    {
        AudienceManager manager = AudienceManager.Instance;

        if (manager == null) return;

        _audience = manager;

        _audience.OnAudienceGained += Audience_OnGained;
        _audience.OnAudienceLost += Audience_OnLost;
    }

    private void UnbindAudience()
    {
        if (_audience == null) return;

        _audience.OnAudienceGained -= Audience_OnGained;
        _audience.OnAudienceLost -= Audience_OnLost;
        _audience = null;
    }

    private void Audience_OnGained(float delta) => ReportAudienceChange(ChatTopics.AudienceSurge, delta);

    private void Audience_OnLost(float delta) => ReportAudienceChange(ChatTopics.AudienceDrop, delta);

    private void ReportAudienceChange(string topicId, float delta)
    {
        float amount = Mathf.Abs(delta);

        if (amount < noticeableAudienceChange) return;

        ChatStimulusBus.Raise(topicId, Mathf.Clamp01(amount / bigAudienceChange));
    }


    // ------------------------------------------------------------------ Donations and missions
    //
    // Donations are read off the replicated list rather than DonationManager's own events. Those
    // are raised from inside server-only paths, so subscribing to them would have produced chat on
    // the host and silence on every other client. The NetworkList is the only version of this that
    // every client actually sees.

    /// <summary>Only counts down while a chat is actually listening.</summary>
    private void TickExploration(float deltaTime)
    {
        if (_chat == null) return;

        explorationNudge.Tick(deltaTime);
    }

    private void TickMissions(float deltaTime)
    {
        // Unbound means no match is running. The nudge stays frozen rather than counting down in
        // the main menu and firing a "go find a mission" hint into an empty lobby.
        if (_donations == null) return;

        missionNudge.Tick(deltaTime);
    }

    private void TryBindDonations()
    {
        DonationManager manager = DonationManager.Instance;

        if (manager == null) return;

        _donations = manager;
        _donations.NetworkStates.OnListChanged += NetworkStates_OnListChanged;

        // The clock starts when the match does, not when the process did.
        missionNudge.ReportProgress();
    }

    private void NetworkStates_OnListChanged(NetworkListEvent<DonationNetworkState> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<DonationNetworkState>.EventType.Add:
            case NetworkListEvent<DonationNetworkState>.EventType.Insert:
                AnnounceDonation(ChatTopics.DonationReceived, changeEvent.Value);
                break;

            case NetworkListEvent<DonationNetworkState>.EventType.Value:
                // Only the transition matters. The list also ticks progress through this same
                // event, and reacting to every tick would bury the chat.
                if (changeEvent.Value.State == changeEvent.PreviousValue.State) break;

                if (changeEvent.Value.State == DonationState.Expired)
                {
                    AnnounceDonation(ChatTopics.DonationExpired, changeEvent.Value);
                }

                break;
        }
    }

    private void AnnounceDonation(string topicId, DonationNetworkState state)
    {
        ChatStimulusBus.Raise(topicId, Mathf.Clamp01(state.Amount / bigDonation),
            state.DonorName.ToString());
    }

    private void PlayerMissionHolder_OnMissionCompleted(Vector3 _)
    {
        missionNudge.ReportProgress();

        ChatStimulusBus.Raise(ChatTopics.MissionCompleted, 0.7f);
    }

    // Picking one up counts as progress too - the player is clearly not lost, so the hint should
    // not be counting down at them while they walk to it.
    private void PlayerMissionHolder_OnMissionReceived(Vector3 _) => missionNudge.ReportProgress();


    // ------------------------------------------------------------------ Doors
    //
    // Hooks the blocked-door sound rather than the interaction, because that sound is already
    // raised on every client through an RPC - the one version of the event reachable from here
    // without the door needing to know who pressed the key.
    //
    // Filtered by distance, not by who interacted, and deliberately so: viewers are watching a
    // screen. A door rattling in front of the streamer is worth a comment whoever pulled on it,
    // and one rattling across the map is not.

    private void Door_OnBlocked(Vector3 position)
    {
        Transform player = ResolveLocalPlayer();

        if (player == null) return;

        float distance = Vector3.Distance(player.position, position);

        if (distance > doorCommentRange) return;

        // Closer means more certainly the streamer's own problem, so chat is more sure of itself.
        float intensity = Mathf.Lerp(0.6f, 0.35f, Mathf.Clamp01(distance / doorCommentRange));

        ChatStimulusBus.Raise(ChatTopics.HintDoorLocked, intensity);
    }

    private Transform ResolveLocalPlayer()
    {
        if (_localPlayer != null && Time.time < _nextPlayerRefresh) return _localPlayer;

        _nextPlayerRefresh = Time.time + PLAYER_REFRESH_INTERVAL;

        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || manager.SpawnManager == null) return null;

        NetworkObject playerObject = manager.SpawnManager.GetPlayerNetworkObject(manager.LocalClientId);

        _localPlayer = playerObject == null ? null : playerObject.transform;

        return _localPlayer;
    }


    // ------------------------------------------------------------------ Lights
    //
    // Polls instead of subscribing, because there is no client-side event to subscribe to. The
    // sabotage replicates through RPCs that return early on the server, so no single callback fires
    // on both the host and the clients - but the sabotaged flag itself ends up correct on every
    // peer. Reading it a couple of times a second gets the transition on all of them with no change
    // to the sabotage system at all.

    private void TickLights(float deltaTime)
    {
        // Only counts down while the player actually has a problem, so the hint cannot fire during
        // a perfectly lit match.
        if (_lightsOut)
        {
            lightsNudge.Tick(deltaTime);
        }

        _sabotageTimer -= deltaTime;

        if (_sabotageTimer > 0f) return;

        _sabotageTimer = SABOTAGE_POLL_INTERVAL;

        PollLights();
    }

    private void PollLights()
    {
        if (_sabotage == null)
        {
            _lightsOut = false;

            // Outside a match there is nothing to find, and this runs in every scene. Back off so
            // an idle main menu is not paying for a scene-wide search twice a second.
            _sabotageTimer = UNBOUND_SABOTAGE_POLL_INTERVAL;

            _sabotage = FindFirstObjectByType<MonsterSabotage>();

            if (_sabotage == null) return;
        }

        bool lightsOut = _sabotage.HasSabotagedOfType(SabotageType.Light);

        if (lightsOut == _lightsOut) return;

        _lightsOut = lightsOut;

        if (lightsOut)
        {
            // Two separate things, on purpose. The reaction is chat losing it the instant the room
            // goes dark; the hint is chat remembering there is a generator, and it only shows up
            // once the player has had a moment to work it out for themselves.
            ChatStimulusBus.Raise(ChatTopics.LightsOut, 0.9f);

            lightsNudge.ReportProgress();
            return;
        }

        lightsNudge.Disarm();

        ChatStimulusBus.Raise(ChatTopics.LightsRestored);
    }
}
