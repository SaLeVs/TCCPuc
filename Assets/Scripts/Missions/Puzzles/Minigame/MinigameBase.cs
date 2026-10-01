using System;
using UnityEngine;

namespace Missions.Puzzles
{
    // Contrato de todo minigame de UI. Para criar um novo:
    // 1. Herde desta classe e implemente só a regra do jogo, chamando Succeed() / Fail();
    // 2. Faça um prefab de UI com o script na raiz (a tecla de interagir chega em Confirm(), Esc cancela);
    // 3. Arraste o prefab no campo Minigame Prefab de uma estação (MinigamePuzzleManager).
    // Rede, input, cursor, Esc/pause e fechar ao cair/morrer ficam com o MinigameHost e a estação.
    public abstract class MinigameBase : MonoBehaviour
    {
        public event Action OnSucceeded;
        public event Action OnFailed;
        public event Action OnCancelled;


        public abstract void Begin();

        public virtual void Stop() { }

        // Chamado pelo MinigameHost quando o jogador aperta a tecla de interagir.
        public virtual void Confirm() { }

        public void Cancel() => OnCancelled?.Invoke();

        protected void Succeed() => OnSucceeded?.Invoke();

        protected void Fail() => OnFailed?.Invoke();

    }
}
