using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
public static class DirectionGuiderUtils
{
public static Vector2 GetGuiderPosition(Vector2 focusObjViewPort, Vector2 itemViewPort, float angleOfLine)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		if (focusObjViewPort == itemViewPort)
		{
			return default;
		}
		float num = (itemViewPort.y - focusObjViewPort.y) / (itemViewPort.x - focusObjViewPort.x);
		if (angleOfLine <= 45f && angleOfLine > -45f)
		{
			return new Vector2(0f, (0f - focusObjViewPort.x) * num + focusObjViewPort.y);
		}
		if (angleOfLine <= 135f && angleOfLine > 45f)
		{
			return new Vector2((0f - focusObjViewPort.y) * (1f / num) + focusObjViewPort.x, 0f);
		}
		if (angleOfLine <= -45f && angleOfLine > -135f)
		{
			return new Vector2((1f - focusObjViewPort.y) * (1f / num) + focusObjViewPort.x, 1f);
		}
		return new Vector2(1f, (1f - focusObjViewPort.x) * num + focusObjViewPort.y);
	}
public static Pos2d GetCenterPos(MiniMapDirectionContext context, int area, int mapClassId, int itemId)
	{
		if (!context.FullScreen.CurrentMiniMap.TryGetMapLiteData(mapClassId, out var liteInfo))
		{
			return Pos2d.Zero;
		}
		MiniMapRegionInfo infoByDropItemId = context.GetRegionInfoByDropItemId(itemId);
		if (infoByDropItemId == null || !infoByDropItemId.Areas.Contains(area))
		{
			if (!liteInfo.TryGetAreaLiteInfo(area, out var areaLiteInfo))
			{
				return Pos2d.Zero;
			}
			return new Pos2d(areaLiteInfo.TotalPos.X / areaLiteInfo.TotalCount, areaLiteInfo.TotalPos.Y / areaLiteInfo.TotalCount);
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (int area2 in infoByDropItemId.Areas)
		{
			if (liteInfo.TryGetAreaLiteInfo(area2, out var areaLiteInfo2))
			{
				num += areaLiteInfo2.TotalPos.X;
				num2 += areaLiteInfo2.TotalPos.Y;
				num3 += areaLiteInfo2.TotalCount;
			}
		}
		return new Pos2d(num / num3, num2 / num3);
	}
}
}
