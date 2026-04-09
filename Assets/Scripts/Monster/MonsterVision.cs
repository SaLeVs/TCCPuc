using Unity.Netcode;
using UnityEngine;

namespace Monster
{
    public class MonsterVision : NetworkBehaviour
    {
        [SerializeField] private float distance;
        [SerializeField] private float angle;
        [SerializeField] private float height;

        [SerializeField] private Color meshColor;
        
        private Mesh mesh;


        private void OnValidate()
        {
            mesh = CreateMesh();
        }

        private void OnDrawGizmos()
        {
            if (mesh)
            {
                Gizmos.color = meshColor;
                Gizmos.DrawMesh(mesh, transform.position, transform.rotation);
            }
        }
        
        private Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();

            int triangles = 8;
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

            for (int i = 0; i < vertices; ++i)
            {
                totalTriangles[i] = i;
            }
            
            mesh.vertices = totalVertices;
            mesh.triangles = totalTriangles;
            mesh.RecalculateNormals();

            return mesh;
        }
        
    }
    
}
