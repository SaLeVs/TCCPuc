namespace Interfaces
{
    /// <summary>
    /// Something whose body can shove a player over by leaning on them — today the monster while
    /// its attack cools down. Lives here because the Player assembly does not reference Monster.
    /// </summary>
    public interface IPushKnockdownSource
    {
        /// <summary>Server only: whether staying in contact with it right now counts towards a knockdown.</summary>
        bool CanKnockDownByPushing { get; }
    }
}
