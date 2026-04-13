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
        
        
        public Vector3 GetRandomPointInSector()
        {
            randomDirection = Random.insideUnitSphere * patrolSectorRadius;
            randomDirection += transform.position;
            
            NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolSectorRadius, 1);

            return hit.position;
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = patrolRadiusColor;
            Gizmos.DrawSphere(transform.position, patrolSectorRadius);
        }
    }
}