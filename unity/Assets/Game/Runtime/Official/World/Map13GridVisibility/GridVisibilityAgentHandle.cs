// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;




namespace Zjcs.Official.World.Map13GridVisibility
{
public class GridVisibilityAgentHandle : IEcContainerStartEntityEventReceiver, IEventReceiver
{
	private GridVisibilityAgentComponent mGridVisibilityAgent;

	private EcContainer mContainer;

	public event Action<HashSet<Pos2d>, bool> OnMultiCellsUnlock;

	public void Init(GridVisibilityAgentComponent gridVisibilityAgent)
	{
		mGridVisibilityAgent = gridVisibilityAgent;
		mContainer = gridVisibilityAgent.Container;
		mContainer.StartEntityEvent += (IEcContainerStartEntityEventReceiver)this;
	}

	public void OnDestroy()
	{
		if (mContainer != null)
		{
			mContainer.StartEntityEvent -= (IEcContainerStartEntityEventReceiver)this;
		}
	}

	void IEcContainerStartEntityEventReceiver.Container_OnEntityStart(VisibilityEntity entity, bool newEntity)
	{
		if (newEntity)
		{
			UnlockCellsByEntity(entity);
		}
	}

	public void CheckUnlockMultiCellsByEntityOnPos(GridGroundComponent gridGround, Pos2d pos)
	{
		GamePosData posData = gridGround.GetPosData(pos);
		if (posData == null || !posData.ExistEntitys())
		{
			return;
		}
		foreach (VisibilityEntity entity in posData.Entitys)
		{
			UnlockCellsByEntity(entity);
		}
	}

	private void UnlockEntityRelativePosAndAddToList(VisibilityEntity entity, HashSet<Pos2d> unlockPosList, int viewRange)
	{
		GridTransformComponent gridTransform = entity.GridTransform;
		if (gridTransform == null)
		{
			return;
		}
		if (mContainer.RootEntity == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return;
		}
		AreaAgentComponent component = mContainer.RootEntity.GetComponent<AreaAgentComponent>();
		if (mGridVisibilityAgent.Host.TryGetCampOwnerEntity(entity, out var entity2))
		{
			LoEntityCampComponent component2 = entity2.GetComponent<LoEntityCampComponent>();
			if (component2 == null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
			else
			{
				HashSet<Pos2d> loEntityIncludePosList = mGridVisibilityAgent.Host.GetLoEntityIncludePosList(mContainer, component2.Info.CampMonsterEntities);
				UnlockPosListAndAddToList(loEntityIncludePosList, component, unlockPosList);
			}
		}
		else if (CheckNeedUnlockBodyRanges(gridTransform.RangeList, gridTransform.Pos, gridTransform.Info.UnlockWholeRequestCount))
		{
			UnlockPosListAndAddToList(gridTransform.AddOneRangeList, component, unlockPosList);
		}
		void UnlockPosListAndAddToList(HashSet<Pos2d> posList, AreaAgentComponent curAreaAgent, HashSet<Pos2d> curUnlockPosList)
		{
			foreach (Pos2d pos in posList)
			{
				if (!mGridVisibilityAgent.GetPosVisibility(pos))
				{
					int regionId = curAreaAgent.GridToRegion(mContainer.ClassId, pos);
					if (mGridVisibilityAgent.IsSameRegionWithOwner(regionId) && mGridVisibilityAgent.UpdatePosVisibilityAndNotify(pos, visibility: true))
					{
						curUnlockPosList.Add(pos);
					}
				}
			}
		}
	}

	private bool CheckNeedUnlockBodyRanges(List<Pos2d> bodyRanges, Pos2d gridPos, int unlockWholeRequestCount)
	{
		int count = bodyRanges.Count;
		if (count <= 1)
		{
			return true;
		}
		unlockWholeRequestCount = Math.Min(count, unlockWholeRequestCount);
		int num = 0;
		foreach (Pos2d bodyRange in bodyRanges)
		{
			if (mGridVisibilityAgent.CheckPosInGround(bodyRange) && mGridVisibilityAgent.GetPosVisibility(bodyRange))
			{
				num++;
				if (num >= unlockWholeRequestCount)
				{
					return true;
				}
			}
		}
		return false;
	}

	private void UnlockCellsByEntity(VisibilityEntity entity)
	{
		if (entity.GridTransform == null)
		{
			return;
		}
		Pos2d pos = entity.GridTransform.Pos;
		int viewRange = mGridVisibilityAgent.Host.GetSpawnCharacterViewRange(entity.Container.ClassId);
		HashSet<Pos2d> hashSet = new HashSet<Pos2d> { pos };
		UnlockEntityRelativePosAndAddToList(entity, hashSet, viewRange);
		try
		{
			OnMultiCellsUnlock?.Invoke(hashSet, arg2: true);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}
}

}
