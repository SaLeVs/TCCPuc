using UnityEngine;

public class ButtonCameraTrigger : MonoBehaviour
{
    public CameraMoveToTarget cameraController;
    public Transform target;

    [Header("Painel que soma instantâneo (ex: MainMenuPanel)")]
    public GameObject panelToDeactivateInstantly;

    [Header("Painel que vai abrir (com dissolve)")]
    public GameObject panelToOpen;
    public PanelDissolve panelDissolve;

    public void OnButtonClick()
    {
        if (panelToDeactivateInstantly != null)
            panelToDeactivateInstantly.SetActive(false); // some na hora, sem fade

        panelToOpen.SetActive(true); // ativa o novo painel (invisível, por causa do OnEnable)

        cameraController.MoveCameraToTarget(target, () =>
        {
            panelDissolve.Appear(); // dissolve pra dentro quando a câmera chegar
        });
    }
}