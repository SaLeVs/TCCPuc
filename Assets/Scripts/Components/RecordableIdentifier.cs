using Enums;
using UnityEngine;

namespace Components
{
    /// <summary>
    /// Marks an object as something the stream reacts to: worth audience, worth a chat line, or
    /// both.
    /// </summary>
    /// <remarks>
    /// There used to be a hand-written <c>recordableId</c> here so the server could name the object
    /// to a client. It was authored on the prefab, so every copy of a prop shared one id and only a
    /// single copy was ever resolvable - the other fifty-three masks gave nothing. The lookup now
    /// goes through <see cref="RecordableRegistry"/>, which is keyed by where the object actually
    /// stands, so every copy counts and nothing has to be numbered by hand.
    /// </remarks>
    public class RecordableIdentifier : MonoBehaviour
    {
        public RecordableTarget targetType;
        public float minimumViewTime = 2f;

        public float audienceGain = 50f;
        public float reviewCooldown = 60f;
        public bool canBeReviewed;

        public bool canBeReviewedForChat = true;
        public float chatCooldown = 20f;

        private bool _positioned;

        /// <summary>
        /// Registers from Start, not OnEnable.
        ///
        /// <para>Netcode creates a spawned object first and applies its replicated transform after,
        /// so a client running this in OnEnable would file the prop under the position it had
        /// inside the prefab rather than where it ended up in the room. By Start the transform has
        /// settled on every peer.</para>
        /// </summary>
        private void Start()
        {
            _positioned = true;

            RecordableRegistry.Register(this);
        }

        private void OnEnable()
        {
            if (_positioned) RecordableRegistry.Register(this);
        }

        private void OnDisable() => RecordableRegistry.Unregister(this);
    }
}
