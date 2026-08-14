using UnityEngine;
using System.Collections;

public class CameraMoveToTarget : MonoBehaviour
{
    [Header("Referências")]
    public Transform cameraTransform; // arraste a Main Camera aqui

    [Header("Configuração do movimento")]
    public float duration = 1.2f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector3 startPos;
    private Quaternion startRot;
    private Coroutine currentRoutine;

    // Chame essa função passando o Transform de destino de cada botão
    public void MoveCameraToTarget(Transform targetPosition)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(MoveRoutine(targetPosition));
    }

    IEnumerator MoveRoutine(Transform targetPosition)
    {
        startPos = cameraTransform.position;
        startRot = cameraTransform.rotation;

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
    }
}