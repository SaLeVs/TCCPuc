using System.Threading;
using System.Threading.Tasks;

namespace Monster.HSM
{
    public interface IActivity
    {
        public ActivityMode Mode { get; }
        
        // Called when we enter in the state
        public Task ActivateAsync(CancellationToken cancellationToken);
        
        
        public Task DeactivateAsync(CancellationToken cancellationToken);
        
    }
}