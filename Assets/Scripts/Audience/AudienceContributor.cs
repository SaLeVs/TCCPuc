using System.Collections.Generic;
using Components;
using Enums;
using Unity.Netcode;
using UnityEngine;

namespace Audience
{
    [RequireComponent(typeof(VisionSensor))]
    public class AudienceContributor : NetworkBehaviour
    {
        [SerializeField] private VisionSensor visionSensor;

        public bool IsWatchingSomething => _activeObjects.Count > 0;
        public bool HasBeenSeen(GameObject target) => _seenObjects.ContainsKey(target.GetInstanceID());
        public int UniqueObjectsSeen => _seenObjects.Count;

        /// <summary>
        /// How often the server is told the player is still filming something it has already paid
        /// for. This used to fire every frame, which is one RPC per frame per player for a message
        /// whose only job is to hold off a thirty second idle timer.
        /// </summary>
        private const float PRESENCE_REPORT_INTERVAL = 0.5f;

        private Dictionary<GameObject, float> _activeObjects = new Dictionary<GameObject, float>();
        private Dictionary<int, float> _pendingViewTime = new Dictionary<int, float>();

        private Dictionary<int, float> _seenObjects = new Dictionary<int, float>();
        private HashSet<GameObject> _presenceObjects = new HashSet<GameObject>();

        private readonly List<GameObject> _activeBuffer = new List<GameObject>();
        private readonly List<GameObject> _presenceBuffer = new List<GameObject>();

        private float _presenceReportTimer;


        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            visionSensor.OnTargetEnter += HandleTargetEnterStatic;
            visionSensor.OnTargetExit += HandleTargetExitStatic;

            visionSensor.OnTargetEnterStatic += HandleTargetEnterStatic;
            visionSensor.OnTargetExitStatic += HandleTargetExitStatic;
        }


        private void Update()
        {
            if (!IsOwner) return;

            TickActiveObjects(Time.deltaTime);
            TickPresenceObjects(Time.deltaTime);
        }

        private void HandleTargetEnterStatic(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier id))
            {
                HandleTargetEnter(target, id.targetType);
            }
        }

        private void HandleTargetExitStatic(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier id))
            {
                HandleTargetExit(target, id.targetType);
            }
        }

        private void HandleTargetEnter(GameObject target, RecordableTarget targetType)
        {
            if (!target.TryGetComponent(out RecordableIdentifier identifier)) return;

            int id = target.GetInstanceID();

            if (_seenObjects.TryGetValue(id, out float seenAt))
            {
                if (identifier.canBeReviewed && Time.time - seenAt >= identifier.reviewCooldown)
                {
                    _seenObjects.Remove(id);
                    _activeObjects[target] = 0f;
                }
                else
                {
                    _presenceObjects.Add(target);
                }
                return;
            }

            if (!_activeObjects.ContainsKey(target))
            {
                float resumedTime = 0f;
                if (_pendingViewTime.TryGetValue(id, out float saved))
                {
                    resumedTime = saved;
                    _pendingViewTime.Remove(id);
                }

                _activeObjects[target] = resumedTime;
            }
        }

        private void HandleTargetExit(GameObject target, RecordableTarget targetType)
        {
            if (_presenceObjects.Contains(target))
            {
                _presenceObjects.Remove(target);
                return;
            }

            if (!_activeObjects.ContainsKey(target)) return;
            if (!target.TryGetComponent(out RecordableIdentifier identifier)) return;

            float accumulatedTime = _activeObjects[target];
            int id = target.GetInstanceID();

            if (accumulatedTime < identifier.minimumViewTime)
            {
                _pendingViewTime[id] = accumulatedTime;
                _activeObjects.Remove(target);
            }
            else
            {
                _activeObjects.Remove(target);
            }
        }

        private void TickActiveObjects(float deltaTime)
        {
            if (_activeObjects.Count == 0) return;

            _activeBuffer.Clear();
            _activeBuffer.AddRange(_activeObjects.Keys);

            foreach (GameObject target in _activeBuffer)
            {
                if (target == null)
                {
                    _activeObjects.Remove(target);
                    continue;
                }

                if (!target.TryGetComponent(out RecordableIdentifier identifier)) continue;

                _activeObjects[target] += deltaTime;

                if (_activeObjects[target] < identifier.minimumViewTime) continue;

                SubmitAudienceServerRpc(identifier.audienceGain);

                _seenObjects[target.GetInstanceID()] = Time.time;
                _activeObjects.Remove(target);
            }
        }

        [Rpc(SendTo.Server)]
        private void SubmitAudienceServerRpc(float gain)
        {
            if (AudienceManager.Instance == null) return;

            AudienceManager.Instance.SubmitGain(gain);
        }

        /// <summary>
        /// Re-arms anything whose review cooldown ran out while it was still in the cone, then tells
        /// the server - at a fixed interval - that the player is still filming.
        /// </summary>
        /// <remarks>
        /// The re-arm used to live only in <see cref="HandleTargetEnter"/>, so it needed a fresh
        /// enter event to run. A player already looking at a prop when its cooldown expired got no
        /// such event: the prop sat in <c>_presenceObjects</c> for the rest of the run, paying
        /// nothing. The presence report below then pinned the manager's idle timer at zero, so the
        /// bar could not decay either. Nothing in, nothing out - which is the frozen audience bar.
        /// Breaking line of sight and looking back was the only way out, and nothing told the player
        /// that.
        /// </remarks>
        private void TickPresenceObjects(float deltaTime)
        {
            if (_presenceObjects.Count == 0)
            {
                // Reset so the next thing that comes into view reports immediately rather than
                // waiting out the remainder of an interval it was not around for.
                _presenceReportTimer = 0f;
                return;
            }

            _presenceBuffer.Clear();

            foreach (GameObject target in _presenceObjects)
            {
                if (target == null || !target.TryGetComponent(out RecordableIdentifier identifier))
                {
                    _presenceBuffer.Add(target);
                    continue;
                }

                // Nothing to wait for: this one pays once and is done. It still counts as something
                // the player is filming, so it stays a presence.
                if (!identifier.canBeReviewed) continue;

                int id = target.GetInstanceID();

                if (_seenObjects.TryGetValue(id, out float seenAt)
                    && Time.time - seenAt < identifier.reviewCooldown) continue;

                _seenObjects.Remove(id);
                _activeObjects[target] = 0f;
                _presenceBuffer.Add(target);
            }

            foreach (GameObject target in _presenceBuffer)
            {
                _presenceObjects.Remove(target);
            }

            if (_presenceObjects.Count == 0)
            {
                _presenceReportTimer = 0f;
                return;
            }

            _presenceReportTimer -= deltaTime;

            if (_presenceReportTimer > 0f) return;

            _presenceReportTimer = PRESENCE_REPORT_INTERVAL;

            SubmitPresenceServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void SubmitPresenceServerRpc()
        {
            if (AudienceManager.Instance == null) return;

            AudienceManager.Instance.SubmitPresence();
        }

        /// <summary>
        /// Wipes what this player has already been paid for, so every prop in the level is worth
        /// something again. For a new contract or a new round.
        /// </summary>
        /// <remarks>
        /// The state below is the owner's - the server never fills it - so this goes to the owner.
        /// It was declared <c>SendTo.ClientsAndHost</c> under a name ending in ServerRpc, which
        /// would have cleared every player's progress at once had anything ever called it.
        /// </remarks>
        [Rpc(SendTo.Owner)]
        public void ResetSeenObjectsRpc()
        {
            _seenObjects.Clear();
            _activeObjects.Clear();
            _presenceObjects.Clear();
            _pendingViewTime.Clear();

            _presenceReportTimer = 0f;
        }


        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            visionSensor.OnTargetEnter -= HandleTargetEnterStatic;
            visionSensor.OnTargetExit -= HandleTargetExitStatic;

            visionSensor.OnTargetEnterStatic -= HandleTargetEnterStatic;
            visionSensor.OnTargetExitStatic -= HandleTargetExitStatic;
        }

    }
}
