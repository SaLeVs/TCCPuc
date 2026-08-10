using UnityEngine;

public class HoverMaterialChange : MonoBehaviour
{
    [Header("Materiais")]
    public Material normalMaterial;
    public Material hoverMaterial;

    [Header("Emission Color")]
    public bool useEmissionInstead = false;
    public Color normalEmission = Color.black;
    public Color hoverEmission = Color.white;

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
    }

    void Update()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool isHovering = Physics.Raycast(ray, out hit) && hit.collider.gameObject == gameObject;

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
    }
}