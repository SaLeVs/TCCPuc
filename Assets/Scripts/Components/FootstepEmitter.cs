using Components.Sound;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class FootstepEmitter : NetworkBehaviour
    {
        [Tooltip("The step's clips, how far it carries and whether the monster hears it. Its range is the " +
                 "normal step; crouching and sprinting scale it through LoudnessMultiplier.")]
        [SerializeField] private SoundDefinitionSO footstepSound;

        /// <summary>
        /// Scales every step this emitter makes — for the players who hear it and for the monster alike.
        /// Driven by the player's PlayerNoiseProfile: crouch turns it down, sprint turns it up. Only
        /// meaningful on the owner, which is the only copy that emits.
        /// </summary>
        public float LoudnessMultiplier { get; set; } = 1f;

        /// <summary>
        /// Called by animation events — on every peer, since every peer plays the animation. Only the
        /// owner's copy counts: for a player that is the client whose legs they are, for the monster
        /// the server. Letting every copy emit is what played a client's steps twice, the second one
        /// from the host's copy at full loudness, because the host never knew the player was crouching.
        /// </summary>
        public void AnimationFootstep()
        {
            if (!IsOwner) return;

            WorldSound.Play(footstepSound, transform.position, NetworkObject, Mathf.Max(0f, LoudnessMultiplier));
        }
    }
}
