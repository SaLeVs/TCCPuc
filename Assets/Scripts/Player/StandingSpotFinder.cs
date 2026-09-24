using UnityEngine;

namespace Player
{
    /// <summary>
    /// Finds a spot where the player's standing capsule fits, starting from where the ragdoll ended
    /// up and spreading outwards on the XZ plane.
    /// </summary>
    public static class StandingSpotFinder
    {
        private const float GroundProbeHeight = 2.5f;
        private const float CapsuleSlack = 0.95f;
        private const float FloorClearance = 0.05f;

        // The ragdoll's hips lie close to the floor; lift the start of the sight line off it.
        private const float BodySightLift = 0.25f;

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
        public static bool TryFind(Vector3 origin, float radius, float height, LayerMask blockingMask,
            LayerMask lineOfSightMask, float maxDistance, float stepSize, float angleStep, out Vector3 result)
        {
            if (TryPlace(origin, origin, radius, height, blockingMask, lineOfSightMask, out result))
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

                    if (TryPlace(origin, origin + offset, radius, height, blockingMask, lineOfSightMask, out result))
                    {
                        return true;
                    }
                }
            }

            result = origin;
            return false;
        }

        private static bool TryPlace(Vector3 origin, Vector3 candidate, float radius, float height,
            LayerMask blockingMask, LayerMask lineOfSightMask, out Vector3 placed)
        {
            placed = candidate;

            // There has to be a floor under the candidate before anything can stand on it.
            Vector3 probeStart = candidate + Vector3.up * GroundProbeHeight;

            if (!Physics.Raycast(probeStart, Vector3.down, out RaycastHit ground, GroundProbeHeight * 2f,
                    blockingMask, QueryTriggerInteraction.Ignore))
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
            // bottom of the capsule it would stand up in.
            return !Physics.Linecast(origin + Vector3.up * BodySightLift, bottom, lineOfSightMask,
                QueryTriggerInteraction.Ignore);
        }
    }
}
