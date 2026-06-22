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
        public event Action<GameObject> OnTargetEnterStatic;
        public event Action<GameObject> OnTargetExitStatic;
        public event Action<GameObject> OnTargetEnter;
        public event Action<GameObject> OnTargetExit;

        [Header("Vision References")]
        [SerializeField] private Transform orientation;

        [Header("Vision Settings")]
        [SerializeField] private float distance;
        [SerializeField, Range(0f, 180f)] private float angle;
        [SerializeField] private float height;
        [SerializeField] private float closeProximityRadius = 2f;
        [SerializeField] private Color meshColor;

        [Header("Scan Settings")]
        [SerializeField, Min(1)] private int scanFrequency;

        [SerializeField] private LayerMask targetLayers;
        [SerializeField] private LayerMask occlusionLayers;

        [SerializeField] private List<GameObject> detectedObjects = new();
        [SerializeField] private List<GameObject> previousDetectedObjects = new();

        private Mesh _mesh;
        private readonly Collider[] _collidersDetected = new Collider[50];

        private int _collidersCount;
        private float _scanInterval;
        private float _scanTimer;

        private Vector3 _destination;

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

            if (_scanTimer <= 0f)
            {
                _scanTimer += _scanInterval;
                ScanTargets();
            }
        }

        private void ScanTargets()
        {
            previousDetectedObjects.Clear();
            previousDetectedObjects.AddRange(detectedObjects);
            detectedObjects.Clear();

            _collidersCount = Physics.OverlapSphereNonAlloc(
                orientation.position,
                distance,
                _collidersDetected,
                targetLayers,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < _collidersCount; i++)
            {
                Collider currentCollider = _collidersDetected[i];
                if (currentCollider == null)
                    continue;

                GameObject objectDetected = currentCollider.gameObject;

                if (!IsObjectInVision(objectDetected))
                    continue;

                if (!objectDetected.TryGetComponent(out RecordableIdentifier identifier))
                    continue;

                if (identifier.targetType == RecordableTarget.None)
                    continue;

                detectedObjects.Add(objectDetected);

                if (!previousDetectedObjects.Contains(objectDetected))
                    TargetEnter(objectDetected, identifier);
            }

            foreach (GameObject detectedObject in previousDetectedObjects)
            {
                if (detectedObject == null)
                    continue;

                if (detectedObjects.Contains(detectedObject))
                    continue;

                if (detectedObject.TryGetComponent(out RecordableIdentifier identifier))
                    TargetExit(detectedObject, identifier);
            }
        }

        private void TargetEnter(GameObject target, RecordableIdentifier identifier)
        {
            if (!IsServer) return;

            OnTargetEnterServer?.Invoke(target, identifier.targetType);

            if (target.TryGetComponent(out NetworkObject netObj))
            {
                SendNetworkTargetEnterRpc(
                    netObj.NetworkObjectId,
                    RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp)
                );
                return;
            }

            if (!string.IsNullOrWhiteSpace(identifier.RecordableId))
            {
                SendStaticTargetEnterRpc(
                    identifier.RecordableId,
                    RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp)
                );
            }
        }

        private void TargetExit(GameObject target, RecordableIdentifier identifier)
        {
            if (!IsServer) return;

            OnTargetExitServer?.Invoke(target, identifier.targetType);

            if (target.TryGetComponent(out NetworkObject netObj))
            {
                SendNetworkTargetExitRpc(
                    netObj.NetworkObjectId,
                    RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp)
                );
                return;
            }

            if (!string.IsNullOrWhiteSpace(identifier.RecordableId))
            {
                SendStaticTargetExitRpc(
                    identifier.RecordableId,
                    RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp)
                );
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendNetworkTargetEnterRpc(ulong id, RpcParams rpcParams = default)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject networkObject))
            {
                OnTargetEnter?.Invoke(networkObject.gameObject);
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendNetworkTargetExitRpc(ulong id, RpcParams rpcParams = default)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject networkObject))
            {
                OnTargetExit?.Invoke(networkObject.gameObject);
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendStaticTargetEnterRpc(string recordableId, RpcParams rpcParams = default)
        {
            foreach (RecordableIdentifier identifier in FindObjectsByType<RecordableIdentifier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (identifier == null)
                    continue;

                if (identifier.RecordableId != recordableId)
                    continue;

                OnTargetEnterStatic?.Invoke(identifier.gameObject);
                break;
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendStaticTargetExitRpc(string recordableId, RpcParams rpcParams = default)
        {
            foreach (RecordableIdentifier identifier in FindObjectsByType<RecordableIdentifier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (identifier == null)
                    continue;

                if (identifier.RecordableId != recordableId)
                    continue;

                OnTargetExitStatic?.Invoke(identifier.gameObject);
                break;
            }
        }

        private bool IsObjectInVision(GameObject objectForTest)
        {
            if (!objectForTest.TryGetComponent(out Collider colliderBound))
                return false;

            _destination = colliderBound.bounds.center;
            Vector3 directionToTarget = _destination - orientation.position;
            float directDistance = directionToTarget.magnitude;

            if (directDistance > distance) return false;

            if (directDistance <= closeProximityRadius)
            {
                if (Physics.Linecast(orientation.position, _destination, occlusionLayers)) return false;
                return true;
            }

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
            Vector3 topCenter = Vector3.up * halfHeight;

            Vector3 bottomLeft = Quaternion.Euler(0, -angle, 0) * Vector3.forward * distance + Vector3.down * halfHeight;
            Vector3 bottomRight = Quaternion.Euler(0, angle, 0) * Vector3.forward * distance + Vector3.down * halfHeight;
            Vector3 topLeft = bottomLeft + Vector3.up * height;
            Vector3 topRight = bottomRight + Vector3.up * height;

            int vertexCount = 0;

            totalVertices[vertexCount++] = bottomCenter;
            totalVertices[vertexCount++] = bottomLeft;
            totalVertices[vertexCount++] = topLeft;

            totalVertices[vertexCount++] = topLeft;
            totalVertices[vertexCount++] = topCenter;
            totalVertices[vertexCount++] = bottomCenter;

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
                bottomLeft = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward * distance + Vector3.down * halfHeight;
                bottomRight = Quaternion.Euler(0, currentAngle + deltaAngle, 0) * Vector3.forward * distance + Vector3.down * halfHeight;

                topRight = bottomRight + Vector3.up * height;
                topLeft = bottomLeft + Vector3.up * height;

                totalVertices[vertexCount++] = bottomLeft;
                totalVertices[vertexCount++] = bottomRight;
                totalVertices[vertexCount++] = topRight;

                totalVertices[vertexCount++] = topRight;
                totalVertices[vertexCount++] = topLeft;
                totalVertices[vertexCount++] = bottomLeft;

                totalVertices[vertexCount++] = topCenter;
                totalVertices[vertexCount++] = topLeft;
                totalVertices[vertexCount++] = topRight;

                totalVertices[vertexCount++] = bottomCenter;
                totalVertices[vertexCount++] = bottomRight;
                totalVertices[vertexCount++] = bottomLeft;

                currentAngle += deltaAngle;
            }

            for (int i = 0; i < vertices; ++i)
                totalTriangles[i] = i;

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
                Gizmos.DrawMesh(_mesh, orientation.position, orientation.rotation);
            }

            if (orientation != null)
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

            for (int i = 0; i < _collidersDetected.Length; i++)
                _collidersDetected[i] = null;

            detectedObjects.Clear();
            previousDetectedObjects.Clear();
        }
    }
}