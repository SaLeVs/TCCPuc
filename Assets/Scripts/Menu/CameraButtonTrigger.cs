using UnityEngine;

public class ButtonCameraTrigger : MonoBehaviour
{
    public CameraMoveToTarget cameraController;
    public Transform target;

    public void OnButtonClick()
    {
        cameraController.MoveCameraToTarget(target);
    }
}