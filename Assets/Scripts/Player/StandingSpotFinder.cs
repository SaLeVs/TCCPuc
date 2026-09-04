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

        /// <summary>
        /// Searches ring by ring, so the first spot that fits is also the closest one.
        /// Returns false when nothing within maxDistance can hold the player.
        /// </summary>
        public static bool TryFind(Vector3 origin, float radius, float height, LayerMask blockingMask,
            float maxDistance, float stepSize, float angleStep, out Vector3 result)
        {
            if (TryPlace(origin, radius, height, blockingMask, out result))
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

                    if (TryPlace(origin + offset, radius, height, blockingMask, out result))
                    {
                        return true;
                    }
                }
            }

            result = origin;
            return false;
        }

        private static bool TryPlace(Vector3 candidate, float radius, float height, LayerMask blockingMask, out Vector3 placed)
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

            return !Physics.CheckCapsule(bottom, top, radius * CapsuleSlack, blockingMask, QueryTriggerInteraction.Ignore);
        }
    }
}
