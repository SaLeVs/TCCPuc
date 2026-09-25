using Missions.Puzzles;

namespace Missions
{
    public interface IMissionOwnerAware
    {
        int ItemId { get; }
        void BindToPuzzle(PuzzleManagerBase puzzle);
    }
}