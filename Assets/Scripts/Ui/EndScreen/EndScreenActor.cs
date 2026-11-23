using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ui.EndScreen
{
    /// <summary>
    /// One player model in an end scene (just the mesh and an Animator, not the network prefab):
    /// either a body on the floor or a survivor walking out. States in its controller: Idle, Walk,
    /// Dead.
    /// </summary>
    public class EndScreenActor : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Idle");
        private static readonly int WalkState = Animator.StringToHash("Walk");
        private static readonly int DeadState = Animator.StringToHash("Dead");

        [SerializeField] private Animator animator;

        private EndScreenNameTag _tag;
        private Coroutine _jolt;
        private Vector3 _restPosition;
        private Quaternion _restRotation;

        public string PlayerName { get; private set; }
        public bool IsDead { get; private set; }
        public bool HasLeft { get; private set; }
        public Animator Animator => animator;

        /// <summary>Middle of the body, for whoever wants to walk up to it or click it.</summary>
        public Vector3 Center
        {
            get
            {
                Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                return hips != null ? hips.position : transform.position + Vector3.up * 0.5f;
            }
        }

        public void Setup(string playerName, EndScreenNameTagStyle style)
        {
            PlayerName = playerName;
            if (animator == null) animator = GetComponentInChildren<Animator>();

            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            _tag = EndScreenNameTag.Create(head != null ? head : transform, style.offset, playerName, style.font, style.material);
        }

        public void LieDead(float startTime)
        {
            IsDead = true;
            // Straight to the end of the fall: they have been down for a while.
            animator.Play(DeadState, 0, Mathf.Clamp01(startTime));
            _restPosition = transform.position;
            _restRotation = transform.rotation;
        }

        /// <summary>A shove from <paramref name="from"/>: the body rocks away and settles.</summary>
        public void Jolt(Vector3 from, float strength = 1f)
        {
            if (!IsDead) return;
            if (_jolt != null) StopCoroutine(_jolt);
            _jolt = StartCoroutine(JoltRoutine(from, strength));
        }

        private IEnumerator JoltRoutine(Vector3 from, float strength)
        {
            Vector3 away = transform.position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;

            // A body that is poked a lot slowly drifts: it keeps a bit of every shove.
            Vector3 settled = _restPosition + away * (0.04f * strength);
            Quaternion settledRotation = _restRotation * Quaternion.Euler(0f, Random.Range(-6f, 6f) * strength, 0f);

            const float duration = 0.55f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;
                // Quick push, then a damped wobble back to the new rest.
                float push = Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.16f * strength;
                float wobble = Mathf.Sin(t * Mathf.PI * 5f) * (1f - t) * 4f * strength;

                transform.position = Vector3.Lerp(_restPosition, settled, t) + away * push + Vector3.up * push * 0.4f;
                transform.rotation = Quaternion.Slerp(_restRotation, settledRotation, t) * Quaternion.Euler(wobble, 0f, wobble * 0.5f);
                yield return null;
            }

            _restPosition = settled;
            _restRotation = settledRotation;
            transform.SetPositionAndRotation(settled, settledRotation);
            _jolt = null;
        }

        /// <summary>
        /// Walks the path; at <paramref name="lookBackAt"/> (an index in the path, -1 for none) stops,
        /// turns to the camera for a moment and goes on. Past <paramref name="hideTagAt"/> the name
        /// fades; at the end the model is gone.
        /// </summary>
        public IEnumerator WalkPath(IList<Vector3> path, float speed, int lookBackAt, float lookBackTime, int hideTagAt)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 target = path[i];

                animator.CrossFade(WalkState, 0.2f);

                while (true)
                {
                    Vector3 to = target - transform.position;
                    Vector3 flat = new Vector3(to.x, 0f, to.z);
                    if (flat.magnitude < 0.05f) break;

                    Quaternion facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * Time.deltaTime);

                    float step = Mathf.Min(speed * Time.deltaTime, flat.magnitude);
                    Vector3 next = transform.position + flat.normalized * step;
                    next.y = Mathf.MoveTowards(transform.position.y, target.y, speed * Time.deltaTime);
                    transform.position = next;
                    yield return null;
                }

                if (i == hideTagAt && _tag != null) _tag.SetVisible(false);

                if (i == lookBackAt && lookBackTime > 0f)
                {
                    yield return LookBack(lookBackTime);
                }
            }

            animator.CrossFade(IdleState, 0.2f);
            HasLeft = true;
            if (_tag != null) _tag.SetVisible(false);

            yield return new WaitForSeconds(0.4f);
            gameObject.SetActive(false);
        }

        private IEnumerator LookBack(float duration)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Quaternion walking = transform.rotation;
            Vector3 toCamera = cam.transform.position - transform.position;
            toCamera.y = 0f;
            Quaternion looking = Quaternion.LookRotation(toCamera.normalized, Vector3.up);

            animator.CrossFade(IdleState, 0.25f);
            yield return Turn(walking, looking, 0.45f);
            yield return new WaitForSeconds(duration);
            yield return Turn(looking, walking, 0.45f);
        }

        private IEnumerator Turn(Quaternion from, Quaternion to, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, time / duration));
                yield return null;
            }

            transform.rotation = to;
        }

        // The walk clip carries footstep events for the in-game player; nothing to do with them here.
        private void AnimationFootstep() { }

        private void OnDisable()
        {
            if (_tag != null && HasLeft) _tag.gameObject.SetActive(false);
        }
    }

    [System.Serializable]
    public struct EndScreenNameTagStyle
    {
        public TMPro.TMP_FontAsset font;
        public Material material;
        [Tooltip("From the head bone.")]
        public Vector3 offset;
    }
}
