using System.Threading;
using System.Threading.Tasks;

namespace Monster.HSM
{
    public interface ISequence
    { 
        public bool IsDone { get; }
        public void StartSequence();
        public bool UpdateSequence();
    }
    /// <summary>
    /// Asynchronous method for a phase step. The method should complete when the step is done, and can be canceled with the provided token
    /// </summary>
    public delegate Task PhaseStep(CancellationToken token);
}