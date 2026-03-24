using Interfaces;
using UnityEngine;

public class Highlight : MonoBehaviour, IHighlighted
{
    [SerializeField] private Outline outline;
    
    public void Enable()
    {
        outline.enabled = true;
    }

    public void Disable()
    {
        outline.enabled = false;
    }
}
