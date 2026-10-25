using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Picks what the arms play while an item is in the hand, on every peer: the item's own
    /// <see cref="ItemDataSO.holdAnimation"/>, or a generic hold when it has none.
    /// </summary>
    /// <remarks>
    /// <para>PlayerAnimator has one HoldItem state on the upper layer. PlayerAnimations decides
    /// when it plays (the _holdItem bool, synced by NetworkAnimator); this decides what it plays,
    /// by swapping that state's clip in an AnimatorOverrideController of this player's own. So a
    /// new item needs no state in the controller and no code: only its ItemDataSO.</para>
    ///
    /// <para>Which item is held only exists on the owner (the selected slot is local), so the id
    /// goes out through a NetworkVariable and every peer swaps the clip on its own copy of the
    /// animator. Going empty keeps the last clip, so the arms blend out of the item's pose and not
    /// out of the generic one.</para>
    /// </remarks>
    public class PlayerHoldAnimation : NetworkBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private Animator animator;
        [SerializeField] private ItemListSO itemDatabase;

        [Tooltip("The clip the HoldItem state has in PlayerAnimator. It only marks which clip the held " +
                 "item's animation replaces; change it here if the state gets a different clip.")]
        [SerializeField] private AnimationClip holdStateClip;

        [Tooltip("Played for items whose ItemDataSO has no hold animation. Empty plays the HoldItem state's own clip.")]
        [SerializeField] private AnimationClip fallbackHoldAnimation;

        // Owner writes the id of the item in its hand, -1 when it is empty.
        private readonly NetworkVariable<int> _heldItemId = new(-1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private AnimatorOverrideController _overrides;
        private AnimationClip _appliedClip;


        private void Awake()
        {
            // Swapped before spawning, so the state NetworkAnimator syncs to late joiners lands on
            // the controller that stays.
            _overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = _overrides;
        }

        public override void OnNetworkSpawn()
        {
            _heldItemId.OnValueChanged += HeldItemId_OnValueChanged;
            ApplyHoldAnimation(_heldItemId.Value);

            if (IsOwner)
            {
                playerState.OnHoldItem += PlayerState_OnHoldItem;
            }
        }

        private void PlayerState_OnHoldItem(int itemId)
        {
            _heldItemId.Value = itemId;
        }

        private void HeldItemId_OnValueChanged(int previousItemId, int itemId)
        {
            ApplyHoldAnimation(itemId);
        }

        private void ApplyHoldAnimation(int itemId)
        {
            if (itemId == -1) return;

            ItemDataSO item = itemDatabase.GetItem(itemId);
            AnimationClip clip = item != null && item.holdAnimation != null ? item.holdAnimation : fallbackHoldAnimation;

            // Each swap makes the Animator reallocate its clip bindings, so items sharing a pose skip it.
            if (clip == _appliedClip) return;

            _appliedClip = clip;
            // Null takes the override out and the HoldItem state plays its own clip again.
            _overrides[holdStateClip] = clip;
        }

        public override void OnNetworkDespawn()
        {
            _heldItemId.OnValueChanged -= HeldItemId_OnValueChanged;

            if (IsOwner)
            {
                playerState.OnHoldItem -= PlayerState_OnHoldItem;
            }
        }
    }
}
