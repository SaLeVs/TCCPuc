using Chat;
using Monster.MonsterSabotages;
using UnityEngine;

namespace Monster
{
    /// <summary>
    /// Makes the chat notice the lights going out, and keep telling the player about the generator
    /// until they do something about it.
    ///
    /// <para>Polls instead of subscribing, because there is no client-side event to subscribe to.
    /// The sabotage replicates through RPCs that return early on the server, so no single callback
    /// fires on both the host and the clients - but the sabotaged flag itself ends up correct on
    /// every peer. Reading it a couple of times a second gets the transition on all of them with no
    /// change to the sabotage system at all.</para>
    /// </summary>
    public class ChatSabotageBridge : MonoBehaviour
    {
        private const float PollInterval = 0.5f;

        /// <summary>Slower cadence while no match is running and there is nothing to find.</summary>
        private const float UnboundPollInterval = 2f;

        [SerializeField]
        [Tooltip("Keeps reminding the player where the lights come back on. Armed while the lights " +
                 "are out, reset the moment they come back.")]
        private ChatNudge lightsNudge = new(ChatTopics.HintLightsOut, 12f, 25f, 0.4f, 0.85f);

        private MonsterSabotage _sabotage;
        private float _timer;
        private bool _lightsOut;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject host = new GameObject(nameof(ChatSabotageBridge));

            host.AddComponent<ChatSabotageBridge>();

            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            // Only counts down while the player actually has a problem, so the hint cannot fire
            // during a perfectly lit match.
            if (_lightsOut)
            {
                lightsNudge.Tick(Time.deltaTime);
            }

            _timer -= Time.deltaTime;

            if (_timer > 0f) return;

            _timer = PollInterval;

            Poll();
        }

        private void Poll()
        {
            if (_sabotage == null)
            {
                _lightsOut = false;

                // Outside a match there is nothing to find, and this runs in every scene. Back off
                // so an idle main menu is not paying for a scene-wide search twice a second.
                _timer = UnboundPollInterval;

                _sabotage = FindFirstObjectByType<MonsterSabotage>();

                if (_sabotage == null) return;
            }

            bool lightsOut = _sabotage.HasSabotagedOfType(SabotageType.Light);

            if (lightsOut == _lightsOut) return;

            _lightsOut = lightsOut;

            if (lightsOut)
            {
                // Starts the clock rather than talking immediately: the player deserves a moment to
                // work it out before chat starts telling them what to do.
                lightsNudge.ReportProgress();
                return;
            }

            lightsNudge.Disarm();

            ChatStimulusBus.Raise(ChatTopics.LightsRestored, 0.5f);
        }
    }
}
