using System;

namespace Enums
{
    /// <summary>
    /// The personality a viewer reads as. A viewer owns exactly one; a chat line declares the set
    /// it is allowed to come out of, so the scared regular never posts the hype line and the troll
    /// never posts the worried one.
    ///
    /// <para>Flags rather than a plain enum because the mask lives on the message side, where a
    /// line usually fits two or three personalities and almost never just one.</para>
    /// </summary>
    [Flags]
    public enum ViewerArchetype
    {
        None = 0,

        /// <summary>Cheers, hypes, spams caps.</summary>
        Hype = 1 << 0,

        /// <summary>Worries out loud, tells the streamer to run.</summary>
        Scared = 1 << 1,

        /// <summary>Mocks, jokes, provokes.</summary>
        Troll = 1 << 2,

        /// <summary>Gives orders and spoilers. "vai pra esquerda", "ja tentou a porta?"</summary>
        Backseat = 1 << 3,

        /// <summary>Barely talks, short and flat when it does.</summary>
        Lurker = 1 << 4,

        Everyone = Hype | Scared | Troll | Backseat | Lurker
    }
}
