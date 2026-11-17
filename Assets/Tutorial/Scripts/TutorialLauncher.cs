using Enums;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tutorial
{
    /// <summary>
    /// O botão "Tutorial" do menu principal chama <see cref="Launch"/>. A cena se vira sozinha a
    /// partir daí: sobe o host, cria o jogador e começa a aula.
    /// </summary>
    public class TutorialLauncher : MonoBehaviour
    {
        /// <summary>
        /// O nome começa com "Game" e um dígito de propósito: chat, barra de audiência, REC e música
        /// só ligam em cenas que o jogo reconhece como partida (Enums.SceneNames.IsGame).
        /// </summary>
        public const string SceneName = "Game0_Tutorial";

        private static bool _loading;

        public void Launch()
        {
            if (_loading) return;

            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                Components.ScreenToast.Show("A cena do tutorial não está no Build Settings.", Components.ToastKind.Warning);
                return;
            }

            _loading = true;
            CursorState.Clear();
            SceneManager.LoadScene(SceneName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _loading = false;

        private void OnDestroy() => _loading = false;
    }
}
