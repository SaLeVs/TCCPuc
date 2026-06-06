using Interfaces;
using UnityEngine;

public class Highlight : MonoBehaviour, IHighlighted
{
    [SerializeField] private Outline outline;
    
    public void Disable()
    {
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    public void Enable()
    {
        if (outline != null)
        {
            outline.enabled = true;
        }
    }
}
