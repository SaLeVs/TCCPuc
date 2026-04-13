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


        public SequentialPhase(List<PhaseStep> steps, CancellationToken cancellationToken)
        {
            _steps = steps;
            _cancellationToken = cancellationToken;
        }

        public void StartSequence() => Next();

        private void Next()
        {
            _index++;
            
            if (_index >= _steps.Count)
            {
                IsDone = true;
                return;
            }
            
            _currentTask = _steps[_index](_cancellationToken);
            
        }

        public bool UpdateSequence()
        {
            if (IsDone) return true;

            if (_currentTask == null || _currentTask.IsCompleted)
            {
                Next();
            }
            
            return IsDone;
        }
        
    }
}