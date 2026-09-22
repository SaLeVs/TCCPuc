using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    /// <summary>
    /// Every recordable in the level that is not a network object of its own, indexed so the server
    /// can name one to a client in a single lookup.
    ///
    /// <para>Replaces sending a <c>recordableId</c> string and having the receiver walk the scene
    /// for a match. That had two problems. The id lived on the prefab, so all 54 copies of a mask
    /// shared it and the receiver always resolved to whichever one the search happened to return
    /// first - every other copy was invisible to the audience and to the chat. And the search was a
    /// FindObjectsByType over the whole scene, run on every target entering or leaving the vision
    /// cone, in both directions.</para>
    ///
    /// <para>Position is the key because it is the one thing that is unique per instance and already
    /// identical on every peer: rooms spawn as network objects at a replicated position, and the
    /// scenery inside them sits at fixed local offsets. Quantised to five centimetres so tiny float
    /// differences cannot miss, with a nearest-match fallback in case one ever does.</para>
    /// </summary>
    public static class RecordableRegistry
    {
        /// <summary>Cells per metre. Five centimetres is far finer than any two props sit apart.</summary>
        private const float CellsPerMeter = 20f;

        /// <summary>How far the fallback will look when the exact cell is empty.</summary>
        private const float FallbackRadius = 0.2f;

        private static readonly Dictionary<Vector3Int, RecordableIdentifier> ByCell = new();
        private static readonly List<RecordableIdentifier> All = new();
        private static readonly HashSet<RecordableIdentifier> Known = new();

        public static int Count => All.Count;

        public static void Register(RecordableIdentifier identifier)
        {
            if (identifier == null) return;

            // Anything carrying its own NetworkObject is named by that id instead, so it never needs
            // resolving through here - and registering it would put a moving object into a map keyed
            // by where it happened to start.
            if (identifier.TryGetComponent(out NetworkObject _)) return;

            if (!Known.Add(identifier)) return;

            All.Add(identifier);

            Vector3Int cell = ToCell(identifier.transform.position);

            if (!ByCell.TryAdd(cell, identifier))
            {
                Debug.LogWarning($"{nameof(RecordableRegistry)}: {identifier.name} sits within five " +
                                 "centimetres of another recordable, so only one of them can be " +
                                 "resolved by sight. Move one of them apart.", identifier);
            }
        }

        public static void Unregister(RecordableIdentifier identifier)
        {
            if (identifier == null) return;

            if (!Known.Remove(identifier)) return;

            All.Remove(identifier);

            Vector3Int cell = ToCell(identifier.transform.position);

            if (ByCell.TryGetValue(cell, out RecordableIdentifier stored) && stored == identifier)
            {
                ByCell.Remove(cell);
            }
        }

        /// <summary>Finds the recordable standing at <paramref name="position"/>.</summary>
        public static bool TryResolve(Vector3 position, out RecordableIdentifier identifier)
        {
            if (ByCell.TryGetValue(ToCell(position), out identifier) && identifier != null) return true;

            return TryResolveNearest(position, out identifier);
        }

        /// <summary>
        /// Safety net for the case where the two peers disagree on a position by more than one
        /// cell. Linear, but it only runs when the direct lookup missed.
        /// </summary>
        private static bool TryResolveNearest(Vector3 position, out RecordableIdentifier identifier)
        {
            identifier = null;

            float bestDistance = FallbackRadius * FallbackRadius;

            foreach (RecordableIdentifier candidate in All)
            {
                if (candidate == null) continue;

                float distance = (candidate.transform.position - position).sqrMagnitude;

                if (distance > bestDistance) continue;

                bestDistance = distance;
                identifier = candidate;
            }

            return identifier != null;
        }

        private static Vector3Int ToCell(Vector3 position)
        {
            return new Vector3Int(
                Mathf.RoundToInt(position.x * CellsPerMeter),
                Mathf.RoundToInt(position.y * CellsPerMeter),
                Mathf.RoundToInt(position.z * CellsPerMeter));
        }

        /// <summary>
        /// Static state outlives a play session when the editor enters play mode without a domain
        /// reload, which would leave the previous run's destroyed objects in the map.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            ByCell.Clear();
            All.Clear();
            Known.Clear();
        }
    }
}
