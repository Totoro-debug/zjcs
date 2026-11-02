using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zjcs.Official.MapRootFog
{
public sealed class LocalGridVisibilityAgent : IDisposable
{
	public CloudController2 CloudController;

	private readonly FogGroundData mGridGroundComponent;
	private readonly FogVisibilityData visibility;
	private readonly GridVisibilityAgentComponent Component;
	private readonly Func<Pos2d?> transferPosition;
	private readonly Func<Pos2d> characterPosition;

	public LocalGridVisibilityAgent(GridVisibilityAgentComponent component, FogVisibilityData visibility, FogGroundData ground, CloudController2 cloud, Func<Pos2d?> transferPosition, Func<Pos2d> characterPosition)
	{
		Component = component; this.visibility = visibility; mGridGroundComponent = ground; CloudController = cloud;
		this.transferPosition = transferPosition; this.characterPosition = characterPosition;
	}

	private Pos2d? mUnlockCellSrcPosOverridePos;

	public bool Ban;

	private bool mBanCloudControllerReveal;

	private List<Pos2d> mBanCloudPosList = new List<Pos2d>();

	private HashSet<Pos2d> TargetPosSet = new HashSet<Pos2d>();

	public event Action<Pos2d, bool, bool> OnPosVisibilityUpdate;

	public event Action<Pos2d> OnTargetPosRemove;

	public void Start()
	{
		InitCloudController(CloudController);
		OnInited();
		Component.OnPosVisibilityUpdate += GridVisibilityAgentComponent_OnPosVisibilityUpdate;
	}

	public void Dispose()
	{
		RemoveListen();
		Component.OnPosVisibilityUpdate -= GridVisibilityAgentComponent_OnPosVisibilityUpdate;
	}

	public void SetUnlockCellSrcPosOverridePos(Pos2d? pos)
	{
		mUnlockCellSrcPosOverridePos = pos;
	}

	public void BanCloudControllerReveal(bool ban)
	{
		mBanCloudControllerReveal = ban;
		if (mBanCloudControllerReveal)
		{
			return;
		}
		Pos2d unlockCellSrcPos = GetUnlockCellSrcPos();
		foreach (Pos2d mBanCloudPos in mBanCloudPosList)
		{
			CloudControllerRevealPos(mBanCloudPos, unlockCellSrcPos);
		}
		mBanCloudPosList.Clear();
	}

	private void GridVisibilityAgentComponent_OnPosVisibilityUpdate(Pos2d pos, bool preVi, bool postVi, bool forRegionActive)
	{
		OnPosVisibilityUpdate?.Invoke(pos, preVi, postVi);
	}

	private void OnInited()
	{
		RemoveListen();
		AddListen();
	}

	private Pos2d GetUnlockCellSrcPos()
	{
		if (mUnlockCellSrcPosOverridePos.HasValue)
		{
			return mUnlockCellSrcPosOverridePos.Value;
		}
		return transferPosition?.Invoke() ?? characterPosition();
	}

	private void Component_OneCellUnlock(Pos2d pos, bool showAnima)
	{
		if (!((Object)(object)CloudController == (Object)null))
		{
			if (showAnima)
			{
				Pos2d unlockCellSrcPos = GetUnlockCellSrcPos();
				CloudControllerRevealPos(pos, unlockCellSrcPos);
			}
			else
			{
				CloudControllerRemovePos(pos);
			}
		}
	}

	private void Component_OnMultiCellsUnlock(HashSet<Pos2d> unlockPosInfoList, bool showAnima)
	{
		if ((Object)(object)CloudController == (Object)null)
		{
			return;
		}
		if (showAnima)
		{
			CloudRevealByDist(unlockPosInfoList, GetUnlockCellSrcPos());
			return;
		}
		foreach (Pos2d unlockPosInfo in unlockPosInfoList)
		{
			CloudControllerRemovePos(unlockPosInfo);
		}
	}

	private void CloudRevealByDist(HashSet<Pos2d> unlockPosInfoList, Pos2d mePos)
	{
		foreach (Pos2d unlockPosInfo in unlockPosInfoList)
		{
			CloudControllerRevealPos(unlockPosInfo, mePos);
		}
	}

	private void CloudControllerRevealPos(Pos2d pos, Pos2d playerPos)
	{
		if (mBanCloudControllerReveal)
		{
			mBanCloudPosList.Add(pos);
		}
		else
		{
			CloudController.Reveal(pos, playerPos);
		}
	}

	private void CloudControllerRemovePos(Pos2d pos)
	{
		CloudController.Remove(pos);
	}

	private void InitCloudController(CloudController2 cloudController)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)cloudController == (Object)null))
		{
			Transform transform = ((Component)cloudController.Cloud).transform;
			FogGroundInfo info = mGridGroundComponent.Info;
			Pos2d pos2d = new Pos2d(info.Size.X / 2, info.Size.Y / 2);
			transform.localPosition = new Vector3((float)pos2d.X + info.OriginPos.x, (float)pos2d.Y + info.OriginPos.y, transform.localPosition.z);
			transform.localScale = new Vector3((float)info.Size.X * 1f * 1.5f, (float)info.Size.Y * 1f * 1.5f, 1f);
			cloudController.Initialize(visibility, mGridGroundComponent);
		}
	}

	public void AddTargetPos(Pos2d gridPos)
	{
		TargetPosSet.Add(gridPos);
	}

	public bool RemoveTargetPos(Pos2d gridPos)
	{
		return TargetPosSet.Remove(gridPos);
	}

	public void OnTargetRemove(Pos2d gridPos)
	{
		if (TargetPosSet.Contains(gridPos))
		{
			OnTargetPosRemove?.Invoke(gridPos);
		}
	}

	private void AddListen()
	{
		Component.OnOneCellUnlock += Component_OneCellUnlock;
		Component.Handle.OnMultiCellsUnlock += Component_OnMultiCellsUnlock;
	}

	private void RemoveListen()
	{
		if (Component != null)
		{
			Component.OnOneCellUnlock -= Component_OneCellUnlock;
			Component.Handle.OnMultiCellsUnlock -= Component_OnMultiCellsUnlock;
		}
	}
}

}
