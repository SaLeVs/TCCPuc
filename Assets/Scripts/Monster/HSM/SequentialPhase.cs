using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Monster.HSM
{
    public class SequentialPhase : ISequence
    {
        public bool IsDone { get; private set; }
        
        private readonly List<PhaseStep> _steps;
        private readonly CancellationToken _cancellationToken;

        private int _index = -1;
        private Task _currentTask;
        
        
        public void StartSequence()
        {
            throw new System.NotImplementedException();
        }

        public bool UpdateSequence()
        {
            throw new System.NotImplementedException();
        }
    }
    
    public delegate Task PhaseStep(CancellationToken token);
}