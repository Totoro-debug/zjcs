namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public class LegionMapDataListener : MonoBehaviour
{
	public IHomeBottomBarEntryServices Services { get; set; }

	private bool mSubscribe;

	private IHomeLegionMapDataManager LegionMapDataManager => Services.LegionMapDataManager;

	private void OnEnable()
	{
		Services.MainPlayer.OnLegionUpdate += new Action<long, long>(OnLegionUpdate);
		if (Services.MainPlayer.LegionId > 0)
		{
			DoRegister();
		}
	}

	private void OnDisable()
	{
		if (Services.MainPlayer != null)
		{
			Services.MainPlayer.OnLegionUpdate -= new Action<long, long>(OnLegionUpdate);
		}
		DoUnregister();
	}

	private void OnLegionUpdate(long preId, long id)
	{
		if (Services.MainPlayer.LegionId > 0)
		{
			DoRegister();
		}
		else
		{
			DoUnregister();
		}
	}

	private void DoRegister()
	{
		if (!mSubscribe)
		{
			mSubscribe = true;
			LegionMapDataManager.QueryAndSubscribeHangUpAffairMsg();
			LegionMapDataManager.QueryAndSubscribeBuildingData();
			LegionMapDataManager.QueryAndSubscribeMemberHangUpMsg();
			LegionMapDataManager.QueryAndSubscribeLifeHangUpData();
			LegionMapDataManager.QueryAndSubscribeItemBagData();
		}
	}

	private void DoUnregister()
	{
		if (mSubscribe)
		{
			mSubscribe = false;
			if (LegionMapDataManager != null)
			{
				LegionMapDataManager.UnsubscribeHangUpAffairMsg();
				LegionMapDataManager.UnsubscribeBuildingData();
				LegionMapDataManager.UnsubscribeMemberHangUpMsg();
				LegionMapDataManager.UnsubscribeLifeHangUpData();
				LegionMapDataManager.UnsubscribeItemBagData();
			}
		}
	}
}

}
