using Inputs;
using TMPro;
using UnityEngine;

namespace Missions.Puzzles
{
    // Dica de controles no canto da tela do minigame ("[LMB] Checar   [Esc] Sair").
    // As teclas vêm do InputReader, então continuam certas se o binding mudar.
    public class MinigameControlsHint : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private TMP_Text label;

        [SerializeField] private string confirmText = "Checar";
        [SerializeField] private string cancelText = "Sair";


        private void OnEnable()
        {
            if (inputReader == null || label == null) return;

            label.text = $"{Key(inputReader.GetInteractBindingDisplay())} {confirmText}\n" +
                         $"{Key(inputReader.GetPauseBindingDisplay())} {cancelText}";
        }

        // Um action com mais de um binding vem como "LMB | E": mostra só o primeiro, como no HUD.
        private static string Key(string display)
        {
            int split = display.IndexOf('|');
            string key = (split >= 0 ? display.Substring(0, split) : display).Trim();

            return $"<b>[{key}]</b>";
        }

    }
}
