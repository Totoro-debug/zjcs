// Selected official condition methods; explicit local service boundaries.
using System;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomeTaskUI
{
public abstract class HomeTaskJumpCondition
{
    public IHomeTaskJumpHost Host;
    public int MapClassId,Region;
    public string Tag;
    public bool IsJumpable;
    public abstract bool CanJump();
    public abstract void Jump(long taskId,Action<object> completed);
    public virtual bool CanTrace()=>false;
}
public sealed class ClientMapGameTotalCountCondition:HomeTaskJumpCondition
{
private bool IsAllMap => MapClassId == -1;
public override bool CanJump()
	{
		if (!IsJumpable)
		{
			return false;
		}
		if (IsAllMap)
		{
			return Host.WorldMapRegionProgressCount > 0;
		}
		return !Host.IsOnMap(MapClassId);
	}
public override void Jump(long taskId, Action<object> jumpEndCallback)
	{
		int mapClassId = MapClassId;
		if (!IsAllMap && !Host.IsMapAccessible(mapClassId))
		{
			Host.Broadcast("MainlandPanel.MapUnLockLimit");
		}
		else
		{
			Host.NavigateRegion(MapClassId, Region, jumpEndCallback);
		}
	}
}

public sealed class ClientWorldMapProgressCondition:HomeTaskJumpCondition
{
public override bool CanJump()
	{
		if (!IsJumpable)
		{
			return false;
		}
		if (!Host.IsOnMap())
		{
			return true;
		}
		if (Host.IsOnMap(MapClassId))
		{
			return false;
		}
		if (!Host.HasMapContainer)
		{
			return false;
		}
		int classId = Host.CurrentMapClassId;
		if (classId != Host.GetGroupCurrentMap(classId))
		{
			return true;
		}
		return false;
	}
public override void Jump(long taskId, Action<object> jumpEndCallback)
	{
		if (Host.IsOnMap(MapClassId) || Host.TryOpenMapEnterByFastClear(MapClassId))
		{
			return;
		}
		if (!Host.IsMapAccessible(MapClassId))
		{
			if (Host.TryOpenMapGalaxyPanel(MapClassId))
			{
				return;
			}
			DateTime dateTime = Host.GetDateTime();
			DateTime mapRegionUnlockTime = Host.GetRegionUnlockTime(MapClassId, Region);
			if (mapRegionUnlockTime > dateTime)
			{
				object cultureTimeSpanArg = Host.FormatDetailedTimeSpan(mapRegionUnlockTime - dateTime);
				Host.Broadcast("MapUnLockTip", cultureTimeSpanArg);
				return;
			}
		}
		int groupCurrentMapClassIdByMapClassIdAutoWithTagId = Host.GetGroupCurrentMap(MapClassId);
		int num = ((MapClassId == groupCurrentMapClassIdByMapClassIdAutoWithTagId) ? MapClassId : groupCurrentMapClassIdByMapClassIdAutoWithTagId);
		CultureTextArg cultureTextArg = new CultureTextArg(Host.GetRegionNameKey(num, Region));
		HomeTaskJumpText textArg;
		if (MapClassId == num)
		{
			DateTime dateTime2 = Host.GetDateTime();
			if (Host.IsRegionUnlocked(MapClassId, Region, dateTime2))
			{
				textArg = new HomeTaskJumpText("jump_to_map_tip1", cultureTextArg);
			}
			else
			{
				int region = Region;
				HomeTaskJumpRegion previousRegion = Host.GetPreviousRegion(MapClassId, Region);
				if (previousRegion != null)
				{
					region = previousRegion.Region;
				}
				CultureTextArg cultureTextArg2 = new CultureTextArg(Host.GetRegionNameKey(num, region));
				textArg = new HomeTaskJumpText("jump_to_map_tip2", cultureTextArg2);
			}
		}
		else
		{
			if (Host.TryOpenMapGalaxyPanel(num))
			{
				return;
			}
			int region2 = Region;
			if (Host.TryGetLastMapEntry(num, out var bestRegion))
			{
				region2 = bestRegion;
			}
			CultureTextArg cultureTextArg3 = new CultureTextArg(Host.GetRegionNameKey(num, region2));
			textArg = new HomeTaskJumpText("jump_to_map_tip2", cultureTextArg3);
		}
		Host.Popup(dialog =>
		{
			dialog.Init(textArg, () =>
			{
				// The bound map owner retains the dialog scene scope.
				Host.GotoMapRegion(MapClassId, Region);
			});
		});
	}
public override bool CanTrace()
	{
		if (!Host.IsOnMap())
		{
			return false;
		}
		if (Host.IsInTheMap(MapClassId))
		{
			return true;
		}
		return Host.CanTraceEntityToJump(MapClassId, Region);
	}
}

public sealed class ClientMapLogicEntityEndByTagCondition:HomeTaskJumpCondition
{
public override bool CanJump()
	{
		if (IsJumpable)
		{
			return !IsInTargetMap();
		}
		return false;
	}
public override void Jump(long taskId, Action<object> jumpEndCallback)
	{
		if (IsInTargetMap())
		{
			return;
		}
		Host.Popup(dialog =>
		{
			HomeTaskJumpText mapRegionDesc = GetMapRegionDesc();
			dialog.Init(mapRegionDesc, () =>
			{
				Host.TraceTaskWhenEnterMap(taskId);
				HomeTaskJumpTag mapLogicEntityTagInfo = Host.GetTag(Tag);
				Host.EnterNearEntity(mapLogicEntityTagInfo.MapClass, mapLogicEntityTagInfo.LogicId);
			});
		});
	}
private HomeTaskJumpText GetMapRegionDesc()
	{
		HomeTaskJumpTag mapLogicEntityTagInfo = Host.GetTag(Tag);
		HomeTaskJumpRegion containerEntityInfo = Host.GetEntityRegion(mapLogicEntityTagInfo.MapClass, mapLogicEntityTagInfo.LogicId);
		CultureTextArg cultureTextArg = new CultureTextArg(Host.GetRegionNameKey(mapLogicEntityTagInfo.MapClass, containerEntityInfo.Region));
		return new HomeTaskJumpText("jump_to_map_tip0", cultureTextArg);
	}
private bool IsInTargetMap()
	{
		return Host.IsInTheMap(Host.GetTag(Tag).MapClass);
	}
public override bool CanTrace()
	{
		return IsInTargetMap();
	}
}

public sealed class ClientMapRegionGoddessCondition:HomeTaskJumpCondition
{
public override bool CanJump()
	{
		if (IsJumpable)
		{
			return !Host.IsOnMap(MapClassId);
		}
		return false;
	}
public override void Jump(long taskId, Action<object> jumpEndCallback)
	{
		if (Host.IsOnMap(MapClassId))
		{
			return;
		}
		if (!Host.IsMapAccessible(MapClassId))
		{
			Host.Broadcast("MainlandPanel.MapUnLockLimit");
			return;
		}
		Host.Popup(dialog =>
		{
			CultureTextArg cultureTextArg = new CultureTextArg(Host.GetRegionNameKey(MapClassId, Region));
			HomeTaskJumpText textArgs = new HomeTaskJumpText("jump_to_map_tip1", cultureTextArg);
			dialog.Init(textArgs, () =>
			{
				// The bound map owner retains the dialog scene scope.
				Host.GotoMapRegion(MapClassId, Region);
			});
		});
	}
}

public sealed class ClientTotalOpenChestCondition:HomeTaskJumpCondition
{
public override bool CanJump()
	{
		if (IsJumpable)
		{
			return Host.IsOnMap();
		}
		return false;
	}
public override void Jump(long taskId, Action<object> jumpEndCallback)
	{
		Host.Popup(dialog =>
		{
			dialog.Init("jump_to_home_tip0", () =>
			{
				//IL_000a: Unknown result type (might be due to invalid IL or missing references)
				Host.LeaveMap();
			});
		});
	}
}

public sealed class ClientDemonBattleLayerPassCondition:HomeTaskJumpCondition
{
public override bool CanJump()
	{
		return IsJumpable;
	}
public override void Jump(long taskId, Action<object> jumpEndCallback)
	{
		Host.NavigateFantasyLadder(jumpEndCallback);
	}
}
}
