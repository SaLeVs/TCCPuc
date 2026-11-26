using UnityEngine;
using UnityEngine.AI;

namespace Player
{
    /// <summary>
    /// Finds a spot where the player's standing capsule fits, starting from where the ragdoll ended
    /// up and spreading outwards on the XZ plane.
    /// </summary>
    public static class StandingSpotFinder
    {
        private const float CapsuleSlack = 0.95f;
        private const float FloorClearance = 0.05f;

        // The ragdoll's hips lie close to the floor; lift the start of the sight line off it.
        private const float BodySightLift = 0.25f;

        // How far below the hips the floor the body lies on is looked for.
        private const float BodyFloorProbeLift = 0.3f;
        private const float BodyFloorProbeDistance = 3f;

        // A spot may sit at most this much above or below the floor the body lies on. The ground
        // probe used to start 2.5 m over the candidate and take whatever it met first, so the
        // player stood up on top of boxes, sofas, door leaves and even the walls themselves.
        private const float MaxStepUp = 0.35f;
        private const float MaxStepDown = 0.6f;

        // A spot has to be on the NavMesh, walkable from where the player stood before going down.
        // The NavMesh only covers real floor, so this is what turns down the tops of boxes,
        // toilets, sinks and tables the body landed on.
        private const float ReferenceNavMeshSnap = 1f;
        private const float SpotNavMeshSnap = 0.5f;
        private const float SpotNavMeshMaxRise = 0.3f;
        private const float SpotNavMeshMaxFlat = 0.35f;

        private static readonly RaycastHit[] FloorHits = new RaycastHit[16];
        private static NavMeshPath _path;

        private static int _groundLayer = -2;

        private static int GroundLayer
        {
            get
            {
                if (_groundLayer == -2) _groundLayer = LayerMask.NameToLayer("Ground");
                return _groundLayer;
            }
        }

        // Never something to stand on, even when it happens to be under the candidate.
        private static int _notGroundLayers = -1;

        private static int NotGroundLayers
        {
            get
            {
                if (_notGroundLayers < 0) _notGroundLayers = LayerMask.GetMask("Player", "Monster", "Doors");
                return _notGroundLayers;
            }
        }

        /// <summary>
        /// Searches ring by ring, so the first spot that fits is also the closest one.
        /// Returns false when nothing within maxDistance can hold the player.
        /// </summary>
        /// <param name="lineOfSightMask">
        /// What may not stand between the body and the spot. A spot is only taken if the body could
        /// have got there in a straight line — the ring search is by distance alone, and the
        /// nearest free floor was often on the far side of a shut door or a wall, which stood the
        /// player up on the other side of it.
        /// </param>
        /// <param name="walkableFrom">
        /// Where the player last stood, known to be walkable. When it is on the NavMesh, a spot is
        /// only taken if the NavMesh reaches it from there; null skips that check.
        /// </param>
        public static bool TryFind(Vector3 origin, float radius, float height, LayerMask blockingMask,
            LayerMask lineOfSightMask, float maxDistance, float stepSize, float angleStep, Vector3? walkableFrom,
            out Vector3 result)
        {
            int groundMask = blockingMask & ~NotGroundLayers;
            Walkable walkable = Walkable.From(walkableFrom);
            float floorY = BodyFloorY(origin, groundMask, walkable);

            if (TryPlace(origin, origin, floorY, radius, height, blockingMask, groundMask, lineOfSightMask, walkable,
                    out result))
            {
                return true;
            }

            stepSize = Mathf.Max(0.1f, stepSize);
            angleStep = Mathf.Clamp(angleStep, 1f, 180f);

            int steps = Mathf.CeilToInt(maxDistance / stepSize);

            for (int step = 1; step <= steps; step++)
            {
                float distance = step * stepSize;

                for (float angle = 0f; angle < 360f; angle += angleStep)
                {
                    float radians = angle * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * distance;

                    if (TryPlace(origin, origin + offset, floorY, radius, height, blockingMask, groundMask,
                            lineOfSightMask, walkable, out result))
                    {
                        return true;
                    }
                }
            }

            result = origin;
            return false;
        }

        /// <summary>
        /// The walkable floor under the body, or a little under the hips when there is none. A body
        /// lying across a box, a toilet or a sink has that under it first; taken as the floor, the
        /// search only accepted spots at that height and stood the player up on top of it.
        /// </summary>
        private static float BodyFloorY(Vector3 origin, int groundMask, Walkable walkable)
        {
            int count = Physics.RaycastNonAlloc(origin + Vector3.up * BodyFloorProbeLift, Vector3.down, FloorHits,
                BodyFloorProbeDistance, groundMask, QueryTriggerInteraction.Ignore);

            if (count == 0) return origin.y - BodySightLift;

            // The floor itself is on the Ground layer. Right under a prop there is no NavMesh (the
            // prop blocks it), so that cannot tell the floor apart there; it only helps when no
            // Ground was hit at all.
            int groundLayer = GroundLayer;
            int nearest = 0;
            int nearestGround = -1;
            int nearestWalkable = -1;

            for (int i = 0; i < count; i++)
            {
                if (FloorHits[i].distance < FloorHits[nearest].distance) nearest = i;

                if (FloorHits[i].collider.gameObject.layer == groundLayer &&
                    (nearestGround < 0 || FloorHits[i].distance < FloorHits[nearestGround].distance))
                {
                    nearestGround = i;
                }
            }

            if (nearestGround >= 0) return FloorHits[nearestGround].point.y;

            for (int i = 0; i < count; i++)
            {
                if ((nearestWalkable < 0 || FloorHits[i].distance < FloorHits[nearestWalkable].distance) &&
                    walkable.Allows(FloorHits[i].point))
                {
                    nearestWalkable = i;
                }
            }

            return FloorHits[nearestWalkable >= 0 ? nearestWalkable : nearest].point.y;
        }

        private static bool TryPlace(Vector3 origin, Vector3 candidate, float floorY, float radius, float height,
            LayerMask blockingMask, int groundMask, LayerMask lineOfSightMask, Walkable walkable, out Vector3 placed)
        {
            placed = candidate;

            // There has to be a floor under the candidate, at about the height of the body's own.
            // Starting the probe just one step over that floor means anything taller — a wall, a
            // cabinet, a box — has the probe start inside it, so the probe passes through and finds
            // the floor at its foot, where the capsule check below then turns the spot down.
            Vector3 probeStart = new Vector3(candidate.x, floorY + MaxStepUp + FloorClearance, candidate.z);

            if (!Physics.Raycast(probeStart, Vector3.down, out RaycastHit ground, MaxStepUp + FloorClearance + MaxStepDown,
                    groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            placed = ground.point;

            // Then the standing capsule has to fit there without overlapping anything.
            float bottomY = radius + FloorClearance;
            float topY = Mathf.Max(bottomY, height - radius);

            Vector3 bottom = placed + Vector3.up * bottomY;
            Vector3 top = placed + Vector3.up * topY;

            if (Physics.CheckCapsule(bottom, top, radius * CapsuleSlack, blockingMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // And the body has to be able to see it: nothing solid between where it lies and the
            // capsule it would stand up in. Its bottom or its top will do — a body lying on a sink
            // or a toilet sees the floor in front of it only over the edge of what it lies on.
            Vector3 sightStart = origin + Vector3.up * BodySightLift;

            if (Physics.Linecast(sightStart, bottom, lineOfSightMask, QueryTriggerInteraction.Ignore) &&
                Physics.Linecast(sightStart, top, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Last, being the dearest: real floor, reachable on foot from where the player stood.
            return walkable.Allows(placed);
        }

        /// <summary>
        /// Whether a floor point is somewhere the player could have walked to. Inactive — everything
        /// passes — when there is no reference, or the reference is off the NavMesh (a scene without
        /// one, or a spot only the player fits, like a duct).
        /// </summary>
        private readonly struct Walkable
        {
            private readonly bool _active;
            private readonly Vector3 _from;

            private Walkable(Vector3 from)
            {
                _active = true;
                _from = from;
            }

            public static Walkable From(Vector3? reference)
            {
                if (reference == null) return default;

                if (!NavMesh.SamplePosition(reference.Value, out NavMeshHit hit, ReferenceNavMeshSnap, NavMesh.AllAreas))
                {
                    return default;
                }

                return new Walkable(hit.position);
            }

            public bool Allows(Vector3 floorPoint)
            {
                if (!_active) return true;

                if (!NavMesh.SamplePosition(floorPoint, out NavMeshHit hit, SpotNavMeshSnap, NavMesh.AllAreas))
                {
                    return false;
                }

                if (Mathf.Abs(hit.position.y - floorPoint.y) > SpotNavMeshMaxRise) return false;

                Vector2 flat = new Vector2(hit.position.x - floorPoint.x, hit.position.z - floorPoint.z);
                if (flat.magnitude > SpotNavMeshMaxFlat) return false;

                // A table or a big box can carry a scrap of NavMesh on top that nothing walks onto.
                _path ??= new NavMeshPath();
                return NavMesh.CalculatePath(_from, hit.position, NavMesh.AllAreas, _path) &&
                       _path.status == NavMeshPathStatus.PathComplete;
            }
        }
    }
}
