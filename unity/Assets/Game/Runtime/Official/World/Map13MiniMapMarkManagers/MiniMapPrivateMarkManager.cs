using System.Collections.Generic;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public class MiniMapPrivateMarkManager : MiniMapMarkManager, IEcEntityWorldStageComponentStateUpdateEventReceiver, IEventReceiver
{
	private MiniMapManagedContext MarkContext => (MiniMapManagedContext)MiniMap;
	private Dictionary<EcEntityIdentifier, IMiniMapManagedStage> mWorldStageDict = new Dictionary<EcEntityIdentifier, IMiniMapManagedStage>();

	protected override MapRegionStatus HandleStatus => MapRegionStatus.Private;

	protected override void OnDestroy()
	{
		foreach (KeyValuePair<EcEntityIdentifier, IMiniMapManagedStage> item in mWorldStageDict)
		{
			item.Value.StateUpdateEvent -= (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;
		}
		mWorldStageDict.Clear();
		base.OnDestroy();
	}

	protected override bool CreateEntityIconCheck(int region, IMiniMapManagedStage worldStage, MiniMapMarkExtendInfo info)
	{
		if (worldStage == null)
		{
			return false;
		}
		if (info.MiniMapMarkHideInEnd && (!worldStage.IsActive() || worldStage.StateEqual(info.CompleteState)))
		{
			return false;
		}
		return true;
	}

	protected override void RegistMark(MiniMapMarkUIBase markUI, EcEntityIdentifier entId)
	{
		IMiniMapSpawnEntity entity = MarkContext.ManagedQueries.GetEntity(entId);
		if (entity != null)
		{
			IMiniMapManagedStage component = entity.GetComponent<IMiniMapManagedStage>();
			if (component != null)
			{
				AddListen(component);
			}
		}
	}

	protected override void UnregistMark(MiniMapMarkUIBase markUI, EcEntityIdentifier entId)
	{
		if (mWorldStageDict.TryGetValue(entId, out var value))
		{
			RemoveListen(value);
		}
	}

	private void AddListen(IMiniMapManagedStage stageComp)
	{
		stageComp.StateUpdateEvent += (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;
		mWorldStageDict.Add(stageComp.Entity.Id, stageComp);
	}

	private void RemoveListen(IMiniMapManagedStage stageComp)
	{
		stageComp.StateUpdateEvent -= (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;
		mWorldStageDict.Remove(stageComp.Entity.Id);
	}

	void IEcEntityWorldStageComponentStateUpdateEventReceiver.WorldStageComponent_OnStateUpdate(IMiniMapManagedStage stageComp, int preState, object param)
	{
		MiniMapMarkExtendInfo entityExtendParameterInfo = MarkContext.MarkDataQueries.GetEntityExtend(stageComp.Entity.ClassId);
		if (entityExtendParameterInfo.MiniMapMarkHideInEnd && stageComp.StateEqual(entityExtendParameterInfo.CompleteState))
		{
			DestroyMark(stageComp.Entity.Id);
		}
	}
}

}
