using System.Collections.Generic;
using Enums;
using Interfaces;
using UnityEngine;

namespace Components
{
    /// <summary>
    /// Opts an object into the viewfinder brackets the HUD draws around things worth noticing.
    /// </summary>
    /// <remarks>
    /// This only describes the object: what it frames, what the prompt says, how far away it
    /// starts showing. Drawing is entirely the local player's HUD, so the component carries no
    /// network state and every peer can hold it without cost. What the object *is* comes from its
    /// neighbours - an <see cref="IInteractable"/> earns the bracket and the prompt, a
    /// <see cref="RecordableIdentifier"/> earns the eye.
    /// </remarks>
    public class InteractionMarker : MonoBehaviour
    {
        private static readonly List<InteractionMarker> ActiveMarkers = new();

        /// <summary>Every enabled marker in the loaded scenes.</summary>
        public static IReadOnlyList<InteractionMarker> Active => ActiveMarkers;

        [Tooltip("Verb shown next to the key when the player aims at this, e.g. \"Pick up\" or " +
                 "\"Open\". Leave empty to show the key alone.")]
        [SerializeField] private string actionLabel = "Pick up";

        [Tooltip("Renderers the brackets wrap around. Leave empty to frame every mesh under " +
                 "this object.")]
        [SerializeField] private Renderer[] framedRenderers;

        [Tooltip("How close the player must be before the brackets appear. 0 uses the HUD " +
                 "default for this kind of object.")]
        [SerializeField, Min(0f)] private float revealDistance;

        public string ActionLabel => actionLabel;
        public float RevealDistance => revealDistance;

        public IInteractable Interactable { get; private set; }
        public RecordableIdentifier Recordable { get; private set; }

        public bool IsInteractable => Interactable != null && (Interactable as MonoBehaviour) != null;
        public bool IsRecordable => Recordable != null && Recordable.targetType != RecordableTarget.None;

        private void Awake()
        {
            // Children too: the interactable usually sits on whichever child carries the collider
            // the interactor's raycast hits, while the marker sits on the root it frames.
            Interactable = GetComponentInChildren<IInteractable>(true);
            Recordable = GetComponentInChildren<RecordableIdentifier>(true);

            if (framedRenderers == null || framedRenderers.Length == 0)
                framedRenderers = CollectMeshRenderers();
        }

        private void OnEnable() => ActiveMarkers.Add(this);

        private void OnDisable() => ActiveMarkers.Remove(this);

        /// <summary>
        /// World bounds of what is currently visible of the object. Falls back to its colliders
        /// when every renderer is hidden, so an item whose mesh is swapped out keeps its frame.
        /// </summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (Renderer framed in framedRenderers)
            {
                if (framed == null || !framed.enabled || !framed.gameObject.activeInHierarchy) continue;

                if (found) bounds.Encapsulate(framed.bounds);
                else bounds = framed.bounds;

                found = true;
            }

            if (found) return true;

            foreach (Collider body in GetComponentsInChildren<Collider>())
            {
                if (!body.enabled) continue;

                if (found) bounds.Encapsulate(body.bounds);
                else bounds = body.bounds;

                found = true;
            }

            return found;
        }

        /// <summary>Whether a hit collider is part of this object, for line-of-sight tests.</summary>
        public bool Owns(Component other) => other != null && other.transform.IsChildOf(transform);

        /// <summary>Whether this is the object a vision sensor reported, whichever part it named.</summary>
        public bool Represents(GameObject target)
        {
            if (target == null) return false;
            if (Recordable != null && Recordable.gameObject == target) return true;

            return target.transform.IsChildOf(transform);
        }

        /// <summary>
        /// Meshes only. Particles, trails and lines stretch the bounds far past the object and
        /// would make the brackets swim around a prop that is standing still.
        /// </summary>
        private Renderer[] CollectMeshRenderers()
        {
            List<Renderer> meshes = new();

            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
            {
                if (candidate is MeshRenderer || candidate is SkinnedMeshRenderer)
                    meshes.Add(candidate);
            }

            return meshes.ToArray();
        }
    }
}
