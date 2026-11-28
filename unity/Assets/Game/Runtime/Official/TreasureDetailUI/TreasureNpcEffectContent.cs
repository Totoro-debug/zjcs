using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.HomePlayerInfoUI;
using PlayerCostMoneyContent=Zjcs.Official.HomeCurrencyWindowsUI.PlayerCostMoneyContent;
using IntroductionPopBtn=Zjcs.Official.HomePlayerInfoUI.HomeIntroductionPopBtn;
using TreasureState=Zjcs.Official.TreasureCultivation.TreasureState;
using PropType=System.Int32;
using SubRank=System.Int32;
using Rank=System.Int32;
using Object=UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {

public class TreasureNpcEffectContent : UIBase
{
	[SerializeField]
	private TreasureViewPrefabReference mNpcEffectItemPrefab;

	[SerializeField]
	private RectTransform mItemParent;

	[SerializeField]
	private GameObject mNpcEffectGo;

	[SerializeField]
	private GameObject mTxtEffectTitleGo;

	private List<int> mNpcIdList;

	private ProfessionResourceOperationBag mDisposableBag;

	private ITreasureNpcEffectServices Services;
    public void Configure(ITreasureNpcEffectServices services) => Services=services??throw new ArgumentNullException(nameof(services));

	public void Init(List<int> npcIdList)
	{
		InitWrapper(() =>
		{
			mNpcIdList = npcIdList;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag = new ProfessionResourceOperationBag();
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		((Transform)(object)mItemParent).DestroyChildren();
		mDisposableBag?.Dispose();
		base.OnDisableAndInited();
	}

	private void Refresh()
	{
		if ((Object)(object)mNpcEffectGo == (Object)null || (Object)(object)mTxtEffectTitleGo == (Object)null)
		{
			return;
		}
		((Transform)(object)mItemParent).DestroyChildren();
		bool flag = mNpcIdList.Count > 0;
		mNpcEffectGo.SetActive(flag);
		mTxtEffectTitleGo.SetActive(flag);
		if (!flag)
		{
			return;
		}
		GameObject result = mDisposableBag.Add(Services.Resources.LoadAssetByGuid<GameObject>(mNpcEffectItemPrefab.AssetGuid)).Result;
		foreach (int mNpcId in mNpcIdList)
		{
			Services.Instantiate(result,mItemParent).GetComponent<TreasureNpcEffectItem>().Init(mNpcId);
		}
	}
}

}
