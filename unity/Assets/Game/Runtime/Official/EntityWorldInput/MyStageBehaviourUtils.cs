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

public static class MyStageBehaviourUtils
{
	public static IMyStageBehaviour GetMyStageBehaviour(IWorldInputRootLookup mapRootMgr, ECViewEntityData entity)
	{
		if (!mapRootMgr.TryGetEntity(entity.Id, out var myEnt))
		{
			return null;
		}
		ECViewDriver componentInChildren = ((Component)myEnt).GetComponentInChildren<ECViewDriver>();
		IMyStageBehaviour myStageBehaviour = null;
		if ((Object)(object)componentInChildren != (Object)null)
		{
			myStageBehaviour = componentInChildren.GetView<ECStageBehaviourBaseView>();
		}
		if (myStageBehaviour == null)
		{
			myStageBehaviour = entity.GetComponent<IMyStageBehaviour>();
		}
		return myStageBehaviour;
	}

	public static bool TryGetMyStageBehaviour(IWorldInputRootLookup mapRootMgr, ECViewEntityData entity, out IMyStageBehaviour myStageBehaviour)
	{
		myStageBehaviour = GetMyStageBehaviour(mapRootMgr, entity);
		return myStageBehaviour != null;
	}
}

}
