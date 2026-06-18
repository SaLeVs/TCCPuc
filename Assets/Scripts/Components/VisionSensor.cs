using System;
using System.Collections.Generic;
using Enums;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class VisionSensor : NetworkBehaviour
    {
        public event Action<GameObject, RecordableTarget> OnTargetEnterServer;
        public event Action<GameObject, RecordableTarget> OnTargetExitServer;
        public event Action<RecordableTarget> OnTargetEnterStatic;
        public event Action<RecordableTarget> OnTargetExitStatic;
        public event Action<GameObject> OnTargetEnter;
        public event Action<GameObject> OnTargetExit;
        
        
        [Header("Vision References")] 
        [SerializeField] private Transform orientation;
        
        [Header("Vision Settings")]
        [SerializeField] private float distance;
        [SerializeField, Range(0f, 180f)] private float angle;
        [SerializeField] private float height;
        [SerializeField] private Color meshColor;
        
        [Header("Scan Settings")]
        [SerializeField, Min(1)] private int scanFrequency;
        
        [SerializeField] public LayerMask targetLayers;
        [SerializeField] private LayerMask occlusionLayers;
        
        [SerializeField] private List<GameObject> detectedObjects = new List<GameObject>();
        [SerializeField] private List<GameObject> previousDetectedObjects = new List<GameObject>();
        
        
        private Mesh _mesh;
        private Collider[] _collidersDetected = new Collider[50];
        
        private int _collidersCount;
        private float _scanInterval;
        private float _scanTimer;

        private Vector3 _destination;
        private Vector3 _origin;
        private Vector3 _direction;
        
        
        private void Start()
        {
            _scanInterval = 1f / scanFrequency;
        }
        
        private void OnValidate()
        {
            _mesh = CreateMesh();
            _scanInterval = 1f / scanFrequency;
        }
        
        private void Update()
        {
            if (!IsServer) return;
            
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
            previousDetectedObjects.Clear();
            foreach (GameObject obj in detectedObjects) previousDetectedObjects.Add(obj);
            detectedObjects.Clear();

            _collidersCount = Physics.OverlapSphereNonAlloc(
                orientation.position, distance, _collidersDetected, targetLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < _collidersCount; i++)
            {
                if (_collidersDetected[i] == null)
                {
                    _collidersDetected[i] = null;
                    continue;
                }

                GameObject objectDetected = _collidersDetected[i].gameObject;
                if (!IsObjectInVision(objectDetected)) continue;
                if (!objectDetected.TryGetComponent(out RecordableIdentifier identifier)) continue;
                if (identifier.targetType == RecordableTarget.None) continue;

                detectedObjects.Add(objectDetected);

                if (!previousDetectedObjects.Contains(objectDetected))
                    TargetEnter(objectDetected, identifier.targetType);
            }

            foreach (GameObject detectedObject in previousDetectedObjects)
            {
                if (detectedObject == null) continue;
                if (!detectedObjects.Contains(detectedObject))
                {
                    if (detectedObject.TryGetComponent(out RecordableIdentifier identifier))
                        TargetExit(detectedObject, identifier.targetType);
                }
            }
        }

        private void TargetEnter(GameObject target, RecordableTarget targetType)
        {
            if (!IsServer) return;

            OnTargetEnterServer?.Invoke(target, targetType);

            if (target.TryGetComponent(out NetworkObject netObj))
            {
                SendNetworkTargetEnterRpc(netObj.NetworkObjectId);
            }
            else
            {
                SendStaticTargetEnterRpc((int)targetType);
            }
        }

        private void TargetExit(GameObject target, RecordableTarget targetType)
        {
            if (!IsServer) return;

            OnTargetExitServer?.Invoke(target, targetType);

            if (target.TryGetComponent(out NetworkObject netObj))
            {
                SendNetworkTargetExitRpc(netObj.NetworkObjectId);
            }
            else
            {
                SendStaticTargetExitRpc((int)targetType);
            }
        }
        
        [Rpc(SendTo.Owner)]
        private void SendNetworkTargetEnterRpc(ulong id)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject networkObject))
            {
                OnTargetEnter?.Invoke(networkObject.gameObject);
            }
        }

        [Rpc(SendTo.Owner)]
        private void SendNetworkTargetExitRpc(ulong id)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj))
            {
                OnTargetExit?.Invoke(netObj.gameObject);
            }
        }

        [Rpc(SendTo.Owner)]
        private void SendStaticTargetEnterRpc(int targetType)
        {
            OnTargetEnterStatic?.Invoke((RecordableTarget)targetType);
        }

        [Rpc(SendTo.Owner)]
        private void SendStaticTargetExitRpc(int targetType)
        {
            OnTargetExitStatic?.Invoke((RecordableTarget)targetType);
        }
        
        private bool IsObjectInVision(GameObject objectForTest)
        {
            if (!objectForTest.TryGetComponent(out Collider collider)) return false;

            _destination = collider.bounds.center;

            Vector3 directionToTarget = _destination - orientation.position;
            
            if (directionToTarget.magnitude > distance) return false;
            
            Vector3 localPos = orientation.InverseTransformPoint(_destination);

            if (localPos.y < -height * 0.5f || localPos.y > height * 0.5f) return false;
            
            float dot = Vector3.Dot(orientation.forward, directionToTarget.normalized);
            float minDot = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);

            if (dot < minDot) return false;
            
            if (Physics.Linecast(orientation.position, _destination, occlusionLayers)) return false;

            return true;
        }
        
        private Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();

            int segments = 10;
            int triangles = (segments * 4) + 2 + 2;
            int vertices = triangles * 3;

            Vector3[] totalVertices = new Vector3[vertices];
            int[] totalTriangles = new int[vertices];

            float halfHeight = height / 2f;

            Vector3 bottomCenter = Vector3.down * halfHeight;
            Vector3 topCenter    = Vector3.up   * halfHeight;

            Vector3 bottomLeft  = Quaternion.Euler(0, -angle, 0) * Vector3.forward * distance + Vector3.down * halfHeight;
            Vector3 bottomRight = Quaternion.Euler(0,  angle, 0) * Vector3.forward * distance + Vector3.down * halfHeight;
            Vector3 topLeft     = bottomLeft  + Vector3.up * height;
            Vector3 topRight    = bottomRight + Vector3.up * height;

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
                bottomLeft  = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward * distance + Vector3.down * halfHeight;
                bottomRight = Quaternion.Euler(0, currentAngle + deltaAngle, 0) * Vector3.forward * distance + Vector3.down * halfHeight;

                topRight = bottomRight + Vector3.up * height;
                topLeft  = bottomLeft  + Vector3.up * height;

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
                totalTriangles[i] = i;

            mesh.vertices  = totalVertices;
            mesh.triangles = totalTriangles;
            mesh.RecalculateNormals();

            return mesh;
        }
        
        private void OnDrawGizmos()
        {
            if (_mesh)
            {
                Gizmos.color = meshColor;
                Gizmos.DrawMesh(_mesh, orientation.position, orientation.rotation);
            }

            Gizmos.DrawWireSphere(orientation.position, distance);

            for (int i = 0; i < _collidersCount; ++i)
            {
                if (_collidersDetected[i] == null) continue;

                Gizmos.DrawSphere(_collidersDetected[i].transform.position, 0.2f);
            }

            Gizmos.color = Color.green;

            foreach (GameObject obj in detectedObjects)
            {
                if (obj == null) continue;

                Gizmos.DrawSphere(obj.transform.position, 0.2f);
            }
        }
        
        private void OnDisable()
        {
            _collidersCount = 0;
            
            if (_collidersDetected != null)
            {
                for (int i = 0; i < _collidersDetected.Length; i++)
                    _collidersDetected[i] = null;
            }

            detectedObjects?.Clear();
            previousDetectedObjects?.Clear();
        }
        
    }
}
