using UnityEngine;

namespace Player
{
    /// <summary>
    /// Drives one blend shape from PlayerCrouch's state: the scarf's corrective shape while the
    /// player is crouched, or the hat's flattened crown while the hat is squashed. Eases at the same
    /// pace the capsule changes size, so the mesh never outgrows the collider that carries it.
    /// </summary>
    /// <remarks>
    /// <para>Runs on every peer: both states are NetworkVariables on PlayerCrouch, so other players
    /// and the mirror see the same shapes.</para>
    ///
    /// <para>The weight is written every LateUpdate, not only when it changes: every clip in the FBX
    /// carries a flat 0 curve for these blend shapes, and with Write Defaults the Animator puts them
    /// back to 0 each frame before this runs.</para>
    /// </remarks>
    public class PlayerCrouchBlendShape : MonoBehaviour
    {
        private enum Trigger
        {
            Crouching,
            HatSquashed
        }

        [SerializeField] private PlayerCrouch playerCrouch;
        [SerializeField] private Trigger trigger;

        [Tooltip("Every renderer whose mesh has the blend shape; the same weight goes to all of them.")]
        [SerializeField] private SkinnedMeshRenderer[] renderers;

        [SerializeField] private string blendShapeName;

        [Tooltip("How fast the shape goes in and out. Match PlayerCrouch's crouch speed.")]
        [SerializeField, Min(0.1f)] private float speed = 10f;

        private const float FullWeight = 100f;

        private int[] _indices;
        private float _weight;

        private void Awake()
        {
            _indices = new int[renderers.Length];
            bool any = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                _indices[i] = renderers[i] != null && renderers[i].sharedMesh != null
                    ? renderers[i].sharedMesh.GetBlendShapeIndex(blendShapeName)
                    : -1;

                if (_indices[i] >= 0) any = true;
                else Debug.LogWarning($"{name}: no blend shape '{blendShapeName}' on renderer {i}.", this);
            }

            if (!any) enabled = false;
        }

        private void LateUpdate()
        {
            bool isOn = trigger == Trigger.Crouching ? playerCrouch.IsCrouching : playerCrouch.IsHatSquashed;
            float target = isOn ? FullWeight : 0f;

            _weight = Mathf.Lerp(_weight, target, speed * Time.deltaTime);
            if (Mathf.Abs(_weight - target) < 0.5f) _weight = target;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (_indices[i] >= 0) renderers[i].SetBlendShapeWeight(_indices[i], _weight);
            }
        }
    }
}
