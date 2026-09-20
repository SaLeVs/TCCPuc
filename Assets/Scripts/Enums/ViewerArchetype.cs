using System;

namespace Enums
{
    /// <summary>
    /// The personality a viewer reads as, a viewer owns exactly one; a chat line declares the set it is allowed to come out of
    /// </summary>
    [Flags]
    public enum ViewerArchetype
    {
        None = 0,
        Hype = 1 << 0,
        Scared = 1 << 1,
        Troll = 1 << 2,
        Backseat = 1 << 3,
        Lurker = 1 << 4,
        Everyone = Hype | Scared | Troll | Backseat | Lurker
    }
}
