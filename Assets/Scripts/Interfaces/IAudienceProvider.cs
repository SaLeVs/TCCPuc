using System;

namespace Interfaces
{
    public interface IAudienceProvider
    {
        event Action<float> OnAudienceChanged;
        float NormalizedAudience { get; }
    }
}