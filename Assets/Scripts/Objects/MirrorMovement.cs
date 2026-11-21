using System;
using UnityEngine;

public class MirrorMovement : MonoBehaviour
{
    [SerializeField] private Transform mirror;
    [SerializeField] private Transform playerTarget;

    void Update()
    {
        Vector3 localPlayer = mirror.InverseTransformPoint(playerTarget.position);
        transform.position = mirror.TransformPoint(new Vector3(localPlayer.x, localPlayer.y, -localPlayer.z));
        
        Vector3 lookAtMirror = mirror.TransformPoint(new Vector3(-localPlayer.x, localPlayer.y, -localPlayer.z));
        transform.LookAt(lookAtMirror);
    }
}
