using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class VisionSensor : NetworkBehaviour
    {
        [Header("Vision References")] 
        [SerializeField] private Transform origin;
        [SerializeField] private Transform orientation;
        
        [Header("Vision Settings")]
        [SerializeField] private float distance;
        [SerializeField, Range(0f, 180f)] private float angle;
        [SerializeField] private float height;
        [SerializeField] private Color meshColor;
        
        [Header("Scan Settings")]
        [SerializeField, Min(1)] private int scanFrequency;
        [SerializeField, Min(1)] private int maxColliders;

        [SerializeField] public LayerMask targetLayers;
        [SerializeField] private LayerMask occlusionLayers;
        
        [SerializeField] private List<GameObject> detectedObjects = new List<GameObject>();
        
        
        private Mesh _mesh;
        private Collider[] _collidersDetected;
        
        private int _collidersCount;
        private float _scanInterval;
        private float _scanTimer;

        
        private void Awake()
        {
            _collidersDetected = new Collider[maxColliders];
        }
        
        private void Start()
        {
            _scanInterval = 1f / scanFrequency;
        }

        private void Update()
        {
            ScanTimer();
        }

        private void ScanTimer()
        {
            _scanTimer -= Time.deltaTime;
            
            if (_scanTimer <= 0)
            {
                _scanTimer += _scanInterval;
                ScanTargets();
            }
        }

        private void ScanTargets()
        {
            _collidersCount = 
                Physics.OverlapSphereNonAlloc(origin.position, distance, _collidersDetected, targetLayers, QueryTriggerInteraction.Collide);
            
            detectedObjects.Clear();

            for (int i = 0; i < _collidersCount; i++)
            {
                GameObject objectDetected = _collidersDetected[i].gameObject;

                if (IsObjectInVision(objectDetected))
                {
                    detectedObjects.Add(objectDetected);
                }
            }
        }

        private bool IsObjectInVision(GameObject objectForTest)
        {
            // Check if object is in correct height
            Vector3 visionOrigin = origin.position;
            Vector3 destination = objectForTest.transform.position;
            Vector3 direction = destination - visionOrigin;

            if (direction.y < 0 || direction.y > height)
            {
                return false;
            }

            // Check if object is in correct angle
            direction.y = 0;
            float deltaAngle = Vector3.Angle(direction, orientation.forward);

            if (deltaAngle > angle)
            {
                return false;
            }
            
            // Check if object is occluded
            if(Physics.Linecast(visionOrigin, destination, occlusionLayers))
            {
                return false;
            }
            
            return true;
        }
        
        private void OnValidate()
        {
            _mesh = CreateMesh();
            _scanInterval = 1f / scanFrequency;
        }
        
        private Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();

            int segments = 10;
            int triangles = (segments * 4) + 2 + 2;
            int vertices = triangles * 3;

            Vector3[] totalVertices = new Vector3[vertices];
            int[] totalTriangles = new int[vertices];
            
            Vector3 bottomCenter = Vector3.zero;
            Vector3 bottomLeft = Quaternion.Euler(0, -angle, 0) * Vector3.forward * distance;
            Vector3 bottomRight = Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
            
            Vector3 topCenter = bottomCenter + Vector3.up * height;
            Vector3 topRight = bottomRight + Vector3.up * height;
            Vector3 topLeft =  bottomLeft + Vector3.up * height;

            int vertexCount = 0;
            
            // Left side
            totalVertices[vertexCount++] = bottomCenter;
            totalVertices[vertexCount++] = bottomLeft;
            totalVertices[vertexCount++] = topLeft;
            
            totalVertices[vertexCount++] = topLeft;
            totalVertices[vertexCount++] = topCenter;
            totalVertices[vertexCount++] = bottomCenter;
            
            // Right side
            totalVertices[vertexCount++] = bottomCenter;
            totalVertices[vertexCount++] = topCenter;
            totalVertices[vertexCount++] = topRight;
            
            totalVertices[vertexCount++] = topRight;
            totalVertices[vertexCount++] = bottomRight;
            totalVertices[vertexCount++] = bottomCenter;
            
            float currentAngle = -angle;
            float deltaAngle = (angle * 2) / segments;

            for (int i = 0; i < segments; ++i)
            {
                bottomLeft = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward * distance;
                bottomRight = Quaternion.Euler(0, currentAngle + deltaAngle, 0) * Vector3.forward * distance;
                
                topRight = bottomRight + Vector3.up * height;
                topLeft =  bottomLeft + Vector3.up * height;
                
                // Far side
                totalVertices[vertexCount++] = bottomLeft;
                totalVertices[vertexCount++] = bottomRight;
                totalVertices[vertexCount++] = topRight;
            
                totalVertices[vertexCount++] = topRight;
                totalVertices[vertexCount++] = topLeft;
                totalVertices[vertexCount++] = bottomLeft;

                // Top
                totalVertices[vertexCount++] = topCenter;
                totalVertices[vertexCount++] = topLeft;
                totalVertices[vertexCount++] = topRight;
            
                // Bottom
                totalVertices[vertexCount++] = bottomCenter;
                totalVertices[vertexCount++] = bottomRight;
                totalVertices[vertexCount++] = bottomLeft;
                
                currentAngle += deltaAngle;
            }
            
            

            for (int i = 0; i < vertices; ++i)
            {
                totalTriangles[i] = i;
            }
            
            mesh.vertices = totalVertices;
            mesh.triangles = totalTriangles;
            mesh.RecalculateNormals();

            return mesh;
        }
        
        private void OnDrawGizmos()
        {
            if (_mesh)
            {
                Gizmos.color = meshColor;
                Gizmos.DrawMesh(_mesh, origin.position, origin.rotation);
            }
            
            Gizmos.DrawWireSphere(origin.position, distance);
            
            for (int i = 0; i < _collidersCount; ++i)
            {
                Gizmos.DrawSphere(_collidersDetected[i].transform.position, 0.2f);
            }
            
            
            Gizmos.color = Color.darkGreen;

            foreach (GameObject obj in detectedObjects)
            {
                Gizmos.DrawSphere(obj.transform.position, 0.2f);
            }
        }
        
    }
    
}
