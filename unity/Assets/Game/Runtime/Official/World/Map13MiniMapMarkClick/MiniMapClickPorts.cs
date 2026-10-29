using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapClickContext { Object MyGridVisibilityAgent { get; } }
    public interface IMiniMapClickInput { int CanTeamClickPosCheck(int map,Pos2d position); }
    public interface IMiniMapClickCameraBrain { void SetViewPos(Vector3 position); }
    public interface IMiniMapClickCameraControl { IMiniMapClickCameraBrain ControlBrain { get; } }
    public interface IMiniMapClickPublisher { void Publish(MiniMapMarkClick.OnClickEventData value); }
    public static class MiniMapClickRules
    {
public static Pos2d MapPosToGrid(MiniMapMaskContext context, int mapClassId, Vector3 pos)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (!context.TryGetMapLiteData(mapClassId, out var liteInfo))
		{
			return Pos2d.Zero;
		}
		return liteInfo.PosToGrid(pos);
	}
}
}
