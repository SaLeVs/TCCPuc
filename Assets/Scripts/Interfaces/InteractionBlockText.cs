namespace Interfaces
{
    /// <summary>
    /// The reasons shown under "INDISPONÍVEL" (see <see cref="IInteractionBlockReason"/>), in one place
    /// so every object explains itself in the same words. They follow what the tutorial chat tells the
    /// player ("É missão de outro jogador. Só o dono pode pegar.", "Ache o QUADRO DE LUZ"...), so the
    /// prompt reminds them of a lesson instead of teaching a new phrase.
    /// </summary>
    public static class InteractionBlockText
    {
        public const string OtherPlayersMission = "É missão de outro jogador";
        public const string NotYourMission = "Não é missão sua";
        public const string OnlyOwnerTakes = "Só o dono pode pegar";
        public const string OnlyOwnerUses = "Só o dono pode usar";
        public const string OnlyOwnerTurns = "Só o dono pode girar";
        public const string MissionComplete = "Missão completa!";

        public const string SlotTaken = "Já tem uma caixa aqui";
        public const string FullInventoryOnTotem = "Inventário cheio\nSegure outra caixa pra trocar";

        public const string LightsAreOn = "A luz não caiu\nO quadro religa depois do apagão";

        public const string BoardInUse = "Outro jogador está no quadro";

        public const string AlreadyReady = "Você já está pronto";
        public const string NotYourTotem = "É o totem de outro jogador";
        public const string NotOpenYet = "Ainda não liberado";

        /// <summary>
        /// Who a mission belongs to, put the way the tutorial put it: someone else's, or nobody's yet.
        /// <paramref name="ownerAssigned"/> false covers a piece still waiting for its mission.
        /// </summary>
        public static string Ownership(bool ownerAssigned, string onlyOwner)
        {
            return ownerAssigned ? $"{OtherPlayersMission}\n{onlyOwner}" : NotYourMission;
        }
    }
}
