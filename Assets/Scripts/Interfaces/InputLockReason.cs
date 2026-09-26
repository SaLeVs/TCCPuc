namespace Interfaces
{
    // Cada sistema trava o input com o seu motivo: soltar um nunca desfaz o travamento de outro
    // (ex.: fechar o minigame não pode destravar o jogador que está caído).
    public enum InputLockReason
    {
        Knockdown,
        ReadyBoard,
        Minigame,
        EndGame
    }
}
