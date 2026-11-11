// Original home scene UI; explicit local service bindings.
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.World.NativeCamera;
using SafeArea=Zjcs.Official.HomeScreenUI.SafeArea;
namespace Zjcs.Official.HomeSceneUiControls
{
using System;
using UnityEngine;

public class AwardRetrieveNpcDirectionGuider : AHomeDirectionGuider
{
	public Vector3 PosOffset;

	public Transform Slot;

	private IHomeDirectionNpcData mNpcDataBase;

	private GameObject mNpc;




	protected override void OnEnable()
	{
		base.OnEnable();
		Services.OnHomeNpcRefresh.AddListener(OnNpcRefresh);
		OnNpcRefresh();
	}

	protected override void OnDisable()
	{
		Services.OnHomeNpcRefresh.RemoveListener(OnNpcRefresh);
		base.OnDisable();
	}

	protected override Vector3 GetInitPos()
	{
		if (mNpcDataBase == null)
		{
			return Vector3.zero;
		}
		Transform spawnPoint = Services.GetSpawnPoint(mNpcDataBase.HomePositionIndex);
		return ((spawnPoint == null) ? Vector3.zero : spawnPoint.position) + PosOffset;
	}

	private void OnNpcRefresh()
	{
		mNpcDataBase = Services.HomeNpcDatas.Find((IHomeDirectionNpcData npc) => npc.Id == Services.AwardRetrieveHomeNpcId && npc.NpcType == NpcType.AwardRetrieve);
		if (mNpcDataBase != null)
		{
			InitPos();
			SetActive(value: true);
			if (mNpc == null)
			{
				HomeDirectionNpcInfo npcInfo = Services.GetNpcInfo(mNpcDataBase.Id);
				GameObject result = Services.LoadPrefab(npcInfo.FigureModelUI).Result;
				mNpc = Services.InstantiatePrefab(result, Slot);
				Services.FreezeSkeletonGraphic(mNpc);
			}
		}
		else
		{
			SetActive(value: false);
			Hide();
		}
	}
}
}
