// Migrated original application fade handler; see Map13 source receipt.
using System;

namespace Zjcs.Official.World.Map13EntityFadeHandlers
{
public interface IMap13EntityFadeOutBinding
{
    // Original MyEntity multicast delegate, including its original last-result
    // semantics. The local entity host owns dispatch and actual destruction.
    Func<bool> FadeOutAction { get; set; }
}
public interface IMap13FadeMapState
{
    bool Initialized { get; }
    bool IsFront { get; }
    Map13LoadingPanelState LoadingPanel { get; }
}
public sealed class Map13LoadingPanelState
{
    public SplashStatus SplashStatus;
}

}
