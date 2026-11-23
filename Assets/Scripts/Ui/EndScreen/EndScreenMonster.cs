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
            Vector3 from = body - transform.position;
            from.y = 0f;
            Vector3 stand = body - from.normalized * pokeDistance;
            stand.y = transform.position.y;

            Vector3 flat = stand - transform.position;
            flat.y = 0f;

            if (flat.magnitude > 0.15f)
            {
                yield return TurnTowards(stand);
                Play(WalkState);

                while (true)
                {
                    if (_roarRequested) yield break;

                    flat = stand - transform.position;
                    flat.y = 0f;
                    if (flat.magnitude < 0.05f) break;

                    Quaternion facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
                    transform.position += flat.normalized * Mathf.Min(walkSpeed * Time.deltaTime, flat.magnitude);
                    yield return null;
                }
            }

            Play(IdleState);
            yield return TurnTowards(body);
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

        private IEnumerator TurnTowards(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) yield break;

            Quaternion facing = Quaternion.LookRotation(to.normalized, Vector3.up);
            while (Quaternion.Angle(transform.rotation, facing) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.rotation = facing;
        }

        private void Play(int state, float fade = 0.25f) => animator.CrossFadeInFixedTime(state, fade);

        // The walk clip carries footstep events for the in-game monster; nothing to do with them here.
        private void AnimationFootstep() { }
    }
}
