using System;
using UnityEngine.InputSystem;

namespace Tutorial
{
    /// <summary>
    /// Nomes das teclas em português, lidos do mesmo PlayerActions que o jogo usa. Se alguém trocar
    /// um binding no asset de input, o texto do tutorial acompanha sem ninguém lembrar de editar.
    /// </summary>
    public static class TutorialKeys
    {
        private static PlayerActions _actions;

        private static PlayerActions Actions => _actions ??= new PlayerActions();

        public static string Move => "W A S D";
        public static string Run => Name(Actions.Game.Run, "Shift");
        public static string Crouch => Name(Actions.Game.Crouch, "Ctrl");
        public static string Flashlight => Name(Actions.Game.Flashlight, "F");
        public static string Interact => Name(Actions.Game.Interact, "clique esquerdo");
        public static string Use => Name(Actions.Game.Use, "clique esquerdo");
        public static string Chat => Name(Actions.Game.Chat, "C");
        public static string Missions => Name(Actions.Game.HideMissions, "V");
        public static string Donate => Name(Actions.Game.HideDonate, "B");
        public static string Pause => Name(Actions.Game.Pause, "Esc");
        public static string Slot1 => Name(Actions.Game.Slot1, "1");

        private static string Name(InputAction action, string fallback)
        {
            if (action == null) return fallback;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;

                string path = binding.effectivePath;
                if (string.IsNullOrEmpty(path)) continue;

                int slash = path.LastIndexOf('/');
                string control = slash >= 0 ? path.Substring(slash + 1) : path;

                return Translate(control, fallback);
            }

            return fallback;
        }

        private static string Translate(string control, string fallback)
        {
            switch (control)
            {
                case "leftButton": return "clique esquerdo";
                case "rightButton": return "clique direito";
                case "middleButton": return "botão do meio";
                case "shift":
                case "leftShift":
                case "rightShift": return "Shift";
                case "ctrl":
                case "leftCtrl":
                case "rightCtrl": return "Ctrl";
                case "alt":
                case "leftAlt": return "Alt";
                case "escape": return "Esc";
                case "space": return "Espaço";
                case "enter": return "Enter";
                case "tab": return "Tab";
                case "backspace": return "Backspace";
            }

            if (control.Length == 1) return control.ToUpperInvariant();

            return string.IsNullOrEmpty(control) ? fallback : control;
        }

        /// <summary>Troca {move}, {run}, {interact}... pelo nome da tecla de verdade.</summary>
        public static string Fill(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return text
                .Replace("{move}", Move)
                .Replace("{run}", Run)
                .Replace("{crouch}", Crouch)
                .Replace("{flashlight}", Flashlight)
                .Replace("{interact}", Interact)
                .Replace("{use}", Use)
                .Replace("{chat}", Chat)
                .Replace("{missions}", Missions)
                .Replace("{donate}", Donate)
                .Replace("{pause}", Pause)
                .Replace("{slot1}", Slot1);
        }
    }
}
