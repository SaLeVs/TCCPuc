using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Objects
{
    /// <summary>
    /// A light loose prop (cup, popcorn bag) that is kicked around. Every peer simulates its own
    /// copy, and <see cref="KickablePropNetwork"/> keeps the copies together: the peer that drives
    /// whatever kicked it last streams its motion, the others follow.
    ///
    /// <para>It needs no NetworkObject of its own, so it works the same placed in a scene or inside
    /// a room spawned at runtime (where a nested NetworkObject never spawns). Its id comes from
    /// where it was placed in the hierarchy, which is the same on every peer.</para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class KickableProp : MonoBehaviour
    {
        private const float MinServerHitSpeed = 0.2f;

        internal Rigidbody Body { get; private set; }
        internal uint Id { get; private set; }
        internal bool IsRegistered;
        internal bool IsUnsynced;

        // Network state, kept by KickablePropNetwork.
        internal ulong Authority;
        internal ushort Epoch;
        internal ushort LastSeq;
        internal ushort SendSeq;
        internal bool IsLocalAuthority;
        internal bool PendingClaim;
        internal bool IsStreaming;
        internal bool WasSleeping;
        internal float StillTime;
        internal Vector3 LastSentPosition;
        internal Quaternion LastSentRotation;
        internal Vector3 RestPosition;
        internal Quaternion RestRotation;

        // The nearest NetworkObject above it (a spawned room), or null for a prop placed in a scene.
        private NetworkObject _anchor;
        private string _path;

        private Rigidbody _lastContactBody;
        private NetworkObject _lastContactObject;
        private bool _lastContactIgnored;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            _anchor = transform.parent != null ? transform.parent.GetComponentInParent<NetworkObject>(true) : null;

            // Read now, before anything else is added to the scene or the room: it is the authored
            // hierarchy on every peer.
            _path = BuildPath(_anchor != null ? _anchor.transform : null);
        }

        private void OnEnable()
        {
            // A prop given its own NetworkObject (and a NetworkRigidbody) is already synced by
            // Netcode; driving it from here too would fight it.
            if (TryGetComponent(out NetworkObject _)) return;

            KickablePropNetwork.Add(this);
        }

        private void OnDisable()
        {
            KickablePropNetwork.Remove(this);
        }

        /// <summary>
        /// The id every peer gives this prop. A prop inside a spawned room has to wait for the room
        /// to spawn, since its id includes the room's NetworkObjectId.
        /// </summary>
        internal bool TryResolveId(out uint id)
        {
            id = 0;
            string key;

            if (_anchor != null)
            {
                if (!_anchor.IsSpawned) return false;
                key = "N" + _anchor.NetworkObjectId + ":" + _path;
            }
            else
            {
                key = "S" + gameObject.scene.name + ":" + _path;
            }

            id = Fnv1a(key);
            Id = id;
            return true;
        }

        /// <summary>
        /// Each step up to the anchor (or the scene root): its name and its exact local position,
        /// both read straight from the scene or prefab file, so every peer gets the same text.
        /// Sibling indices are not used: the order of root objects shifts when something calls
        /// DontDestroyOnLoad in an Awake, and Awake order differs from run to run.
        /// </summary>
        private string BuildPath(Transform anchor)
        {
            StringBuilder builder = new StringBuilder();
            Transform current = transform;

            while (current != null && current != anchor)
            {
                Vector3 local = current.localPosition;
                builder.Insert(0, string.Format("/{0}@{1:X8}{2:X8}{3:X8}", current.name,
                    System.BitConverter.SingleToInt32Bits(local.x),
                    System.BitConverter.SingleToInt32Bits(local.y),
                    System.BitConverter.SingleToInt32Bits(local.z)));
                current = current.parent;
            }

            return builder.ToString();
        }

        private static uint Fnv1a(string text)
        {
            uint hash = 2166136261;

            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }

            return hash;
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleContact(collision);
        }

        /// <summary>
        /// Something this peer drives (its own player, or on the server the monster, a door or a
        /// ragdoll) touched the prop: this peer now streams it. Bodies other peers drive only push
        /// the local copy, which the stream from their peer then corrects.
        /// </summary>
        private void HandleContact(Collision collision)
        {
            if (!IsRegistered || IsLocalAuthority || !KickablePropNetwork.IsActive) return;

            Rigidbody other = collision.rigidbody;
            if (other == null) return;

            if (other != _lastContactBody)
            {
                _lastContactBody = other;
                _lastContactIgnored = other.GetComponent<KickableProp>() != null;
                _lastContactObject = _lastContactIgnored ? null : other.GetComponentInParent<NetworkObject>();
            }

            // Props knock each other about on every peer; whoever drives the first one streams the rest.
            if (_lastContactIgnored) return;

            bool drivenHere;

            if (_lastContactObject != null && _lastContactObject.IsSpawned)
            {
                drivenHere = _lastContactObject.IsOwner;
            }
            else
            {
                // Bodies without a spawned NetworkObject (ragdolls, doors in spawned rooms) move on
                // every peer at once; only the server's copy counts, so the peers don't all claim it.
                // And only when it hits: a body merely lying against the prop would take it back
                // from the player kicking it on every physics step.
                NetworkManager networkManager = NetworkManager.Singleton;
                drivenHere = networkManager != null && networkManager.IsServer &&
                             collision.relativeVelocity.sqrMagnitude > MinServerHitSpeed * MinServerHitSpeed;
            }

            if (drivenHere)
            {
                KickablePropNetwork.Claim(this);
            }
        }
    }
}
