using System.Threading;
using System.Threading.Tasks;
using System;

namespace Monster.HSM
{
    public class DelayActivationActivity : Activity
    {
        public float seconds = 0.2f;

        public override async Task ActivateAsync(CancellationToken cancellationToken)
        {
           await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
           await base.ActivateAsync(cancellationToken);
        }
    }
}