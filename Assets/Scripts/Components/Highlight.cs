using Interfaces;
using UnityEngine;

public class Highlight : MonoBehaviour, IHighlighted
{
    [SerializeField] private Material _highlightMaterial;
    [SerializeField] private MeshRenderer _meshRenderer;
    
    private Material _material;
    
    
    public void Enable()
    {
        
    }

    public void Disable()
    {
        
    }
}
