// Complete original radar hint; see map14 fast-kill source receipt.
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityRadar;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.LegacyWorldStages;
namespace Zjcs.Official.EntityRadarFastKill
{

public class MapControllerEntityRadarTipFastKill : MapControllerEntityRadarTipBase
{
    public IRadarFastKillServices Services { get; set; }
	private IRadarFastKillRoot mMapRootManagerProxy;

	private IRadarFastKillPosition mMyMapPositionHandler;

	public override RadarTipType TipType => RadarTipType.FastKill;

	public override bool CanShowTip(MyEntity entity)
	{
		return CheckCanFastKill(entity);
	}

	private bool CheckCanFastKill(MyEntity entity)
	{
		if (mMapRootManagerProxy == null)
		{
			mMapRootManagerProxy = Services.ResolveRoot(entity);
		}
		if (mMyMapPositionHandler == null)
		{
			mMyMapPositionHandler = Services.ResolvePosition(entity);
		}
		ECViewEntityData entity2 = entity.Entity;
		if (Services.Queries.TryGetCampOwnerEntity(entity2, out var entity3))
		{
			EcEntityIdentifier entIdByLoEntId = Services.GetEntIdByLoEntId(entity3.ClassId);
			if ((Object)(object)mMapRootManagerProxy.GetEntity(entIdByLoEntId) == (Object)null)
			{
				return false;
			}
		}
		int region = mMyMapPositionHandler.QueryRegion(entity.myGridTransform.Pos);
		RadarFastKillPlayer mainPlayer = Services.MainPlayer;
		LegacyWorldStageComponentData component = entity2.GetComponent<LegacyWorldStageComponentData>();
		if (component.InteractionRedirectStage != null)
		{
			component = component.InteractionRedirectStage;
			entity2 = component.Entity;
		}
		IRadarStageFast component2 = entity2.GetComponent<IRadarStageFast>();
		if (component2 == null)
		{
			return false;
		}
		return Services.Queries.IsFastGame(mMapRootManagerProxy.CurrentMapClassId, region, component2.GetMapFastType(), mainPlayer.Level, mainPlayer.SubRank, mainPlayer.CombatRating);
	}
}

}
