using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Missions.Donations
{
    /// <summary>
    /// The client-side life of a donation, in three stages. It is announced on a card in the middle
    /// of the screen while the TTS reads it, then it drops into a tray under the missions, and from
    /// there the player cycles through whatever is pending on a single key, grenade-style.
    ///
    /// There is only ever one card on screen. It is fed either by the alert queue or by the manual
    /// selection, which is why both live in here instead of in two components fighting over it.
    /// </summary>
    public class DonationUIController : MonoBehaviour
    {
        /// <summary>Carries a position so SfxManager can play it like every other sound.</summary>
        public static event Action<Vector3> OnDonationReceivedSound;

        public static event Action<Vector3> OnDonationCompletedSound;

        [Header("Card slot")]
        [Tooltip("Where the single card is spawned. One card is reused for every donation.")]
        [SerializeField] private Transform cardContainer;

        [SerializeField] private DonationPopupView cardPrefab;

        [Header("Tray")]
        [Tooltip("Row the chips line up in, under the missions panel.")]
        [SerializeField] private Transform trayContainer;

        [Tooltip("Small view: the same DonationPopupView script with only the icon and the " +
                 "expiration ring wired, so the text fields simply go untouched.")]
        [SerializeField] private DonationPopupView chipPrefab;

        [Tooltip("Turned off while the tray is empty, so no empty box sits under the missions.")]
        [SerializeField] private GameObject trayRoot;

        [Header("Alert timing")]
        [Tooltip("Vivox gives no callback when its TTS finishes, so the card's time on screen is " +
                 "estimated from the text instead: base + words / rate, clamped to the range below.")]
        [SerializeField, Min(0f)] private float alertBaseSeconds = 1.5f;

        [SerializeField, Min(0.1f)] private float alertWordsPerSecond = 2.5f;

        [Tooltip("Floor and ceiling for the estimate, so a one-word donation still lands and a " +
                 "long one does not hold the screen forever.")]
        [SerializeField] private Vector2 alertSecondsRange = new Vector2(3f, 12f);

        [Header("Events")]
        [Tooltip("Wire the card's HudAnimationPanel Show here. Kept as events because the donation " +
                 "assembly cannot reference the UI one without creating a circular dependency.")]
        [SerializeField] private UnityEvent onCardShown;

        [SerializeField] private UnityEvent onCardHidden;

        private readonly Dictionary<string, DonationNetworkState> _states = new();
        private readonly Dictionary<string, DonationPopupView> _chips = new();
        private readonly Queue<string> _pendingAlerts = new();
        private readonly List<string> _trayOrder = new();

        private DonationManager _manager;
        private DonationPopupView _card;
        private string _alertId;
        private string _selectedId;
        private float _alertEndsAt;
        private bool _isCardShown;

        /// <summary>SfxManager reads Vector3.zero as "this is an interface sound": 2D, no occlusion.</summary>
        private static readonly Vector3 UiSoundPosition = Vector3.zero;

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.NetworkStates.OnListChanged -= HandleListChanged;
            }
        }

        private void Update()
        {
            if (_manager == null)
            {
                TryBind();
                return;
            }

            TickAlert();
            TickExpiration();
        }

        /// <summary>
        /// One key does everything, the way grenades do: it walks the tray and then wraps round to
        /// nothing. Pressing during an alert cuts the alert short instead of being swallowed, so
        /// the player is never waiting on the UI.
        /// </summary>
        public void CycleNext()
        {
            if (_alertId != null)
            {
                FinishAlert();
                return;
            }

            if (_trayOrder.Count == 0)
            {
                Select(null);
                return;
            }

            int next = _selectedId == null ? 0 : _trayOrder.IndexOf(_selectedId) + 1;

            Select(next >= 0 && next < _trayOrder.Count ? _trayOrder[next] : null);
        }

        private void TryBind()
        {
            _manager = DonationManager.Instance;
            if (_manager == null) return;

            _manager.NetworkStates.OnListChanged += HandleListChanged;

            foreach (var state in _manager.NetworkStates)
            {
                HandleState(state);
            }
        }

        private void HandleListChanged(NetworkListEvent<DonationNetworkState> changeEvent)
        {
            switch (changeEvent.Type)
            {
                case NetworkListEvent<DonationNetworkState>.EventType.Add:
                case NetworkListEvent<DonationNetworkState>.EventType.Insert:
                case NetworkListEvent<DonationNetworkState>.EventType.Value:
                    HandleState(changeEvent.Value);
                    break;

                case NetworkListEvent<DonationNetworkState>.EventType.Remove:
                case NetworkListEvent<DonationNetworkState>.EventType.RemoveAt:
                    Drop(changeEvent.Value.InstanceId.ToString());
                    break;
            }
        }

        private void HandleState(DonationNetworkState state)
        {
            string id = state.InstanceId.ToString();
            bool isNew = !_states.ContainsKey(id);

            _states[id] = state;

            if (state.State == DonationState.Completed)
            {
                OnDonationCompletedSound?.Invoke(UiSoundPosition);
                Drop(id);
                return;
            }

            if (state.State == DonationState.Expired)
            {
                Drop(id);
                return;
            }

            if (isNew)
            {
                OnDonationReceivedSound?.Invoke(UiSoundPosition);
                _pendingAlerts.Enqueue(id);
                return;
            }

            RefreshViews(id, state);
        }

        private void TickAlert()
        {
            if (_alertId == null)
            {
                if (_pendingAlerts.Count == 0) return;

                StartNextAlert();
                return;
            }

            if (Time.unscaledTime >= _alertEndsAt) FinishAlert();
        }

        private void StartNextAlert()
        {
            // Donations that vanished while queued have no state left; skip straight past them.
            while (_pendingAlerts.Count > 0)
            {
                string id = _pendingAlerts.Dequeue();
                if (!_states.TryGetValue(id, out var state)) continue;

                _alertId = id;
                _alertEndsAt = Time.unscaledTime + EstimateAlertSeconds(state);

                ShowCard(state);
                return;
            }
        }

        /// <summary>The card had its moment: it becomes a chip and the queue moves on.</summary>
        private void FinishAlert()
        {
            if (_alertId == null) return;

            string id = _alertId;
            _alertId = null;

            AddToTray(id);
            HideCard();
        }

        /// <summary>
        /// Vivox will not tell us how long it speaks for, so the card's dwell time is read off the
        /// text at a plain words-per-second rate and clamped at both ends.
        /// </summary>
        private float EstimateAlertSeconds(DonationNetworkState state)
        {
            string message = state.Message.ToString();
            int words = string.IsNullOrWhiteSpace(message)
                ? 0
                : message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

            float estimate = alertBaseSeconds + words / Mathf.Max(0.1f, alertWordsPerSecond);

            return Mathf.Clamp(estimate, alertSecondsRange.x, alertSecondsRange.y);
        }

        private void AddToTray(string id)
        {
            if (!_states.TryGetValue(id, out var state)) return;
            if (_chips.ContainsKey(id)) return;

            DonationPopupView chip = Instantiate(chipPrefab, trayContainer);

            _chips[id] = chip;
            _trayOrder.Add(id);

            // The tray root has to be on before the chip is set up. While it is off, the chip is
            // not active in the hierarchy, so Unity defers its Awake - which left Setup using a
            // RectTransform that had never been cached, and StartCoroutine refusing to run the
            // enter animation. The tray root is switched off whenever the last donation leaves, so
            // this hit every donation arriving into an empty tray.
            RefreshTrayRoot();

            chip.Setup(state, ResolveIcon(state));
        }

        /// <summary>
        /// Takes a donation out of every stage at once. Whatever the player was looking at moves on
        /// to the next one rather than leaving them staring at a card that no longer means anything.
        /// </summary>
        private void Drop(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            int index = _trayOrder.IndexOf(id);

            if (_chips.TryGetValue(id, out var chip))
            {
                _chips.Remove(id);
                chip.PlayExit(() => Destroy(chip.gameObject));
            }

            if (index >= 0) _trayOrder.RemoveAt(index);

            _states.Remove(id);

            if (_alertId == id)
            {
                _alertId = null;
                HideCard();
            }

            if (_selectedId == id)
            {
                // The one that left held the slot, so slide the selection to whatever took its
                // place in the row; if it was the last, there is nothing left to show.
                Select(index >= 0 && index < _trayOrder.Count ? _trayOrder[index] : null);
            }

            RefreshTrayRoot();
        }

        private void Select(string id)
        {
            _selectedId = id;

            if (id != null && _states.TryGetValue(id, out var state))
            {
                ShowCard(state);
                return;
            }

            _selectedId = null;
            HideCard();
        }

        private void ShowCard(DonationNetworkState state)
        {
            if (_card == null)
            {
                if (cardPrefab == null || cardContainer == null) return;

                _card = Instantiate(cardPrefab, cardContainer);
            }

            _card.Setup(state, ResolveIcon(state));

            if (_isCardShown) return;

            _isCardShown = true;
            onCardShown?.Invoke();
        }

        private void HideCard()
        {
            if (!_isCardShown) return;

            _isCardShown = false;
            onCardHidden?.Invoke();
        }

        private void RefreshViews(string id, DonationNetworkState state)
        {
            if (_chips.TryGetValue(id, out var chip)) chip.UpdateState(state);

            if (_card != null && (id == _alertId || id == _selectedId)) _card.UpdateState(state);
        }

        private void RefreshTrayRoot()
        {
            if (trayRoot != null && trayRoot.activeSelf != (_trayOrder.Count > 0))
            {
                trayRoot.SetActive(_trayOrder.Count > 0);
            }
        }

        private void TickExpiration()
        {
            if (NetworkManager.Singleton == null) return;
            if (_states.Count == 0) return;

            double now = NetworkManager.Singleton.ServerTime.TimeAsFloat;

            foreach (var pair in _states)
            {
                DonationNetworkState state = pair.Value;
                float ratio;
                float remaining;

                if (state.ExpireTime <= 0)
                {
                    ratio = 1f;
                    remaining = -1f;
                }
                else
                {
                    double total = state.ExpireTime - state.SpawnTime;
                    double elapsed = now - state.SpawnTime;

                    remaining = (float)Math.Max(0, state.ExpireTime - now);
                    ratio = total > 0 ? 1f - Mathf.Clamp01((float)(elapsed / total)) : 1f;
                }

                if (_chips.TryGetValue(pair.Key, out var chip)) chip.SetExpiration(ratio, remaining);

                if (_card != null && (pair.Key == _alertId || pair.Key == _selectedId))
                {
                    _card.SetExpiration(ratio, remaining);
                }
            }
        }

        /// <summary>
        /// The network state only carries the donation id, so the sprite comes from the authored
        /// DonationDefinition, looked up on this client through the manager's pool.
        /// </summary>
        private Sprite ResolveIcon(DonationNetworkState state)
        {
            if (_manager == null) return null;

            DonationDefinition definition = _manager.GetDefinition(state.DonationId.ToString());
            return definition != null ? definition.icon : null;
        }

    }
}
