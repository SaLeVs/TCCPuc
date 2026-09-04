using Interfaces;
using UnityEngine;

public class Highlight : MonoBehaviour, IHighlighted
{
    [SerializeField] private Outline outline;

    public void Enable() => SetOutlineVisible(true);

    public void Disable() => SetOutlineVisible(false);

    private void SetOutlineVisible(bool visible)
    {
        if (outline == null) return;

        outline.enabled = visible;
    }
}
