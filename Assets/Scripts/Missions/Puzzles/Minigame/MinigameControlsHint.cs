using System;
using Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Missions.Puzzles
{
    // Dica de controles no canto da tela do minigame: a tecla numa plaquinha e, ao lado, o que ela faz.
    // As teclas vêm do InputReader, então continuam certas se o binding mudar.
    // Botão do mouse aparece como o desenho do mouse em vez do nome ("LMB"), como no aviso de interação do HUD.
    public class MinigameControlsHint : MonoBehaviour
    {
        [Serializable]
        private struct KeyHint
        {
            public TMP_Text keyText;
            public Image keyIcon;
            public TMP_Text label;
        }

        [SerializeField] private InputReader inputReader;

        [SerializeField] private KeyHint confirm;
        [SerializeField] private KeyHint cancel;

        [SerializeField] private string confirmText = "Checar";
        [SerializeField] private string cancelText = "Sair";

        [Header("Ícones do mouse")]
        [SerializeField] private Sprite mouseLeftSprite;
        [SerializeField] private Sprite mouseRightSprite;
        [SerializeField] private Sprite mouseMiddleSprite;


        private void OnEnable()
        {
            if (inputReader == null) return;

            string key = inputReader.GetInteractBindingDisplay(out string device, out string control);
            Show(confirm, key, device, control, confirmText);

            key = inputReader.GetPauseBindingDisplay(out device, out control);
            Show(cancel, key, device, control, cancelText);
        }

        // Na plaquinha vai o texto da tecla ou o desenho do botão do mouse, nunca os dois.
        // Botão do mouse sem desenho (os laterais) continua em texto.
        private void Show(KeyHint hint, string key, string device, string control, string text)
        {
            Sprite icon = device == "Mouse" ? MouseIcon(control) : null;

            if (hint.keyIcon != null)
            {
                hint.keyIcon.sprite = icon;
                hint.keyIcon.gameObject.SetActive(icon != null);
            }

            if (hint.keyText != null)
            {
                hint.keyText.text = key.Trim();
                hint.keyText.gameObject.SetActive(icon == null);
            }

            if (hint.label != null) hint.label.text = text;
        }

        private Sprite MouseIcon(string control)
        {
            return control switch
            {
                "leftButton" => mouseLeftSprite,
                "rightButton" => mouseRightSprite,
                "middleButton" => mouseMiddleSprite,
                _ => null
            };
        }

    }
}
