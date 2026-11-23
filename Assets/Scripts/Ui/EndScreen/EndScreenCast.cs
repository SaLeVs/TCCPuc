using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ui.EndScreen
{
    /// <summary>
    /// Puts one model per name in the end scene: bodies on the floor (Lose) or survivors that walk
    /// through the door one after the other (Win). The spots are children placed in the scene;
    /// there are as many players as spots at most.
    /// </summary>
    public class EndScreenCast : MonoBehaviour
    {
        public enum Mode
        {
            LyingDead,
            WalkThroughDoor
        }

        [SerializeField] private Mode mode;
        [SerializeField] private EndScreenActor actorPrefab;
        [Tooltip("Where each player is (dead) or starts walking from (alive), in order.")]
        [SerializeField] private Transform[] spots;
        [SerializeField] private EndScreenNameTagStyle nameTag = new EndScreenNameTagStyle { offset = new Vector3(0f, 0.55f, 0f) };

        [Header("Walk through the door")]
        [Tooltip("Points every survivor walks through after their spot: the first is at the door.")]
        [SerializeField] private Transform[] path;
        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float startDelay = 1.2f;
        [SerializeField] private float stagger = 1.1f;
        [Tooltip("Only the last one out stops at the door to look back at the camera.")]
        [SerializeField] private float lookBackTime = 1.2f;

        private readonly List<EndScreenActor> _actors = new();

        public IReadOnlyList<EndScreenActor> Actors => _actors;

        public void Populate(IReadOnlyList<string> names)
        {
            int count = Mathf.Min(names.Count, spots.Length);

            for (int i = 0; i < count; i++)
            {
                Transform spot = spots[i];
                EndScreenActor actor = Instantiate(actorPrefab, spot.position, spot.rotation, transform);
                actor.name = $"{actorPrefab.name} ({names[i]})";
                actor.Setup(names[i], nameTag);
                _actors.Add(actor);

                if (mode == Mode.LyingDead)
                {
                    actor.LieDead(1f);
                }
            }

            if (mode == Mode.WalkThroughDoor && _actors.Count > 0)
            {
                StartCoroutine(WalkOut());
            }
        }

        private IEnumerator WalkOut()
        {
            yield return new WaitForSeconds(startDelay);

            List<Vector3> points = new List<Vector3>();
            foreach (Transform point in path)
            {
                if (point != null) points.Add(point.position);
            }

            for (int i = 0; i < _actors.Count; i++)
            {
                bool isLast = i == _actors.Count - 1;
                StartCoroutine(_actors[i].WalkPath(points, walkSpeed, isLast ? 0 : -1, lookBackTime, 0));
                yield return new WaitForSeconds(stagger);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = mode == Mode.LyingDead ? Color.red : Color.green;
            if (spots != null)
            {
                foreach (Transform spot in spots)
                {
                    if (spot == null) continue;
                    Gizmos.DrawWireSphere(spot.position, 0.25f);
                    Gizmos.DrawLine(spot.position, spot.position + spot.forward * 0.6f);
                }
            }

            if (path == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == null) continue;
                Gizmos.DrawWireSphere(path[i].position, 0.15f);
                if (i > 0 && path[i - 1] != null) Gizmos.DrawLine(path[i - 1].position, path[i].position);
            }
        }
    }
}
