using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoverMaterialChange : MonoBehaviour
{
    [Header("Clique")]
    [Tooltip("Only lights up while the mouse is over a button that can be clicked right now. The mirrors, post-its and planes of the menus sit behind the buttons drawn on them, and used to light up for an empty spot, a locked menu or a panel that is not open.")]
    public bool onlyOverClickableButton = true;

    [Header("Materiais")]
    public Material normalMaterial;
    public Material hoverMaterial;

    [Header("Emission Color")]
    public bool useEmissionInstead = false;
    public Color normalEmission = Color.black;
    public Color hoverEmission = Color.white;

    [Header("Luz do hover")]
    public Light hoverLight; // arraste a luz que deve acender/apagar no hover

    private Renderer rend;
    private MaterialPropertyBlock propBlock;
    private Camera cam;

    void Start()
    {
        rend = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        cam = Camera.main;

        if (!useEmissionInstead && normalMaterial != null)
            rend.material = normalMaterial;

        if (hoverLight != null)
            hoverLight.enabled = false; // começa apagada
    }

    void Update()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool isHovering = Physics.Raycast(ray, out hit) && hit.collider.gameObject == gameObject;
        if (isHovering && onlyOverClickableButton) isHovering = IsPointerOverClickableButton();

        if (useEmissionInstead)
        {
            rend.GetPropertyBlock(propBlock);
            propBlock.SetColor("_EmissionColor", isHovering ? hoverEmission : normalEmission);
            rend.SetPropertyBlock(propBlock);
        }
        else
        {
            rend.material = isHovering ? hoverMaterial : normalMaterial;
        }

        if (hoverLight != null)
            hoverLight.enabled = isHovering;
    }

    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
    private static int _uiCheckFrame = -1;
    private static bool _uiCheckResult;

    /// <summary>
    /// Whether the topmost UI element under the mouse belongs to a button, toggle or dropdown that is
    /// active and interactable (its CanvasGroups included: a panel still fading in, or the menu locked
    /// while it loads, is not). Worked out once a frame for every hover object of the scene.
    /// </summary>
    private static bool IsPointerOverClickableButton()
    {
        if (_uiCheckFrame == Time.frameCount) return _uiCheckResult;
        _uiCheckFrame = Time.frameCount;
        _uiCheckResult = false;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        UiHits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = Input.mousePosition }, UiHits);
        if (UiHits.Count == 0) return false;

        // The topmost hit only: a button under a panel that covers it cannot be clicked either.
        GameObject top = UiHits[0].gameObject;
        Selectable selectable = top != null ? top.GetComponentInParent<Selectable>() : null;
        if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable()) return false;

        _uiCheckResult = selectable is Button || selectable is Toggle || selectable is Dropdown;
        return _uiCheckResult;
    }
}