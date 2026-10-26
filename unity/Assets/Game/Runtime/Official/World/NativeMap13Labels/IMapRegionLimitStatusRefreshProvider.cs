// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;

public interface IMapRegionLimitStatusRefreshProvider : IDisposable
{
	MapRegionLimitType RegionLimitType { get; }
}

}
