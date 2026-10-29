// Complete original client class with consumed player/native/container type boundaries.
using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.EntityLifecycle;
using Object = UnityEngine.Object;

using UnityEngine;

namespace Zjcs.Official.MapRootVisibility
{
public class MyGridVisibilityAgent : MyComponent<FogVisibilityAgentData, object>
{
	public CloudController2 CloudController;

	private FogGroundData mFogGroundData;

	private Pos2d? mUnlockCellSrcPosOverridePos;

	public bool Ban;

	private bool mBanCloudControllerReveal;

	private List<Pos2d> mBanCloudPosList = new List<Pos2d>();

	private HashSet<Pos2d> TargetPosSet = new HashSet<Pos2d>();

	public IFogVisibilityPlayer Player {get;set;}
	private Zjcs.Official.EntityContainers.MyContainer ClientContainer => (Zjcs.Official.EntityContainers.MyContainer)myContainer;

	public event Action<Pos2d, bool, bool> OnPosVisibilityUpdate;

	public event Action<Pos2d> OnTargetPosRemove;

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		mFogGroundData = Component.GetComponent<FogGroundData>();
		InitCloudController(CloudController);
		if (!ClientContainer.Inited)
		{
			ClientContainer.OnInited += OnInited;
		}
		else
		{
			OnInited();
		}
		Component.OnPosVisibilityUpdate += FogVisibilityAgentData_OnPosVisibilityUpdate;
	}

	public override void OnEnd(object userData)
	{
		((MonoBehaviour)this).StopAllCoroutines();
		base.OnEnd(userData);
		RemoveListen();
		Component.OnPosVisibilityUpdate -= FogVisibilityAgentData_OnPosVisibilityUpdate;
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

	private void FogVisibilityAgentData_OnPosVisibilityUpdate(Pos2d pos, bool preVi, bool postVi, bool forRegionActive)
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
		FogCharacterData character = Player.Character;
		var id = character.FindWorldTransferAction();
		MyEntity entity = Player.GetEntity(id);
		if ((Object)(object)entity != (Object)null)
		{
			IFogTransferClient component = Player.GetTransferClient(entity);
			if (component != null && component.Component != null)
			{
				return component.Component.Pos;
			}
		}
		return Player.MyMeUiGridPos ?? character.GridPos;
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
		if (!((Object)(object)cloudController == (Object)null))
		{
			Transform transform = ((Component)cloudController.Cloud).transform;
			FogGroundInfo info = mFogGroundData.Info;
			Pos2d pos2d = new Pos2d(info.Size.X / 2, info.Size.Y / 2);
			transform.localPosition = new Vector3((float)pos2d.X + info.OriginPos.x, (float)pos2d.Y + info.OriginPos.y, transform.localPosition.z);
			transform.localScale = new Vector3((float)info.Size.X * 1f * 1.5f, (float)info.Size.Y * 1f * 1.5f, 1f);
			cloudController.Initialize(Component, mFogGroundData);
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
		if ((Object)(object)myContainer != (Object)null)
		{
			ClientContainer.OnInited -= OnInited;
		}
		if (Component != null)
		{
			Component.OnOneCellUnlock -= Component_OneCellUnlock;
			Component.Handle.OnMultiCellsUnlock -= Component_OnMultiCellsUnlock;
		}
	}
}

}
