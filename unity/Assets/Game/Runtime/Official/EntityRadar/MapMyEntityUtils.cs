// Original client radar implementation; see entity radar source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap14Labels;
using Zjcs.Official.EntityContainers;
using MyContainer = Zjcs.Official.EntityContainers.MyContainer;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using IGameObjectPoolItem = Zjcs.Official.StoryPlayables.IGameObjectPoolItem;
namespace Zjcs.Official.EntityRadar
{
public static class MapMyEntityUtils
{
public static Vector3 GetOffsetByAnchor(MyEntity entity, HorizontalAnchor horAnchor, VerticalAnchor verAnchor)
	{











		IRadarBodyInfo info = entity.Entity.GetComponent<IRadarBodyInfo>();
		Vector2Int zero = Vector2Int.zero;
		Vector2Int zero2 = Vector2Int.zero;
		foreach (RadarBodyPosInfo bodyRangeInfo in info.BodyRangeInfos)
		{
			Pos2d pos = bodyRangeInfo.Pos;
			if (pos.X < zero.x)
			{
				zero.x = pos.X;
			}
			if (pos.Y < zero.y)
			{
				zero.y = pos.Y;
			}
			if (pos.X > zero2.x)
			{
				zero2.x = pos.X;
			}
			if (pos.Y > zero2.y)
			{
				zero2.y = pos.Y;
			}
		}
		Vector2 zero3 = Vector2.zero;
		zero3.x = horAnchor switch
		{
			HorizontalAnchor.Left => (float)zero.x - 0.5f, 
			HorizontalAnchor.Center => (float)(zero.x + zero2.x) / 2f, 
			HorizontalAnchor.Right => (float)zero2.x + 0.5f, 
			_ => throw new ArgumentOutOfRangeException("horAnchor", horAnchor, null), 
		};
		zero3.y = verAnchor switch
		{
			VerticalAnchor.Top => (float)zero2.y + 0.5f, 
			VerticalAnchor.Mid => (float)(zero.y + zero2.y) / 2f, 
			VerticalAnchor.Bottom => (float)zero.y - 0.5f, 
			_ => throw new ArgumentOutOfRangeException("verAnchor", verAnchor, null), 
		};
		return (Vector3)((zero3 - info.RealCenter) * 1f);
	}
}
}
