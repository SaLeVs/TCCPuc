using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class PatrolSector : MonoBehaviour
    {
        [SerializeField] private float patrolSectorRadius;
        [SerializeField] private Color patrolRadiusColor;

        [Tooltip("Random points tried per call before giving up until the next wander interval. " +
                 "Corridor sectors are mostly wall, so most single draws miss the floor.")]
        [SerializeField, Min(1)] private int pointAttempts = 8;

        [Tooltip("How far a random point may be snapped onto the NavMesh. Large enough to follow " +
                 "the auditorium's sloped seating, small enough not to reach the balcony above it.")]
        [SerializeField, Min(0.5f)] private float pointSnapDistance = 4f;

        [Tooltip("A point is rejected when walking to it from the sector's centre is longer than " +
                 "this many radii — it is behind a wall, in the next room or on another floor.")]
        [SerializeField, Min(1f)] private float maxPathLengthInRadii = 1.5f;

        public Vector3 Position => transform.position;

        private NavMeshPath _path;


        /// <summary>
        /// A failed SamplePosition leaves hit.position at infinity, and feeding that to the agent
        /// is what produced the "setting destination to infinity is ignored" spam — the monster
        /// then kept its old path and stuttered. Returns false instead of a garbage point.
        ///
        /// <para>Only points the monster can actually walk to from the sector's centre, on a
        /// reasonably short path, are accepted. The map stacks floors (seating under the
        /// balcony, corridors under the ticket office) and rooms sit wall-to-wall with
        /// corridors, so the nearest NavMesh to a random point is often unreachable or a long
        /// detour away.</para>
        /// </summary>
        public bool TryGetRandomPointInSector(out Vector3 point)
        {
            point = transform.position;

            // Snapped on every call rather than cached: the mission rooms only get floor once
            // SpawnRooms has spawned them and rebuilt the NavMesh.
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit centre, patrolSectorRadius, 1))
            {
                return false;
            }

            _path ??= new NavMeshPath();
            float maxPathLength = patrolSectorRadius * maxPathLengthInRadii;

            for (int i = 0; i < pointAttempts; i++)
            {
                // A flat disc, not a sphere: a vertical offset is what dropped points onto the
                // floor above or below the sector.
                Vector2 offset = Random.insideUnitCircle * patrolSectorRadius;
                Vector3 candidate = centre.position + new Vector3(offset.x, 0f, offset.y);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, pointSnapDistance, 1)) continue;
                if (!NavMesh.CalculatePath(centre.position, hit.position, NavMesh.AllAreas, _path)) continue;
                if (_path.status != NavMeshPathStatus.PathComplete) continue;
                if (GetPathLength(_path) > maxPathLength) continue;

                point = hit.position;
                return true;
            }

            return false;
        }

        private static float GetPathLength(NavMeshPath path)
        {
            Vector3[] corners = path.corners;
            float length = 0f;

            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = patrolRadiusColor;
            Gizmos.DrawSphere(transform.position, patrolSectorRadius);
        }
    }
}