using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ui.EndScreen
{
    /// <summary>
    /// The monster in the Lose scene, enjoying itself: walks from body to body, pokes each one
    /// (the body rocks), roars over it, looks around, and now and then turns to the camera to roar
    /// at whoever is watching.
    ///
    /// <para>Clicking the monster makes it roar at the camera right away; clicking a body sends it
    /// to poke that one next.</para>
    ///
    /// <para>States in its controller: Idle, Walk, Poke, Roar, LookLeft, LookRight.</para>
    /// </summary>
    public class EndScreenMonster : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Idle");
        private static readonly int WalkState = Animator.StringToHash("Walk");
        private static readonly int PokeState = Animator.StringToHash("Poke");
        private static readonly int RoarState = Animator.StringToHash("Roar");
        private static readonly int LookLeftState = Animator.StringToHash("LookLeft");
        private static readonly int LookRightState = Animator.StringToHash("LookRight");

        [SerializeField] private Animator animator;
        [SerializeField] private EndScreenCast cast;

        [Header("Moves")]
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float turnSpeed = 220f;

        [Tooltip("Metres a second the walk clip's feet cover at normal speed (MutantWalk: about 1.27). " +
                 "The clip is sped up or slowed down to match Walk Speed, so the feet do not skate.")]
        [SerializeField, Min(0.01f)] private float walkClipSpeed = 1.27f;

        [Tooltip("Degrees a second when turning without walking anywhere.")]
        [SerializeField] private float turnInPlaceSpeed = 110f;

        [Tooltip("How far it keeps its feet from any body on the floor, in metres.")]
        [SerializeField, Min(0f)] private float bodyClearance = 0.45f;
        [Tooltip("How far from a body's middle it stands to poke it.")]
        [SerializeField] private float pokeDistance = 1.05f;
        [SerializeField] private float pokeDuration = 2.2f;
        [Tooltip("Into the poke clip, when the hand reaches the body (0-1).")]
        [SerializeField, Range(0f, 1f)] private float pokeHitAt = 0.45f;
        [SerializeField] private float roarDuration = 4.2f;
        [SerializeField] private float lookDuration = 2.4f;

        [Header("Rhythm")]
        [SerializeField] private float startDelay = 0.6f;
        [Tooltip("Chance, after a body, of turning to roar at the camera.")]
        [SerializeField, Range(0f, 1f)] private float roarAtCameraChance = 0.35f;
        [Tooltip("Chance of poking the same body a second time.")]
        [SerializeField, Range(0f, 1f)] private float pokeAgainChance = 0.3f;

        [Header("Clicks")]
        [Tooltip("How close (fraction of screen height) a click must land to the monster or a body.")]
        [SerializeField] private float clickRadius = 0.09f;

        private EndScreenActor _requested;
        private bool _roarRequested;
        private EndScreenActor _last;
        private Coroutine _routine;

        private void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _routine = StartCoroutine(Run());
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector2 click = mouse.position.ReadValue();
            float radius = clickRadius * Screen.height;

            // The monster first: it is the one standing in front.
            if (IsNear(cam, Center, click, radius * 1.6f))
            {
                _roarRequested = true;
                return;
            }

            if (cast == null) return;

            EndScreenActor closest = null;
            float best = radius;
            foreach (EndScreenActor actor in cast.Actors)
            {
                Vector3 screen = cam.WorldToScreenPoint(actor.Center);
                if (screen.z <= 0f) continue;
                float distance = Vector2.Distance(click, screen);
                if (distance < best)
                {
                    best = distance;
                    closest = actor;
                }
            }

            if (closest != null) _requested = closest;
        }

        private Vector3 Center
        {
            get
            {
                Transform chest = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
                return chest != null ? chest.position : transform.position + Vector3.up * 1.2f;
            }
        }

        private static bool IsNear(Camera cam, Vector3 world, Vector2 click, float radius)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            return screen.z > 0f && Vector2.Distance(click, screen) < radius;
        }

        private IEnumerator Run()
        {
            Play(IdleState);
            yield return new WaitForSeconds(startDelay);

            // Opens with a roar at the camera: it knows it is being filmed.
            yield return RoarAtCamera();

            while (true)
            {
                if (_roarRequested)
                {
                    yield return RoarAtCamera();
                    continue;
                }

                EndScreenActor target = PickTarget();
                if (target == null)
                {
                    yield return LookAround();
                    continue;
                }

                yield return WalkTo(target);
                if (_roarRequested) continue;

                yield return Poke(target);
                _last = target;

                if (Random.value < pokeAgainChance && _requested == null)
                {
                    yield return Poke(target);
                }

                if (_requested != null || _roarRequested) continue;

                if (Random.value < roarAtCameraChance)
                {
                    yield return RoarAtCamera();
                }
                else if (Random.value < 0.5f)
                {
                    yield return RoarAt(target.Center);
                }
                else
                {
                    yield return LookAround();
                }
            }
        }

        private EndScreenActor PickTarget()
        {
            if (_requested != null)
            {
                EndScreenActor requested = _requested;
                _requested = null;
                return requested;
            }

            if (cast == null || cast.Actors.Count == 0) return null;

            List<EndScreenActor> options = new List<EndScreenActor>();
            foreach (EndScreenActor actor in cast.Actors)
            {
                if (actor != _last || cast.Actors.Count == 1) options.Add(actor);
            }

            return options[Random.Range(0, options.Count)];
        }

        private IEnumerator WalkTo(EndScreenActor target)
        {
            Vector3 body = target.Center;
            Vector3 stand = PickStandPoint(target);

            List<Vector3> route = new List<Vector3>();
            PlanRoute(transform.position, stand, target, route);

            Vector3 flat = stand - transform.position;
            flat.y = 0f;

            if (flat.magnitude > 0.15f)
            {
                // Sets off and turns on the way, like it walks: turning on the spot first slid the
                // whole idle pose round on the floor.
                Play(WalkState);
                SetWalkPace(true);

                foreach (Vector3 waypoint in route)
                {
                    while (true)
                    {
                        if (_roarRequested)
                        {
                            SetWalkPace(false);
                            yield break;
                        }

                        flat = waypoint - transform.position;
                        flat.y = 0f;
                        if (flat.magnitude < 0.05f) break;

                        Quaternion facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
                        float angle = Quaternion.Angle(transform.rotation, facing);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);

                        // Slows down into a sharp turn rather than skating sideways through it.
                        float pace = Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(20f, 90f, angle));
                        transform.position += flat.normalized * Mathf.Min(walkSpeed * pace * Time.deltaTime, flat.magnitude);
                        yield return null;
                    }
                }

                SetWalkPace(false);
            }

            Play(IdleState);
            yield return TurnTowards(body);
        }

        /// <summary>
        /// The walk clip covers <see cref="walkClipSpeed"/> metres a second; the animation is played
        /// at whatever rate makes its feet keep up with <see cref="walkSpeed"/>, so they stay planted.
        /// </summary>
        private void SetWalkPace(bool walking)
        {
            if (animator == null) return;

            animator.speed = walking && walkClipSpeed > 0.01f ? walkSpeed / walkClipSpeed : 1f;
        }

        /// <summary>
        /// Somewhere at poking distance from the body, on the side that needs the least walking,
        /// standing clear of every other body and on the stage.
        /// </summary>
        private Vector3 PickStandPoint(EndScreenActor target)
        {
            Vector3 body = target.Center;
            body.y = transform.position.y;

            Vector3 best = body - Flat(body - transform.position).normalized * pokeDistance;
            float bestScore = float.MaxValue;

            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                Vector3 candidate = body + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * pokeDistance;
                candidate.y = transform.position.y;

                if (!HasFloor(candidate)) continue;
                if (ClearanceFromBodies(candidate, null) < bodyClearance) continue;

                float score = Flat(candidate - transform.position).magnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Straight there when nothing is in the way; otherwise one step aside around the body that
        /// blocks, whichever side is shorter and on the stage. Enough for a handful of bodies.
        /// </summary>
        private void PlanRoute(Vector3 from, Vector3 to, EndScreenActor target, List<Vector3> route)
        {
            route.Clear();

            Vector3 lastPoint = from;
            for (int guard = 0; guard < 4; guard++)
            {
                if (!TryFindBlocker(lastPoint, to, out Vector3 blockerPoint))
                {
                    break;
                }

                Vector3 dir = Flat(to - lastPoint).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float push = bodyClearance + 0.35f;

                Vector3 left = blockerPoint + side * push;
                Vector3 right = blockerPoint - side * push;
                left.y = right.y = transform.position.y;

                bool leftOk = HasFloor(left) && ClearanceFromBodies(left, null) >= bodyClearance;
                bool rightOk = HasFloor(right) && ClearanceFromBodies(right, null) >= bodyClearance;
                if (!leftOk && !rightOk) break;

                Vector3 detour = !rightOk || (leftOk && PathLength(lastPoint, left, to) <= PathLength(lastPoint, right, to))
                    ? left
                    : right;

                route.Add(detour);
                lastPoint = detour;
            }

            route.Add(to);
        }

        private bool TryFindBlocker(Vector3 a, Vector3 b, out Vector3 point)
        {
            point = default;
            if (cast == null) return false;

            float nearest = float.MaxValue;

            foreach (EndScreenActor actor in cast.Actors)
            {
                if (actor == null || !actor.isActiveAndEnabled) continue;

                actor.GetFootprint(out Vector3 feet, out Vector3 head);

                // Ignore the last stretch: the walk ends next to a body on purpose.
                Vector3 end = Vector3.MoveTowards(b, a, 0.3f);
                float distance = SegmentDistance(a, end, feet, head, out Vector3 onPath, out Vector3 onBody);

                if (distance >= bodyClearance) continue;

                float along = Flat(onPath - a).magnitude;
                if (along < nearest)
                {
                    nearest = along;
                    point = onBody;
                }
            }

            return nearest < float.MaxValue;
        }

        private float ClearanceFromBodies(Vector3 point, EndScreenActor ignore)
        {
            float clearance = float.MaxValue;
            if (cast == null) return clearance;

            foreach (EndScreenActor actor in cast.Actors)
            {
                if (actor == null || actor == ignore || !actor.isActiveAndEnabled) continue;

                actor.GetFootprint(out Vector3 feet, out Vector3 head);
                clearance = Mathf.Min(clearance, SegmentDistance(point, point, feet, head, out _, out _));
            }

            return clearance;
        }

        private bool HasFloor(Vector3 point)
        {
            Vector3 probe = point + Vector3.up * 1f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Ignore)) return false;

            // The stage, not a step down off its edge nor a prop to climb.
            return Mathf.Abs(hit.point.y - transform.position.y) < 0.2f;
        }

        private static float PathLength(Vector3 a, Vector3 via, Vector3 b) =>
            Flat(via - a).magnitude + Flat(b - via).magnitude;

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Closest distance between two segments, flat on the floor.</summary>
        private static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2,
            out Vector3 c1, out Vector3 c2)
        {
            p1.y = q1.y = p2.y = q2.y = 0f;

            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float s, t;

            if (a <= 1e-6f && e <= 1e-6f)
            {
                s = t = 0f;
            }
            else if (a <= 1e-6f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 1e-6f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return Vector3.Distance(c1, c2);
        }

        private IEnumerator Poke(EndScreenActor target)
        {
            yield return TurnTowards(target.Center);
            Play(PokeState, 0.15f);

            float hitTime = pokeDuration * pokeHitAt;
            yield return new WaitForSeconds(hitTime);
            target.Jolt(transform.position, 1f);
            yield return new WaitForSeconds(pokeDuration - hitTime);

            Play(IdleState);
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator RoarAtCamera()
        {
            _roarRequested = false;
            Camera cam = Camera.main;
            if (cam != null) yield return RoarAt(cam.transform.position);
        }

        private IEnumerator RoarAt(Vector3 point)
        {
            yield return TurnTowards(point);
            Play(RoarState, 0.15f);
            yield return new WaitForSeconds(roarDuration);
            Play(IdleState);
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator LookAround()
        {
            Play(Random.value < 0.5f ? LookLeftState : LookRightState);
            float time = 0f;
            while (time < lookDuration && !_roarRequested && _requested == null)
            {
                time += Time.deltaTime;
                yield return null;
            }

            Play(IdleState);
        }

        /// <summary>
        /// Turns on the spot. A big turn steps round with the walk at half pace instead of
        /// spinning the idle pose, which read as the monster sliding on ice; a small one is just a
        /// slow shift of weight.
        /// </summary>
        private IEnumerator TurnTowards(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) yield break;

            Quaternion facing = Quaternion.LookRotation(to.normalized, Vector3.up);
            float angle = Quaternion.Angle(transform.rotation, facing);
            if (angle <= 1f)
            {
                transform.rotation = facing;
                yield break;
            }

            bool stepping = angle > 25f;
            if (stepping)
            {
                Play(WalkState, 0.2f);
                if (animator != null) animator.speed = 0.55f;
            }

            float speed = stepping ? turnInPlaceSpeed : turnInPlaceSpeed * 0.5f;
            while (Quaternion.Angle(transform.rotation, facing) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, speed * Time.deltaTime);
                yield return null;
            }

            transform.rotation = facing;

            if (stepping)
            {
                if (animator != null) animator.speed = 1f;
                Play(IdleState, 0.2f);
            }
        }

        private void Play(int state, float fade = 0.25f) => animator.CrossFadeInFixedTime(state, fade);

        // The walk clip carries footstep events for the in-game monster; nothing to do with them here.
        private void AnimationFootstep() { }
    }
}
