using Interfaces;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// How the player's body and the monster's meet.
    ///
    /// <para>The monster is a kinematic body driven by its NavMeshAgent: to the physics it has
    /// infinite mass. Pinned between it and a wall, the player's capsule was pushed out through
    /// the wall. So on the owner the two bodies no longer collide; the monster's overlap is turned
    /// into velocity instead (<see cref="ApplyMonsterSeparation"/>), handed to the stair climber
    /// with the rest of the move, and that stops at walls like any other move.</para>
    ///
    /// <para>On the server it also keeps time: leaned on long enough by something that counts as
    /// pushing (the monster between swings), the player is knocked down.</para>
    /// </summary>
    public class PlayerMonsterContact : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerKnockdown knockdown;
        [SerializeField] private PlayerDead playerDead;
        [SerializeField] private CapsuleCollider playerCapsule;
        [SerializeField] private Rigidbody playerRigidbody;

        [Header("Separation")]
        [Tooltip("Layers of the bodies that push the player. Empty means the Monster layer.")]
        [SerializeField] private LayerMask monsterMask;

        [Tooltip("Fastest the player is moved out of the monster, in metres per second.")]
        [SerializeField, Min(0f)] private float maxSeparationSpeed = 8f;

        [Header("Push knockdown")]
        [Tooltip("What being shoved over does. Leave empty to never knock down by pushing.")]
        [SerializeField] private ImpactProfileSO pushImpact;

        [Tooltip("Seconds of being pushed, with no break longer than the grace below, before the player goes down.")]
        [SerializeField, Min(0.1f)] private float pushKnockdownSeconds = 2f;

        [Tooltip("How far apart the bodies may be and still count as touching. The separation keeps " +
                 "them just apart, so without some margin a pinned player would never count.")]
        [SerializeField, Min(0f)] private float contactMargin = 0.15f;

        [Tooltip("A break in contact shorter than this does not restart the count.")]
        [SerializeField, Min(0f)] private float contactGraceSeconds = 0.3f;

        private const float DiscoveryMargin = 1.5f;

        private readonly Collider[] _nearby = new Collider[16];
        private Collider[] _ownColliders;

        private float _pushTime;
        private float _lastPushTime = float.NegativeInfinity;

        private void Awake()
        {
            if (knockdown == null) knockdown = GetComponent<PlayerKnockdown>();
            if (playerDead == null) playerDead = GetComponent<PlayerDead>();
            if (playerCapsule == null) playerCapsule = GetComponent<CapsuleCollider>();
            if (playerRigidbody == null) playerRigidbody = GetComponent<Rigidbody>();
            if (monsterMask.value == 0) monsterMask = LayerMask.GetMask("Monster");

            _ownColliders = GetComponentsInChildren<Collider>(true);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned) return;

            if (IsOwner) IgnoreMonsterBodies();
            if (IsServer) TickPushKnockdown(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Owner only, from PlayerMovement: the velocity the player wants, with the part going into
        /// the monster dropped and a push out of it added. Walls still stop the result, because it
        /// goes through the stair climber like any other move.
        /// </summary>
        public Vector3 ApplyMonsterSeparation(Vector3 velocity)
        {
            if (playerCapsule == null || !playerCapsule.enabled) return velocity;

            GetCapsulePose(out Vector3 position, out Quaternion rotation);

            int count = Physics.OverlapSphereNonAlloc(position, CapsuleReach() + DiscoveryMargin, _nearby, monsterMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider monster = _nearby[i];

                if (!Physics.ComputePenetration(playerCapsule, position, rotation, monster,
                        monster.transform.position, monster.transform.rotation, out Vector3 direction,
                        out float distance))
                {
                    continue;
                }

                Vector3 away = new Vector3(direction.x, 0f, direction.z);

                if (away.sqrMagnitude < 0.0001f)
                {
                    away = position - monster.bounds.center;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f) continue;
                }

                away.Normalize();

                float into = -Vector3.Dot(velocity, away);
                if (into > 0f) velocity += away * into;

                velocity += away * Mathf.Min(distance / Time.fixedDeltaTime, maxSeparationSpeed);
            }

            return velocity;
        }

        /// <summary>
        /// Contacts between the two are dropped as soon as the monster comes near. Re-done every
        /// step: Unity forgets an ignored pair once either collider is switched off, and the
        /// player's are while knocked down.
        /// </summary>
        private void IgnoreMonsterBodies()
        {
            if (playerCapsule == null || !playerCapsule.enabled || !playerCapsule.gameObject.activeInHierarchy) return;

            GetCapsulePose(out Vector3 position, out _);

            int count = Physics.OverlapSphereNonAlloc(position, CapsuleReach() + DiscoveryMargin * 2f, _nearby,
                monsterMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                foreach (Collider own in _ownColliders)
                {
                    if (own == null || own.isTrigger || !own.enabled || !own.gameObject.activeInHierarchy) continue;

                    Physics.IgnoreCollision(own, _nearby[i], true);
                }
            }
        }

        private void TickPushKnockdown(float deltaTime)
        {
            if (pushImpact == null || knockdown == null || playerCapsule == null) return;

            if (knockdown.IsKnockedDown || (playerDead != null && playerDead.IsDead))
            {
                _pushTime = 0f;
                return;
            }

            if (TryFindPusher(out Vector3 pusherCenter))
            {
                _pushTime += deltaTime;
                _lastPushTime = Time.time;
            }
            else if (Time.time - _lastPushTime > contactGraceSeconds)
            {
                _pushTime = 0f;
            }

            if (_pushTime < pushKnockdownSeconds) return;

            _pushTime = 0f;

            // Thrown the way it was being pushed: away from the monster.
            knockdown.ApplyImpact(pushImpact, transform.position - pusherCenter);
        }

        /// <summary>
        /// Something touching the player that counts as pushing right now. Built from the capsule's
        /// shape rather than queried with it, so it works for remote players too, whose body only
        /// follows the network.
        /// </summary>
        private bool TryFindPusher(out Vector3 pusherCenter)
        {
            pusherCenter = default;

            Vector3 scale = playerCapsule.transform.lossyScale;
            float radius = playerCapsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfSegment = Mathf.Max(0f, playerCapsule.height * Mathf.Abs(scale.y) * 0.5f - radius);

            Vector3 center = playerCapsule.transform.TransformPoint(playerCapsule.center);
            Vector3 up = playerCapsule.transform.up * halfSegment;

            int count = Physics.OverlapCapsuleNonAlloc(center - up, center + up, radius + contactMargin, _nearby,
                monsterMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                // The monster's body collider sits on its root, and MonsterAttack further down.
                NetworkObject owner = _nearby[i].GetComponentInParent<NetworkObject>();
                IPushKnockdownSource source = owner != null
                    ? owner.GetComponentInChildren<IPushKnockdownSource>()
                    : _nearby[i].GetComponentInParent<IPushKnockdownSource>();

                if (source == null || !source.CanKnockDownByPushing) continue;

                pusherCenter = _nearby[i].bounds.center;
                return true;
            }

            return false;
        }

        private void GetCapsulePose(out Vector3 position, out Quaternion rotation)
        {
            // At the physics pose when the capsule is on the body itself: interpolation leaves the
            // transform a step behind.
            if (playerRigidbody != null && playerRigidbody.transform == playerCapsule.transform)
            {
                position = playerRigidbody.position;
                rotation = playerRigidbody.rotation;
                return;
            }

            position = playerCapsule.transform.position;
            rotation = playerCapsule.transform.rotation;
        }

        private float CapsuleReach()
        {
            Vector3 scale = playerCapsule.transform.lossyScale;
            float size = Mathf.Max(playerCapsule.height * Mathf.Abs(scale.y),
                playerCapsule.radius * 2f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            return playerCapsule.center.magnitude + size;
        }
    }
}
