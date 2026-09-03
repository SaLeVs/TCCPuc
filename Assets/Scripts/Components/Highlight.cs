using Interfaces;
using UnityEngine;

public class Highlight : MonoBehaviour, IHighlighted
{
    [SerializeField] private Outline outline;

    [Header("Colors")]
    [SerializeField] private Color availableColor = Color.white;

    private bool _cachedOriginal;

    public void Disable()
    {
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    public void Enable()
    {
        Show(availableColor);
    }

    private void Show(Color color)
    {
        if (outline == null) return;
        
        if (!_cachedOriginal)
        {
            _cachedOriginal = true;

            if (availableColor == Color.white)
            {
                availableColor = outline.OutlineColor;
                if (color == Color.white) color = availableColor;
            }
        }

        outline.OutlineColor = color;
        outline.enabled = true;
    }
}
