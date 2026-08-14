using UnityEngine;

public class ButtonCameraTrigger : MonoBehaviour
{
    public CameraMoveToTarget cameraController;
    public Transform target;
    public GameObject panelToOpen;   // ex: LanPanel
    public PanelDissolve panelDissolve; // o PanelDissolve do mesmo painel

    public void OnButtonClick()
    {
        panelToOpen.SetActive(true); // ativa já invisível (por causa do OnEnable)

        cameraController.MoveCameraToTarget(target, () =>
        {
            panelDissolve.Appear(); // só dissolve pra dentro quando a câmera chegar
        });
    }
}