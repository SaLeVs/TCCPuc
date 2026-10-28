using Unity.Netcode;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Decides what the body does with the velocity the player wants: it follows the floor up and
    /// down steps and slopes, and stops at what it can't climb. PlayerMovement hands its velocity
    /// over every physics step through <see cref="ResolveVelocity"/>.
    ///
    /// <para>The capsule has no friction and a round bottom, so pushed into any ledge below its
    /// middle it rides up it like a ramp - over boxes and props, launched when running. So nothing
    /// steeper than <see cref="maxSlopeAngle"/> is ever pushed into: it is either a step on
    /// <see cref="stepLayerMask"/> no taller than <see cref="maxStepHeight"/> with room for the body
    /// on top, which is climbed as a short ramp, or a wall, which the move slides along.</para>
    /// </summary>
    public class PlayerStairClimber : NetworkBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private CapsuleCollider capsuleCollider;

        [Header("Steps")]
        [Tooltip("Tallest step that is climbed, from the feet. Anything taller is a wall.")]
        [SerializeField, Min(0f)] private float maxStepHeight = 0.3f;

        [Tooltip("Layers a step can be on. A step on any other layer is a wall.")]
        [SerializeField] private LayerMask stepLayerMask;

        [Tooltip("Narrowest top a step can have, measured in from its edge. Narrower ones - a rail, a cup, " +
                 "the lip of a box - are walls.")]
        [SerializeField, Min(0f)] private float minStepDepth = 0.15f;

        [Tooltip("Steps are taken as if they were a ramp this steep: the climb starts that far before the " +
                 "step, and the drop past an edge is spread the same way. Lower is smoother.")]
        [SerializeField, Range(10f, 80f)] private float stepRampAngle = 45f;

        [Header("Ground")]
        [Tooltip("Steepest floor that can be walked on. Steeper slopes are walls, and the body slides down them.")]
        [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 45f;

        // How much the casts shrink the capsule, so a surface it already touches is still found.
        private const float Skin = 0.02f;
        // A gap under the feet this small is standing; the rest is closed on the next step.
        private const float GroundedDistance = 0.05f;
        // The ground probe starts this far up inside the capsule, so a floor it sinks into is still hit.
        private const float ProbeLift = 0.05f;
        // How far past a step's edge its top is probed.
        private const float StepProbeInset = 0.03f;
        private const float StepLevelTolerance = 0.02f;
        private const float MinStepRise = 0.01f;
        // The quickest a step is risen onto, when it is found already at the feet.
        private const float MinStepTime = 0.04f;
        // A climb that isn't on top of the step by then is given up, so a blocked one can't hover.
        private const float StepTimeout = 0.5f;
        private const float MinMoveSpeed = 0.05f;
        private const float SlopeTolerance = 0.001f;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private int _collisionMask;

        private bool _isFollowingGround;
        private bool _isStepping;
        private float _stepTargetY;
        private float _stepRiseSpeed;
        private float _stepTimer;
        private float _lastResolveTime = float.MinValue;


        private void Awake()
        {
            // What the capsule collides with, minus players: they push each other, not stand on each other.
            for (int layer = 0; layer < 32; layer++)
            {
                if (layer != gameObject.layer && !Physics.GetIgnoreLayerCollision(gameObject.layer, layer))
                {
                    _collisionMask |= 1 << layer;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerState.OnPlayerDead  += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
            }
        }


        private void PlayerState_OnPlayerDead(bool isDead) => ResetState();
        private void PlayerState_OnPlayerLocked(bool isLocked) => ResetState();

        private void ResetState()
        {
            _isFollowingGround = false;
            _isStepping = false;
            _stepTimer = 0f;
        }

        /// <summary>
        /// The velocity the body should have this physics step, given the horizontal velocity the
        /// player wants. Owner only, from FixedUpdate.
        /// </summary>
        public Vector3 ResolveVelocity(Vector3 desiredVelocity)
        {
            float deltaTime = Time.fixedDeltaTime;

            // Skipped steps - locked, knocked down, teleported - leave nothing worth carrying over.
            if (Time.fixedTime - _lastResolveTime > deltaTime * 1.5f)
            {
                ResetState();
            }

            _lastResolveTime = Time.fixedTime;

            GetCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            float feetY = bottom.y - radius;
            // A hair under the cosine, so a slope of exactly maxSlopeAngle is still floor: built at
            // 45 degrees, its normal came out a rounding error steeper and the ramp was a wall.
            float minGroundNormalY = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad) - SlopeTolerance;

            bool hasGround = ProbeGround(bottom, radius, minGroundNormalY, out float groundGap, out Vector3 groundNormal,
                out float middleGap);
            bool isGrounded = hasGround && groundGap <= GroundedDistance;
            bool isOnMiddle = isGrounded && middleGap <= GroundedDistance;
            _isFollowingGround = isGrounded || (_isFollowingGround && hasGround && groundGap <= maxStepHeight + Skin);

            // Up a slope the look-ahead follows it, or it would hit the slope itself. Resting on an
            // edge it stays level: the edge's normal says nothing about where the floor goes.
            Vector3 move = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);
            Vector3 castDirection = (isOnMiddle ? AlongGround(move, groundNormal) : move).normalized;
            bool isOnStepTop = isOnMiddle && feetY - middleGap >= _stepTargetY - StepLevelTolerance;

            bool isStepping = ResolveObstacles(ref move, castDirection, bottom, top, radius, feetY,
                minGroundNormalY, isOnStepTop, deltaTime, out float riseSpeed);

            Vector3 velocity;

            if (isStepping)
            {
                velocity = new Vector3(move.x, riseSpeed, move.z);
            }
            else if (isGrounded)
            {
                // Along the floor, and down onto it: the gap is never left for gravity to close.
                velocity = AlongGround(move, groundNormal);
                velocity.y -= Mathf.Max(0f, groundGap) / deltaTime;
            }
            else if (_isFollowingGround)
            {
                // Walked off a step: down it like a ramp instead of dropping onto the next one, but
                // never slower than falling, so a player who stops over the edge still comes down -
                // and never past the floor below. Carrying the last step's fall speed sank a running
                // player into each tread, and the solver pushed them back up out of it.
                float followSpeed = Mathf.Min(groundGap / deltaTime,
                    move.magnitude * Mathf.Tan(stepRampAngle * Mathf.Deg2Rad));
                float fallSpeed = -(rb.linearVelocity.y + Physics.gravity.y * deltaTime);
                float downSpeed = Mathf.Min(Mathf.Max(followSpeed, fallSpeed), groundGap / deltaTime);
                velocity = new Vector3(move.x, -downSpeed, move.z);
            }
            else
            {
                // In the air: gravity has it.
                return new Vector3(move.x, rb.linearVelocity.y, move.z);
            }

            // The velocities above are what the body should end the step with, and gravity is
            // about to add its share. Left in, it would also slide a standing player down slopes.
            if (rb.useGravity)
            {
                velocity.y -= Physics.gravity.y * deltaTime;
            }

            return velocity;
        }

        /// <summary>
        /// Looks ahead along the move for anything steeper than a walkable slope. A step that can be
        /// taken gives the speed to rise at so the feet follow a ramp of <see cref="stepRampAngle"/>
        /// onto it; anything else is a wall, and the part of the move going into it is dropped as
        /// soon as the body would touch it. Returns whether the body is taking a step.
        /// </summary>
        private bool ResolveObstacles(ref Vector3 move, Vector3 castDirection, Vector3 bottom, Vector3 top,
            float radius, float feetY, float minGroundNormalY, bool isOnStepTop, float deltaTime,
            out float riseSpeed)
        {
            float speed = move.magnitude;
            float rampTangent = Mathf.Tan(stepRampAngle * Mathf.Deg2Rad);
            float riseTargetY = feetY;
            riseSpeed = 0f;

            if (speed > MinMoveSpeed)
            {
                float reach = Skin + speed * deltaTime + maxStepHeight / rampTangent;
                int count = Physics.CapsuleCastNonAlloc(bottom, top, radius - Skin, castDirection, _hits, reach,
                    _collisionMask, QueryTriggerInteraction.Ignore);
                SortByDistance(count);

                // Steps are chained, so on stairs steeper than the ramp the next one is already
                // being climbed while the body is still rising onto this one.
                float baseY = _isStepping ? Mathf.Max(feetY, _stepTargetY) : feetY;
                bool canStep = _isFollowingGround || _isStepping;

                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = _hits[i];

                    // Overlapping at the start: the solver is already pushing the body out of it.
                    if (hit.distance <= 0f && hit.point == Vector3.zero) continue;

                    // A collider that lets this body through, like the monster-only blocker under a
                    // counter flap, is no wall to it either.
                    if ((hit.collider.excludeLayers.value & (1 << gameObject.layer)) != 0) continue;

                    // A slope, or an edge low enough to roll over.
                    if (hit.normal.y >= minGroundNormalY) continue;

                    Vector3 away = new Vector3(hit.normal.x, 0f, hit.normal.z);
                    if (away.sqrMagnitude < 0.0001f) continue;
                    away.Normalize();

                    float approach = -Vector3.Dot(move, away);
                    if (approach <= MinMoveSpeed) continue;

                    // How far the real capsule is from touching it, straight towards it.
                    float distance = Mathf.Max(0f, hit.distance * -Vector3.Dot(castDirection, away) - Skin);

                    if (canStep && TryGetStepTop(hit, away, bottom, top, radius, feetY, baseY, minGroundNormalY,
                            out float stepTopY))
                    {
                        float rise = stepTopY - feetY;
                        float timeToStep = distance / approach;

                        // Reaching the ramp that ends at the step's edge during this physics step: rise
                        // to be on top by the time the edge is reached.
                        if (rise >= (distance - approach * deltaTime) * rampTangent)
                        {
                            riseSpeed = Mathf.Max(riseSpeed, rise / Mathf.Max(timeToStep, MinStepTime));
                            riseTargetY = Mathf.Max(riseTargetY, stepTopY);
                        }

                        baseY = Mathf.Max(baseY, stepTopY);
                        continue;
                    }

                    // A wall: keep the part of the move along it, and only go into it until touching.
                    float allowed = distance / deltaTime;

                    if (approach > allowed)
                    {
                        move += away * (approach - allowed);
                    }
                }
            }

            if (riseSpeed > 0f)
            {
                _isStepping = true;
                _stepTargetY = riseTargetY;
                _stepRiseSpeed = riseSpeed;
                _stepTimer = 0f;
                return true;
            }

            if (!_isStepping) return false;

            // Close to the top the edge stops looking like a step, and the body is up before it is
            // over it: finish the climb, then hold the height until the floor under it is the step.
            // Let down early, the round bottom would sink onto the edge and ride it up again.
            _stepTimer += deltaTime;

            if (isOnStepTop || _stepTimer >= StepTimeout || speed <= MinMoveSpeed)
            {
                _isStepping = false;
                return false;
            }

            riseSpeed = Mathf.Clamp((_stepTargetY - feetY) / deltaTime, 0f, _stepRiseSpeed);
            return true;
        }

        /// <summary>
        /// Whether what the cast hit is a step, and how high its top is. It has to be on the step
        /// layers and not moving, no more than <see cref="maxStepHeight"/> above
        /// <paramref name="baseY"/>, flat and deep enough to stand on, and the body has to fit on
        /// top of it with room to move over it.
        /// </summary>
        private bool TryGetStepTop(RaycastHit hit, Vector3 away, Vector3 bottom, Vector3 top, float radius,
            float feetY, float baseY, float minGroundNormalY, out float stepTopY)
        {
            stepTopY = 0f;

            if ((stepLayerMask.value & (1 << hit.collider.gameObject.layer)) == 0) return false;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) return false;

            // Probed from just above the tallest step allowed: a taller obstacle starts above the
            // ray, which then misses its top and finds only the floor.
            float probeY = baseY + maxStepHeight + Skin;
            float probeLength = probeY - feetY + Skin;

            if (!ProbeStepTop(hit.point - away * StepProbeInset, probeY, probeLength, minGroundNormalY,
                    out stepTopY))
            {
                return false;
            }

            if (stepTopY - feetY <= MinStepRise || stepTopY - baseY > maxStepHeight) return false;

            if (minStepDepth > 0f &&
                (!ProbeStepTop(hit.point - away * (StepProbeInset + minStepDepth), probeY, probeLength,
                     minGroundNormalY, out float deepTopY) ||
                 Mathf.Abs(deepTopY - stepTopY) > StepLevelTolerance))
            {
                return false;
            }

            Vector3 lift = Vector3.up * (stepTopY - feetY + Skin);
            float castRadius = radius - Skin;

            if (Physics.CheckCapsule(bottom + lift, top + lift, castRadius, _collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return !Physics.CapsuleCast(bottom + lift, top + lift, castRadius, -away, out RaycastHit blocked,
                       hit.distance + StepProbeInset + minStepDepth, _collisionMask, QueryTriggerInteraction.Ignore) ||
                   blocked.normal.y >= minGroundNormalY;
        }

        private bool ProbeStepTop(Vector3 point, float probeY, float probeLength, float minGroundNormalY,
            out float topY)
        {
            topY = 0f;
            Vector3 origin = new Vector3(point.x, probeY, point.z);

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit topHit, probeLength, _collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if ((stepLayerMask.value & (1 << topHit.collider.gameObject.layer)) == 0) return false;
            if (topHit.normal.y < minGroundNormalY) return false;

            topY = topHit.point.y;
            return true;
        }

        /// <summary>
        /// The floor under the capsule, as far down as a step can drop away: the closest walkable
        /// support under its round bottom, and the floor right under its middle. The middle's face
        /// gives the slope when it is what the body stands on; resting on an edge alone, the edge's
        /// normal does, so the body rolls over it.
        /// </summary>
        private bool ProbeGround(Vector3 bottom, float radius, float minGroundNormalY, out float gap,
            out Vector3 normal, out float middleGap)
        {
            float reach = maxStepHeight + Skin;
            gap = float.MaxValue;
            normal = Vector3.up;
            middleGap = float.MaxValue;

            if (Physics.Raycast(bottom, Vector3.down, out RaycastHit middleHit, radius + reach, _collisionMask,
                    QueryTriggerInteraction.Ignore) && middleHit.normal.y >= minGroundNormalY)
            {
                middleGap = middleHit.distance - radius;
                gap = middleGap;
                normal = middleHit.normal;
            }

            if (Physics.SphereCast(bottom + Vector3.up * ProbeLift, radius - Skin, Vector3.down, out RaycastHit hit,
                    ProbeLift + Skin + reach, _collisionMask, QueryTriggerInteraction.Ignore) &&
                hit.normal.y >= minGroundNormalY)
            {
                float supportGap = hit.distance - ProbeLift - Skin;

                if (supportGap < gap - GroundedDistance)
                {
                    normal = hit.normal;
                }

                gap = Mathf.Min(gap, supportGap);
            }

            return gap < float.MaxValue;
        }

        /// <summary>
        /// The move turned along the floor. Uphill it keeps its speed, so ramps don't launch it.
        /// Downhill it keeps its horizontal speed instead and only gains the drop: rolling over each
        /// edge on the way down stairs, the edge's tilted normal took up to a tenth of it.
        /// </summary>
        private static Vector3 AlongGround(Vector3 move, Vector3 groundNormal)
        {
            float downhill = groundNormal.x * move.x + groundNormal.z * move.z;

            if (downhill > 0f && groundNormal.y > 0.0001f)
            {
                return new Vector3(move.x, -downhill / groundNormal.y, move.z);
            }

            Vector3 along = Vector3.ProjectOnPlane(move, groundNormal);
            return along.sqrMagnitude > 0.000001f ? along.normalized * move.magnitude : Vector3.zero;
        }

        /// <summary>The capsule's sphere centres at the physics pose, which interpolation leaves the transform behind.</summary>
        private void GetCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            radius = capsuleCollider.radius;
            float halfSegment = Mathf.Max(0f, capsuleCollider.height * 0.5f - radius);
            Vector3 center = rb.position + rb.rotation * capsuleCollider.center;
            Vector3 up = rb.rotation * Vector3.up;

            bottom = center - up * halfSegment;
            top = center + up * halfSegment;
        }

        private void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                int j = i - 1;

                while (j >= 0 && _hits[j].distance > hit.distance)
                {
                    _hits[j + 1] = _hits[j];
                    j--;
                }

                _hits[j + 1] = hit;
            }
        }


        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerState.OnPlayerDead  -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
            }
        }

    }
}
