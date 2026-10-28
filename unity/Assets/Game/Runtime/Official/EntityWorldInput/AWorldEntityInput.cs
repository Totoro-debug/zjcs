// Original client/shared input source; see world input source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using WorldStageComponent = Zjcs.Official.LegacyWorldStages.LegacyWorldStageComponentData;
using EntityOwnershipType = Zjcs.Official.EntityTipDotViews.EntityOwnershipType;
using IMyVisibility = Zjcs.Official.EntityLifecycle.IMyVisibility;
namespace Zjcs.Official.EntityWorldInput
{

[DisallowMultipleComponent]
public abstract class AWorldEntityInput : MonoBehaviour
{
	public IWorldInputGeometry Geometry { get; set; }
    public IWorldInputTeamContext TeamContext { get; set; }
	public abstract bool ClickOverriden { get; }

	public abstract MyEntity MyEntity { get; }

		public IWorldInputSupport MapInput { get; set; }

		public IWorldInputRootLookup BaseMapRootManagerProxy { get; set; }

	public abstract WorldEntityInputPriority Priority { get; }

	public abstract Sprite GetEntitySprite();

	public abstract string GetEntityMenuItemKey();

	public abstract bool ClickByColliderCheck();

	public abstract bool ClickByGridCheck();

	public abstract bool ConflictWithInput(AWorldEntityInput input);

	public abstract void OnClick(Pos2d pos, bool isLogicClick);

	public virtual AWorldEntityInput GetRedirectInput()
	{
		return null;
	}

	public virtual GridRect GetEntityGridRect()
	{
		return Geometry.GetPosListGridRect(MyEntity.myGridTransform.GetClientChooseBodyRange());
	}

	public virtual bool CheckLimitedByTeam()
	{
		if (TeamContext.MainTeam == null)
		{
			return true;
		}
		if (!TeamContext.IsLeader)
		{
			return false;
		}
		if (!TeamContext.TryGetStatusByContainerId(MyEntity.Id.ContainerId, out var status))
		{
			return false;
		}
		if (TeamContext.TeamCheck(BaseMapRootManagerProxy.CurrentMapClassId, status))
		{
			return true;
		}
		return false;
	}
}

}
