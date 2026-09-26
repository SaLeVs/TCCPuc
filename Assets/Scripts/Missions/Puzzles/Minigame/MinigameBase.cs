using System;
using UnityEngine;

namespace Missions.Puzzles
{
    // Contrato de todo minigame de UI. Para criar um novo:
    // 1. Herde desta classe e implemente só a regra do jogo, chamando Succeed() / Fail();
    // 2. Faça um prefab de UI com o script na raiz (ligue um botão "Sair" em Cancel());
    // 3. Arraste o prefab no campo Minigame Prefab de uma estação (MinigamePuzzleManager).
    // Rede, input, cursor e fechar ao cair/morrer ficam com o MinigameHost e a estação.
    public abstract class MinigameBase : MonoBehaviour
    {
        public event Action OnSucceeded;
        public event Action OnFailed;
        public event Action OnCancelled;


        public abstract void Begin();

        public virtual void Stop() { }

        public void Cancel() => OnCancelled?.Invoke();

        protected void Succeed() => OnSucceeded?.Invoke();

        protected void Fail() => OnFailed?.Invoke();

    }
}
