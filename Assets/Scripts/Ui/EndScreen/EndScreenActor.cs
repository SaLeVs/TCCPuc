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
            _spawnPosition = transform.position;

            SettleOnFloor();
        }

        [Header("Lying on the floor")]
        [Tooltip("What the body lies on. Only used to find the floor's height under it.")]
        [SerializeField] private LayerMask floorMask = ~0;

        [Tooltip("Furthest the head is lifted off the floor, in degrees, so the hat lies on the stage " +
                 "instead of sinking into it.")]
        [SerializeField, Range(0f, 90f)] private float maxHeadLift = 60f;

        [Tooltip("How far, in total, the monster's pokes may push a body from where it fell.")]
        [SerializeField, Min(0f)] private float maxDrift = 0.08f;

        private Vector3 _spawnPosition;
        private Vector3 _feet;
        private Vector3 _head;

        /// <summary>
        /// The body from feet to head, flat on the floor, for whoever has to walk around it.
        /// </summary>
        public void GetFootprint(out Vector3 feet, out Vector3 head)
        {
            Vector3 shift = transform.position - _restPosition;
            feet = _feet + shift;
            head = _head + shift;
        }

        /// <summary>
        /// The death clip ends with the body face down and the head - hat and all - a good 25 cm
        /// inside the floor, and the chest 10 cm in: on camera the hat brim stood up out of the
        /// stage like a fin. Instead of physics (which is what made it twitch), the pose is
        /// frozen once and corrected once: the head comes up until it clears the body's own lowest
        /// point, then the whole body is lifted so its lowest vertex just touches the floor.
        /// </summary>
        private void SettleOnFloor()
        {
            if (animator == null) return;

            // Apply the end of the fall now and keep it: with the Animator off, nothing overwrites
            // the corrected bones afterwards.
            animator.Update(0f);
            animator.enabled = false;

            SkinnedMeshRenderer[] skins = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length == 0) return;

            Mesh baked = new Mesh();
            try
            {
                Transform hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                Transform neck = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
                Transform head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                SkinnedMeshRenderer headSkin = FindHeadSkin(skins, head);

                if (hips != null && head != null && headSkin != null)
                {
                    float bodyLowest = float.MaxValue;
                    foreach (SkinnedMeshRenderer skin in skins)
                    {
                        if (skin != headSkin) bodyLowest = Mathf.Min(bodyLowest, LowestPoint(skin, baked));
                    }

                    LiftHead(hips, neck, head, headSkin, bodyLowest, baked);
                }

                float lowest = float.MaxValue;
                foreach (SkinnedMeshRenderer skin in skins) lowest = Mathf.Min(lowest, LowestPoint(skin, baked));

                float floor = FloorHeight();
                if (lowest < float.MaxValue)
                {
                    transform.position += Vector3.up * (floor + 0.005f - lowest);
                }
            }
            finally
            {
                Destroy(baked);
            }

            _restPosition = transform.position;
            _restRotation = transform.rotation;
            _spawnPosition = transform.position;

            Transform footL = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            Transform footR = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            Transform headBone = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            _feet = footL != null && footR != null ? (footL.position + footR.position) * 0.5f : transform.position;
            _head = headBone != null ? headBone.position : transform.position + transform.forward;
            _feet.y = _head.y = transform.position.y;
        }

        /// <summary>Raises the head about the body's sideways axis, a step at a time, until it is no lower than the body.</summary>
        private void LiftHead(Transform hips, Transform neck, Transform head, SkinnedMeshRenderer headSkin,
            float bodyLowest, Mesh baked)
        {
            Vector3 along = head.position - hips.position;
            along.y = 0f;
            if (along.sqrMagnitude < 0.0001f) return;
            along.Normalize();

            Vector3 side = Vector3.Cross(Vector3.up, along);
            Quaternion neckRest = neck != null ? neck.rotation : Quaternion.identity;
            Quaternion headRest = head.rotation;

            for (float angle = 0f; angle <= maxHeadLift + 0.01f; angle += 5f)
            {
                // Negative about this axis tips the face up off the floor; the neck takes half.
                if (neck != null) neck.rotation = Quaternion.AngleAxis(-angle * 0.5f, side) * neckRest;
                head.rotation = Quaternion.AngleAxis(-angle, side) * headRest;

                if (LowestPoint(headSkin, baked) >= bodyLowest - 0.01f) return;
            }
        }

        private static SkinnedMeshRenderer FindHeadSkin(SkinnedMeshRenderer[] skins, Transform head)
        {
            if (head == null) return null;

            foreach (SkinnedMeshRenderer skin in skins)
            {
                if (skin.rootBone == head || skin.name == "Head") return skin;
            }

            return null;
        }

        private static float LowestPoint(SkinnedMeshRenderer skin, Mesh baked)
        {
            skin.BakeMesh(baked, true);

            Vector3[] vertices = baked.vertices;
            Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
            float lowest = float.MaxValue;

            // Every other vertex is plenty to find the bottom of a body.
            for (int i = 0; i < vertices.Length; i += 2)
            {
                float y = toWorld.MultiplyPoint3x4(vertices[i]).y;
                if (y < lowest) lowest = y;
            }

            return lowest;
        }

        private float FloorHeight()
        {
            Vector3 probe = Center;
            probe.y = transform.position.y + 1.5f;

            RaycastHit[] hits = Physics.RaycastAll(probe, Vector3.down, 4f, floorMask, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                // The highest surface at or a little above the spot: the stage, not the pit below it.
                if (hit.point.y > transform.position.y + 0.3f) continue;
                if (hit.point.y > best) best = hit.point.y;
            }

            return best > float.MinValue ? best : transform.position.y;
        }

        /// <summary>A shove from <paramref name="from"/>: the body gives a little and settles.</summary>
        public void Jolt(Vector3 from, float strength = 1f)
        {
            if (!IsDead) return;
            if (_jolt != null) StopCoroutine(_jolt);
            _jolt = StartCoroutine(JoltRoutine(from, strength));
        }

        /// <summary>
        /// Along the floor only, never up or tilted: it used to hop and rock about its feet, which
        /// dipped the head and shoulders into the stage on every poke and kept a bit of each shove
        /// until bodies wandered into each other.
        /// </summary>
        private IEnumerator JoltRoutine(Vector3 from, float strength)
        {
            Vector3 away = transform.position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;

            Vector3 settled = _restPosition + away * (0.02f * strength);
            Vector3 drift = settled - _spawnPosition;
            drift.y = 0f;
            if (drift.magnitude > maxDrift) settled = _spawnPosition + drift.normalized * maxDrift;
            settled.y = _restPosition.y;

            Quaternion settledRotation = _restRotation * Quaternion.Euler(0f, Random.Range(-2f, 2f) * strength, 0f);

            const float duration = 0.45f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                // A small give and back, damped: a body being prodded, not one being kicked.
                float give = Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.05f * strength;

                transform.position = Vector3.Lerp(_restPosition, settled, eased) + away * give;
                transform.rotation = Quaternion.Slerp(_restRotation, settledRotation, eased);
                yield return null;
            }

            Vector3 shift = settled - _restPosition;
            _feet += shift;
            _head += shift;
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
