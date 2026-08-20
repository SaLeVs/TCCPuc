using UnityEngine;
using System.Collections;
using System;

public class CameraMoveToTarget : MonoBehaviour
{
    public Transform cameraTransform;
    public float duration = 1.2f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine currentRoutine;

    public void MoveCameraToTarget(Transform targetPosition, Action onComplete = null)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(MoveRoutine(targetPosition, onComplete));
    }

    IEnumerator MoveRoutine(Transform targetPosition, Action onComplete)
    {
        Vector3 startPos = cameraTransform.position;
        Quaternion startRot = cameraTransform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = easeCurve.Evaluate(elapsed / duration);

            cameraTransform.position = Vector3.Lerp(startPos, targetPosition.position, t);
            cameraTransform.rotation = Quaternion.Slerp(startRot, targetPosition.rotation, t);

            yield return null;
        }

        cameraTransform.position = targetPosition.position;
        cameraTransform.rotation = targetPosition.rotation;

        currentRoutine = null;
        onComplete?.Invoke(); // avisa que terminou
    }
}