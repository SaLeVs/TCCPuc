using UnityEngine;

namespace Systems
{
    public class CircuitSpawnPoint : MonoBehaviour
    {
        [SerializeField] private Color gizmoColor = new Color(1f, 0.85f, 0.2f, 0.6f);
        [SerializeField] private float gizmoRadius = 0.35f;

        public Transform SpawnTransform => transform;

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, gizmoRadius);
            Gizmos.DrawRay(transform.position, transform.forward * (gizmoRadius * 3f));
        }
    }
}
