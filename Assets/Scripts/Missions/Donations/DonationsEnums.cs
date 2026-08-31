using System;

namespace Missions.Donations
{
    public enum DonationCategory
    {
        Recording,
        MicSpeech
    }
    
    public enum DonationStackingMode
    {
        Cumulative,
        Exclusive
    }

    public enum DonationState
    {
        Active,
        Completed,
        Expired
    }
}