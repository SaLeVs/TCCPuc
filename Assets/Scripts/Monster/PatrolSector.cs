using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class PatrolSector : MonoBehaviour
    {
        [SerializeField] private float patrolSectorRadius;
        [SerializeField] private Color patrolRadiusColor;
        
        public Vector3 Position => transform.position;
        
        private Vector3 randomDirection;
        
        
        /// <summary>
        /// A failed SamplePosition leaves hit.position at infinity, and feeding that to the agent
        /// is what produced the "setting destination to infinity is ignored" spam — the monster
        /// then kept its old path and stuttered. Returns false instead of a garbage point.
        /// </summary>
        public bool TryGetRandomPointInSector(out Vector3 point)
        {
            randomDirection = Random.insideUnitSphere * patrolSectorRadius;
            randomDirection += transform.position;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolSectorRadius, 1))
            {
                point = hit.position;
                return true;
            }

            point = transform.position;
            return false;
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = patrolRadiusColor;
            Gizmos.DrawSphere(transform.position, patrolSectorRadius);
        }
    }
}