// Complete original promotion controller; explicit saved-player, parser and window ports.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TreasureInfo=Zjcs.Official.TreasureProperties.TreasureInfo;
using PropType=System.Int32;
using ElementType=Zjcs.Official.TreasureSlots.ElementType;
namespace Zjcs.Official.TreasurePromoteUI {
using Object=UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Linq;





using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TreasureUpgradePanel : MonoBehaviour, IWindowOperator
{
    private ITreasurePromoteServices services;
    private ITreasurePromoteServices Services=>services??throw new InvalidOperationException("Bind original treasure promotion services before activation.");
    public void Configure(ITreasurePromoteServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	private enum TreasureUpgradeStatus
	{
		Upgrade,
		Wakeup,
		Max
	}

	[SerializeField]
	private Transform mAwakeCostSlot;

	[SerializeField]
	private Button mAwakeCostSelectorBtn;

	[SerializeField]
	private ProfessionCommonMaterial mAwakeCommonMaterial;

	[SerializeField]
	private TreasureUpgradePanelStarInfo mStarInfo;

	[SerializeField]
	private List<ProfessionPlayerPropItem> mStarProps;

	[SerializeField]
	private List<ProfessionPlayerPropItem> mProps;

	[SerializeField]
	private Button mUpgradeBtn;

	[SerializeField]
	private Button mWakeupBtn;

	[SerializeField]
	private ProfessionPropEffectPlayAgent mEffectAgent;

	[SerializeField]
	private WorldStateSwitcher<TreasureUpgradeStatus> mButtonSwitcher;

	[SerializeField]
	private Toggle mAutoMergeToggle;

	[SerializeField]
	private Toggle mUseCommonMaterialToggle;

	[SerializeField]
	private WorldStateSwitcher<bool> mCanUseCommonMaterialStateSwitcher;

	private IProfessionItemData mItemData;

	private ITreasurePropProvider mPropProvider;

	private List<ItemIcon> mMaterials = new List<ItemIcon>();

	private Dictionary<int, long> mSelectedPieces = new Dictionary<int, long>();

	private Dictionary<PropType, long> mTmpCurStarProps = new Dictionary<PropType, long>();

	private Dictionary<PropType, long> mTmpNextStarProps = new Dictionary<PropType, long>();




	public GameObject Operator => ((Component)this).gameObject;

	protected void Awake()
	{






		((UnityEvent)mUpgradeBtn.onClick).AddListener((UnityAction)OnUpgradeBtnClick);
		((UnityEvent)mWakeupBtn.onClick).AddListener((UnityAction)OnAwakeBtnClick);
		((UnityEvent<bool>)(object)mAutoMergeToggle.onValueChanged).AddListener((UnityAction<bool>)OnAutoMergeToggleChange);
		((UnityEvent<bool>)(object)mUseCommonMaterialToggle.onValueChanged).AddListener((UnityAction<bool>)OnUseCommonMaterialToggleChange);
		((UnityEvent)mAwakeCostSelectorBtn.onClick).AddListener((UnityAction)OnAwakeMaterialBtnClick);
	}

	protected void OnDestroy()
	{






		((UnityEvent)mUpgradeBtn.onClick).RemoveListener((UnityAction)OnUpgradeBtnClick);
		((UnityEvent)mWakeupBtn.onClick).RemoveListener((UnityAction)OnAwakeBtnClick);
		((UnityEvent<bool>)(object)mAutoMergeToggle.onValueChanged).RemoveListener((UnityAction<bool>)OnAutoMergeToggleChange);
		((UnityEvent<bool>)(object)mUseCommonMaterialToggle.onValueChanged).RemoveListener((UnityAction<bool>)OnUseCommonMaterialToggleChange);
		((UnityEvent)mAwakeCostSelectorBtn.onClick).RemoveListener((UnityAction)OnAwakeMaterialBtnClick);
	}

	protected void OnEnable()
	{
		mAutoMergeToggle.isOn = Services.AwakeAutoMergeEnable;
		mUseCommonMaterialToggle.isOn = Services.UpgradeUseCommonMaterailEnable;
		Services.OnWindowClosing += new Action<GameObject>(Instance_OnWindowClosed);
	}

	protected void OnDisable()
	{
		Services.OnWindowClosing -= new Action<GameObject>(Instance_OnWindowClosed);
	}

	private void OnAutoMergeToggleChange(bool isOn) { Services.SetAwakeAutoMerge(isOn); }

	private void OnUseCommonMaterialToggleChange(bool isOn) { Services.SetUseCommonMaterial(isOn,()=>{if(mItemData!=null)TryRefreshMaterials();}); }

	private void Instance_OnWindowClosed(GameObject obj)
	{
		TreasureAwakeMaterialPanel component = obj.GetComponent<TreasureAwakeMaterialPanel>();
		if (!((Object)(object)component == (Object)null))
		{
			mSelectedPieces = component.SelectItems.Where((KeyValuePair<int, long> kvp) => kvp.Value > 0).ToDictionary((KeyValuePair<int, long> kvp) => kvp.Key, (KeyValuePair<int, long> kvp) => kvp.Value);
			TryRefreshMaterials();
		}
	}

	public void Show(IProfessionItemData itemData)
	{
		mItemData = itemData;
		((Component)this).gameObject.SetActive(true);
		foreach (ProfessionPlayerPropItem mStarProp in mStarProps)
		{
			((Component)mStarProp).gameObject.SetActive(false);
		}
		foreach (ProfessionPlayerPropItem mProp in mProps)
		{
			((Component)mProp).gameObject.SetActive(false);
		}
		mPropProvider = new TreasureDummyPropProvider(Services.Tables,Services.Player,mItemData.Id);
		Refresh();
	}

	private void Refresh()
	{
		mStarInfo.Refresh(mItemData);
		mSelectedPieces.Clear();
		TryRefreshMaterials();
		RefreshProps();
		RefreshButtons();
		RefreshToogle();
	}

	private TreasureUpgradeStatus GetStatus()
	{
		TreasureInfo treasureInfo = Services.Tables.GetTreasure(mItemData.Info.ClassId);
		int star = Services.Icons.GetState(mItemData).Star;
		if (star < treasureInfo.StarMax - 1)
		{
			return TreasureUpgradeStatus.Upgrade;
		}
		if (star == treasureInfo.StarMax - 1)
		{
			return TreasureUpgradeStatus.Wakeup;
		}
		return TreasureUpgradeStatus.Max;
	}

	private void RefreshButtons()
	{
		TreasureUpgradeStatus status = GetStatus();
		mButtonSwitcher.SetState(status);
	}

	private void RefreshToogle()
	{
		if (Services.LimitCommonMaterial(mItemData.Info.ClassId))
		{
			((Component)mAutoMergeToggle).gameObject.SetActive(false);
		}
	}

	private void TryRefreshMaterials()
	{
		foreach (ItemIcon mMaterial in mMaterials)
		{
			Services.Materials.RecycleIcon(mMaterial);
		}
		mMaterials.Clear();
		ITreasurePromoteServices mainPlayer = Services;
		bool state = !Services.LimitCommonMaterial(mItemData.Info.ClassId) && GetStatus() == TreasureUpgradeStatus.Upgrade;
		mCanUseCommonMaterialStateSwitcher.SetState(state);
		Services.GetUpgradeMaterials(mItemData.Id, GetStatus() == TreasureUpgradeStatus.Upgrade, Services.CanUseCommonMaterial(mItemData.Id), out var materials);
		if (materials == null)
		{
			return;
		}
		RefreshCommanMaterials(materials);
		foreach (KeyValuePair<int, long> item2 in materials)
		{
			if (item2.Key != Services.CommonPieceClassId || item2.Key == Services.MoneyId)
			{
				ItemIcon item = Services.Materials.GetIcon(item2.Key, mAwakeCostSlot).SetRequireCount(item2.Value).SetClickPopupInfoDialog()
					.SetCenterHintContent(show: true);
				mMaterials.Add(item);
			}
		}
	}

	private void RefreshCommanMaterials(IDictionary<int, long> materials)
	{
		bool flag = materials.ContainsKey(Services.CommonPieceClassId);
		((Component)mAwakeCommonMaterial).gameObject.SetActive(flag);
		if (flag)
		{
			long num = mSelectedPieces.Sum((KeyValuePair<int, long> kvp) => kvp.Value);
			long num2 = Services.GetCommonTreasurePiece(mItemData).Sum(((IProfessionItemData It, long Count) it) => it.Count);
			long starUpCommonPiece = Services.GetStarUpCommonPiece(mItemData.Info.ClassId, Services.Icons.GetState(mItemData).Star);
			mAwakeCommonMaterial.SetQuality(mItemData.GetQuality()).SetRedDot(num2 > starUpCommonPiece && num < starUpCommonPiece).RefreshCount(num, starUpCommonPiece)
				.Show();
		}
	}

	private void OnAwakeMaterialBtnClick() { Services.PopupAwakeMaterials(panel=>{panel.ItemData=mItemData;panel.SelectItems=mSelectedPieces;}); }

	private void RefreshProps()
	{
		TreasureInfo treasureInfo = Services.Tables.GetTreasure(mItemData.Info.ClassId);
		TreasureState itemParamTreasure = Services.Icons.GetState(mItemData);
		int star = itemParamTreasure.Star;
		bool flag = star >= treasureInfo.StarMax;
		Services.IsTreasureMaxLevel(treasureInfo.ClassId);
		int treasureFinalLevel = Services.GetTreasureFinalLevel(treasureInfo.ClassId);
		int hitCount = 1;
		mTmpCurStarProps.Clear();
		mTmpNextStarProps.Clear();
		int treasureBaseLevel = Services.GetTreasureBaseLevel(treasureInfo.ClassId);
		TreasureCalcPropData treasureCalcPropData = TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(Services.Tables,treasureFinalLevel, treasureBaseLevel);
		treasureCalcPropData.AssignBlessLevelPropDict(Services.SubRank);
		Services.Properties.GetStarProps(treasureInfo,Services.Player, star, treasureCalcPropData, hitCount, mTmpCurStarProps);
		if (!flag)
		{
			Services.Properties.GetStarProps(treasureInfo,Services.Player, star + 1, treasureCalcPropData, hitCount, mTmpNextStarProps);
		}
		int hitCount2 = Services.GetHitCount(treasureInfo,treasureFinalLevel);
		int num = itemParamTreasure?.HitCount ?? 1;
		(PropType, long) effectPropInfo = Services.Properties.GetEffectPropInfo(treasureInfo,Services.Player, star, treasureCalcPropData, hitCount2);
		treasureInfo.Effect.GetLevelMaxHitCount(treasureFinalLevel);
		(PropType, long) tuple = (flag ? (0, 0L) : Services.Properties.GetEffectPropInfo(treasureInfo,Services.Player, star + 1, treasureCalcPropData, hitCount2));
		bool flag2 = treasureInfo.Effect is TreasureEffectTask;
		int num2 = (flag2 ? 1 : 0);
		foreach (KeyValuePair<PropType, long> mTmpCurStarProp in mTmpCurStarProps)
		{
			long value = 0L;
			Dictionary<PropType, long> dictionary = mTmpNextStarProps;
			if (dictionary == null || !dictionary.TryGetValue(mTmpCurStarProp.Key, out value))
			{
				value = -1L;
			}
			if (treasureInfo.EffectPropTypes.Contains(mTmpCurStarProp.Key) & flag2)
			{
				string text = LanguageManager.Instance.GetText(TreasureViewLanguage.GetPropertyNameKey(mTmpCurStarProp.Key));
				mStarProps[0].Init((ProfessionPropertyType)mTmpCurStarProp.Key, string.Format(LanguageManager.Instance.GetText("treasure_common_total_promote"), text), effectPropInfo.Item2, (tuple.Item1 == 0) ? (-1) : tuple.Item2);
				((Component)mStarProps[0]).gameObject.SetActive(true);
				mProps[0].Init((ProfessionPropertyType)mTmpCurStarProp.Key, string.Format(LanguageManager.Instance.GetText("treasure_common_single_promote"), text), mTmpCurStarProp.Value, value);
				((Component)mProps[0]).gameObject.SetActive(true);
				mProps[1].Init("treasure_common_hitcount_limit", $"{num}/{treasureInfo.Effect.GetLevelMaxHitCount(treasureFinalLevel)}");
				((Component)mProps[1]).gameObject.SetActive(true);
			}
			else
			{
				((Component)mStarProps[num2]).gameObject.SetActive(true);
				mStarProps[num2].Init((ProfessionPropertyType)mTmpCurStarProp.Key, mTmpCurStarProp.Value, value);
				num2++;
			}
		}
	}

	private void OnUpgradeBtnClick()
    {
        Services.GetUpgradeMaterials(mItemData.Id,true,Services.CanUseCommonMaterial(mItemData.Id),out var materials);
        int code=Services.CanUpgrade(mItemData.Id,materials,true);
        if(code!=0){Services.ShowError(code);return;}
        Services.Upgrade(mItemData.Id,Services.CanUseCommonMaterial(mItemData.Id),true,()=>{mEffectAgent.StartPlayEffect();Refresh();});
    }

	private void OnAwakeBtnClick()
    {
        Services.GetUpgradeMaterials(mItemData.Id,true,true,out var materials);
        int code=Services.CanUpgrade(mItemData.Id,materials,Services.AwakeAutoMergeEnable);
        if(code!=0){Services.ShowError(code);return;}
        Services.Upgrade(mItemData.Id,true,Services.AwakeAutoMergeEnable,()=>{mEffectAgent.StartPlayEffect();Refresh();});
    }
}

}
