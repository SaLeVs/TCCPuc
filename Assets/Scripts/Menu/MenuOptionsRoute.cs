using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opens the options board from the online lobby, and sends its close button (the little house)
/// back to wherever the options were opened from.
///
/// <para>The options board hangs on its own wall: its board only shows once the camera has moved
/// there and its PanelDissolve was told to appear, which the main menu's OPÇÕES does through its
/// ButtonCameraTrigger. The lobby's OPÇÕES only switched the panel on, so it stayed invisible and
/// unclickable, and the house always went back to the main menu.</para>
/// </summary>
public class MenuOptionsRoute : MonoBehaviour
{
    [Tooltip("Moves the menu camera; the one the main menu's OPÇÕES uses.")]
    [SerializeField] private CameraMoveToTarget cameraController;

    [Tooltip("Where the camera looks at the options board.")]
    [SerializeField] private Transform optionsCameraTarget;

    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private PanelDissolve optionsDissolve;

    [Tooltip("The house of the options board. Its own ButtonCameraTrigger goes back to the main menu; " +
             "it is pointed at the lobby while the options were opened from there.")]
    [SerializeField] private ButtonCameraTrigger closeTrigger;

    [Header("Lobby online")]
    [Tooltip("Where the camera looks at the online lobby.")]
    [SerializeField] private Transform lobbyCameraTarget;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private PanelDissolve lobbyDissolve;

    // The house's own way back: to the main menu.
    private Transform _menuTarget;
    private GameObject _menuDeactivate;
    private GameObject _menuOpen;
    private PanelDissolve _menuDissolve;

    private CanvasGroup _lobbyGroup;
    private bool _fromLobby;

    private void Awake()
    {
        if (closeTrigger == null) return;

        _menuTarget = closeTrigger.target;
        _menuDeactivate = closeTrigger.panelToDeactivateInstantly;
        _menuOpen = closeTrigger.panelToOpen;
        _menuDissolve = closeTrigger.panelDissolve;

        // Runtime listeners run after the button's persistent ones, so this comes after the trigger
        // has already sent the camera back: the route is put back for the next opening.
        Button closeButton = closeTrigger.GetComponent<Button>();
        if (closeButton != null) closeButton.onClick.AddListener(CloseButton_OnClick);

        if (lobbyPanel != null) _lobbyGroup = lobbyPanel.GetComponent<CanvasGroup>();
    }

    /// <summary>The lobby's OPÇÕES.</summary>
    public void OpenFromLobby()
    {
        if (optionsPanel == null || cameraController == null || optionsCameraTarget == null) return;

        _fromLobby = true;

        if (closeTrigger != null)
        {
            // The lobby panel stays on, so the lobby keeps refreshing and still notices the match
            // starting while the player is at the board; it only stops taking clicks.
            closeTrigger.target = lobbyCameraTarget;
            closeTrigger.panelToDeactivateInstantly = null;
            closeTrigger.panelToOpen = lobbyPanel;
            closeTrigger.panelDissolve = lobbyDissolve;
        }

        SetLobbyClickable(false);

        optionsPanel.SetActive(true);
        cameraController.MoveCameraToTarget(optionsCameraTarget, () =>
        {
            if (optionsDissolve != null) optionsDissolve.Appear();
        });
    }

    private void CloseButton_OnClick()
    {
        if (!_fromLobby) return;

        _fromLobby = false;
        SetLobbyClickable(true);
        RestoreMenuRoute();
    }

    private void RestoreMenuRoute()
    {
        if (closeTrigger == null) return;

        closeTrigger.target = _menuTarget;
        closeTrigger.panelToDeactivateInstantly = _menuDeactivate;
        closeTrigger.panelToOpen = _menuOpen;
        closeTrigger.panelDissolve = _menuDissolve;
    }

    private void SetLobbyClickable(bool clickable)
    {
        if (_lobbyGroup == null) return;

        _lobbyGroup.interactable = clickable;
        _lobbyGroup.blocksRaycasts = clickable;
    }

    private void OnDisable()
    {
        // Left the lobby some other way (the match started, the scene changed): nothing stays locked.
        if (!_fromLobby) return;

        _fromLobby = false;
        SetLobbyClickable(true);
        RestoreMenuRoute();
    }
}
