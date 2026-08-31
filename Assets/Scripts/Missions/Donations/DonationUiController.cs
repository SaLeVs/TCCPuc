using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Donations
{
    public class DonationUiController : MonoBehaviour
    {
        [SerializeField] private Transform feedContainer;
        [SerializeField] private DonationPopupView popupPrefab;
        [SerializeField] private int maxVisiblePopups = 4;

        private readonly Dictionary<string, DonationPopupView> _activeViews = new();
        private readonly Dictionary<string, DonationNetworkState> _lastKnownState = new();
        private DonationManager _manager;
        

        private void OnEnable()
        {
            TryBind();
        }

        private void Update()
        {
            if (_manager == null)
            {
                TryBind();
                return;
            }

            UpdateExpirationBars();
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

        private void UpdateExpirationBars()
        {
            if (NetworkManager.Singleton == null || _activeViews.Count == 0) return;

            double now = NetworkManager.Singleton.ServerTime.TimeAsFloat;

            foreach (KeyValuePair<string, DonationPopupView> kvp in _activeViews)
            {
                if (!_lastKnownState.TryGetValue(kvp.Key, out var state)) continue;
                if (state.ExpireTime <= 0) continue;

                double total = state.ExpireTime - state.SpawnTime;
                double elapsed = now - state.SpawnTime;
                float ratio = total > 0 ? 1f - Mathf.Clamp01((float)(elapsed / total)) : 1f;

                kvp.Value.SetExpirationRatio(ratio);
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
                    RemoveView(changeEvent.Value.InstanceId.ToString());
                    break;
            }
        }

        private void HandleState(DonationNetworkState state)
        {
            string id = state.InstanceId.ToString();
            _lastKnownState[id] = state;

            if (state.State == DonationState.Completed || state.State == DonationState.Expired)
            {
                RemoveView(id);
                return;
            }

            if (!_activeViews.TryGetValue(id, out var view))
            {
                if (_activeViews.Count >= maxVisiblePopups) return;

                view = Instantiate(popupPrefab, feedContainer);
                _activeViews[id] = view;
                view.Setup(state);
            }
            else
            {
                view.UpdateState(state);
            }
        }

        private void RemoveView(string instanceId)
        {
            _lastKnownState.Remove(instanceId);

            if (_activeViews.TryGetValue(instanceId, out var view))
            {
                _activeViews.Remove(instanceId);
                view.PlayExit(() => Destroy(view.gameObject));
            }
        }
        
        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.NetworkStates.OnListChanged -= HandleListChanged;
            }
        }
        
    }
}