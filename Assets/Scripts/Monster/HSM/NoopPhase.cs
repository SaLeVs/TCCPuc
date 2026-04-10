using Interfaces;

namespace Monster.HSM
{
    // For states that don't need time to transition
    public class NoopPhase : ISequence
    {
        public bool IsDone { get; private set; }

        public void StartSequence()
        {
            IsDone = true;
        }

        public bool UpdateSequence()
        {
            return IsDone;
        }
    }
}