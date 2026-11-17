using System;
using Interfaces;
using UnityEngine;

namespace Tutorial
{
    /// <summary>
    /// Um botão de parede que o jogador aperta com a tecla de interagir. Usado na lição do chat
    /// (o botão escondido que abre a porta), com o modelo do botão do puzzle de luz. Só existe no
    /// host, como o resto do tutorial.
    /// </summary>
    public class TutorialButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private string buttonId;
        [SerializeField] private Transform cap;
        [SerializeField] private Renderer lamp;

        public event Action OnPressed;

        public string Id => buttonId;
        public bool IsPressed { get; private set; }
        public bool IsArmed { get; set; } = true;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public void Configure(string id, Transform capTransform, Renderer lampRenderer)
        {
            buttonId = id;
            cap = capTransform;
            lamp = lampRenderer;
        }

        private const float PressDepth = 0.015f;

        public bool CanInteract(GameObject interactor) => IsArmed && !IsPressed;

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;

            IsPressed = true;

            // O botão afunda na parede (o +Z do botão aponta para fora dela).
            if (cap != null) cap.position -= transform.forward * PressDepth;
            Paint(new Color(0.2f, 0.9f, 0.3f));

            OnPressed?.Invoke();
            return true;
        }

        private void Paint(Color color)
        {
            if (lamp == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            lamp.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(EmissionColor, color * 2f);
            lamp.SetPropertyBlock(block);
        }
    }
}
