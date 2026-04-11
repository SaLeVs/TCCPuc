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
    
    
}