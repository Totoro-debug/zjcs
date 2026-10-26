// Selected official Map12 world-label implementation; see source receipt.
namespace Zjcs.Official.World.NativeMap12Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using System;

public interface IMapRegionLimitStatusRefreshProvider : IDisposable
{
	MapRegionLimitType RegionLimitType { get; }
}

}
