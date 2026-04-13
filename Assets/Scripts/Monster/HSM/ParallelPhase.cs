using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Monster.HSM
{
    public class ParallelPhase : ISequence
    {
        public bool IsDone { get; private set; }
        
        private readonly List<PhaseStep> _steps;
        private readonly CancellationToken _cancellationToken;
        
        private List<Task> _tasks;
        
        
        public ParallelPhase(List<PhaseStep> steps, CancellationToken cancellationToken)
        {
            _steps = steps;
            _cancellationToken = cancellationToken;
        }
        
        public void StartSequence()
        {
            if (_steps == null || _steps.Count == 0)
            {
                IsDone = true;
                return;
            }
            
            _tasks = new List<Task>(_steps.Count);

            for (int i = 0; i < _steps.Count; i++)
            {
                _tasks.Add(_steps[i](_cancellationToken));
            }
        }

        public bool UpdateSequence()
        {
            if (IsDone) return true;
            
            IsDone = _tasks == null || _tasks.TrueForAll(task => task.IsCompleted);
            
            return IsDone;
        }
    }
}