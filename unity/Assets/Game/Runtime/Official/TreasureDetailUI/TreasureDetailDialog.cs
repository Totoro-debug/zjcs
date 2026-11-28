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
public class TreasureDetailDialog : UIBase, IItemDetailDialog
{
    private ITreasureDetailServices Services;
    [SerializeField] private TreasureSuitContent mSuit;
    public void Configure(ITreasureDetailServices services)
    {
        Services=services??throw new ArgumentNullException(nameof(services));
        mTreasureProps.Configure(services);
        mSuit.Configure(services);
    }

	private enum OperationBtnStatus
	{
		None,
		Activate,
		Train,
		TrainFinished
	}

	[SerializeField]
	private TopBarHandler mTopBar;

	[SerializeField]
	private ItemDetailDialog mItemDetailDialog;

	[Header("物品数量")]
	[SerializeField]
	private GameObject mPieceItemCountGo;

	[SerializeField]
	private WorldMultiLanguageText mPieceItemCountText;

	[Header("星级&等级")]
	public TreasureStarInfo CurrentStarInfo;

	[SerializeField]
	private TreasureProps mTreasureProps;

	[Header("套装")]
	[Header("操作")]
	[SerializeField]
	private PlayerCostMoneyContent mActivateContent;

	[SerializeField]
	private Button mTrainOpBtn;

	[SerializeField]
	private Button mReplaceBtn;

	[SerializeField]
	private List<TreasureRedDotItem> mRedDots;

	[SerializeField]
	private WorldStateSwitcher<OperationBtnStatus> mButtonStateSwither;

	[SerializeField]
	private WorldMultiLanguageText mLevelText;

	[SerializeField]
	private BlessLevelTextViewChanger mLevelTextView;

	[SerializeField]
	private IntroductionPopBtn mBlessLevelRuleBtn;

	public ShareItemBtn ShareItemBtn;

	protected TreasureInfo TreasureInfo;

	protected TreasureState ItemParam => mSnapshotTreasure!=null?mSnapshotTreasure.TreasureParam:Services.GetState(mItemData);

	protected int TreasureClassId;

	protected int TreasurePieceClassId;

	private SnapshotTreasure mSnapshotTreasure;

	protected ITreasurePropProvider mTreasurePropProvider;

	private Dictionary<PlayerItemType, int> mTopBarItemDict = new Dictionary<PlayerItemType, int>();

	private bool mCanOperation;

	private bool mCanShowReplace;

	private IProfessionItemData mItemData;

	private ITreasureReplaceEventLisener ReplaceListener => Services.Icons.ReplaceListener;

	public event Action OnTreasureSuperStar;

	protected void Awake()
	{
		((UnityEvent)mActivateContent.Btn.onClick).AddListener((UnityAction)OnActivateBtnClick);
		((UnityEvent)mTrainOpBtn.onClick).AddListener((UnityAction)OnTrainBtnClick);
		((UnityEvent)mReplaceBtn.onClick).AddListener((UnityAction)OnReplaceBtnClick);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.Icons.OnTreasureRefresh += MainPlayer_OnTreasureRefresh;
		Services.Icons.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
	}

	protected override void OnDisableAndInited()
	{
		if (Services.Icons != null)
		{
			Services.Icons.OnTreasureRefresh -= MainPlayer_OnTreasureRefresh;
			Services.Icons.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.OnDisableAndInited();
	}

	private void OnReplaceBtnClick()
	{
		Services.Close(((Component)this).gameObject);
		int treasureInSlotIndex = Services.GetTreasureInSlotIndex(TreasureInfo.ClassId);
		TreasureReplaceInfo treasureReplaceInfo = new TreasureReplaceInfo();
		treasureReplaceInfo.TreasureClassId = TreasureInfo.ClassId;
		treasureReplaceInfo.ElementType = Services.GetElement(TreasureInfo.ClassId);
		treasureReplaceInfo.OriginIndex = treasureInSlotIndex;
		ReplaceListener.FireOnTreasureStartReplace(treasureReplaceInfo);
	}

	private void MainPlayer_OnTreasureRefresh(IProfessionItemData itemData, bool isNew)
	{
		if (mItemData?.Info.ClassId == itemData.Info.ClassId)
		{
			if (ItemParam.Star >= TreasureInfo.StarMax)
			{
				OnTreasureSuperStar?.Invoke();
			}
			mTreasureProps.RefreshProps();
			RefreshStar();
			RefreshOperationBtn();
		}
		RefreshSuitInfo();
		mItemDetailDialog.UpdateItemNameText();
	}

	private void MainPlayer_OnItemCountUpdate(IProfessionItemData itemData, long preCount, object sourceData)
	{
		if (itemData.Info.ClassId == TreasurePieceClassId)
		{
			RefreshPieceCount();
		}
	}

	private void RefreshPieceCount()
	{
		long itemAmount = Services.GetItemAmount(TreasurePieceClassId);
		mPieceItemCountText.SetKey("treasuredetail_piece_count", CultureNumberArg.Compact(itemAmount));
		mPieceItemCountGo.gameObject.SetActive(true);
	}

	public void SetCanOperation(bool canOperation)
	{
		mCanOperation = canOperation;
			mSuit.SetCanOperation(canOperation);
		RefreshOperationBtn();
	}

	public void SetCanShowReplace(bool canShow)
	{
		mCanShowReplace = canShow;
		RefreshOperationBtn();
	}

	public void SetCanShowNpcTreasureAndState(bool canShow, bool canShowState)
	{
		mTreasureProps.SetCanShowNpcTreasureAndState(canShow, canShowState);
		mTreasureProps.RefreshProps();
	}

	public void SetForceInSlot()
	{
		mTreasureProps.SetForceInSlot();
	}

	public void Init(int itemId, long requireCount = 0L)
	{
		InitWrapper(() =>
		{
			mCanOperation = false;
			mCanShowReplace = false;
			TreasureClassId = itemId;
			mItemData = Services.GetItemById(itemId);
			mTreasurePropProvider = new TreasureDummyPropProvider(Services.Tables,Services.Player,TreasureClassId);
			mTreasureProps.SetTreasure(TreasureClassId);
			TreasurePieceClassId = Services.GetPieceIdByTreasureId(itemId);
			TreasureInfo = Services.GetTreasure(itemId);
			CurrentStarInfo.Hide();
			mItemDetailDialog.SetNameTextModifier(UpdateItemNameText);
			mTopBarItemDict.Clear();
			mTopBar.UpdateCurrencyData(mTopBarItemDict);
			if (mItemData == null)
			{
				mItemDetailDialog.Init(itemId, requireCount);
			}
			else
			{
				mItemDetailDialog.Init(mItemData, requireCount);
			}
			Refresh();
			RefreshPieceCount();
			if (ItemParam != null && ItemParam.Active)
			{
				ShareItemBtn.Init(mItemData);
			}
		});
	}

	public void Init(IProfessionItemData itemData, long requireCount = 0L)
	{
		Init(itemData.Id, requireCount);
		mRedDots.ForEach((TreasureRedDotItem it) =>
		{
			it.Init(mItemData.Id);
		});
	}

	public void Init(SnapshotTreasure snapshot, bool canOperation = false)
	{
		InitWrapper(() =>
		{
			mSnapshotTreasure = snapshot;
			mCanOperation = canOperation;
			mSuit.SetCanOperation(canOperation);
			TreasureClassId = mSnapshotTreasure.ItemId;
			TreasureInfo = Services.GetTreasure(TreasureClassId);
			TreasurePieceClassId = TreasureInfo.TreasurePieceId;
			mTreasurePropProvider = new TreasureSnapshotPropProvider(Services.Tables,mSnapshotTreasure);
			mItemDetailDialog.SetNameTextModifier(UpdateItemNameText);
			mItemDetailDialog.Init(mSnapshotTreasure.ItemId, 0L);
			mTreasureProps.SetTreasure(TreasureClassId, mTreasurePropProvider);
			TreasureSuitInfo treasureSuitInfo = Services.GetSuitByTreasure(TreasureClassId);
			if (mSnapshotTreasure.ActivedInSuit != null && treasureSuitInfo != null)
			{
				List<int> list = new List<int>();
				foreach (KeyValuePair<int, int> item in mSnapshotTreasure.ActivedInSuit)
				{
					if (!treasureSuitInfo.Treasures.Contains(item.Key))
					{
						list.Add(item.Key);
					}
				}
				foreach (int item2 in list)
				{
					mSnapshotTreasure.ActivedInSuit.Remove(item2);
				}
			}
			mPieceItemCountGo.SetActive(false);
			SetSuitInfo(treasureSuitInfo, mSnapshotTreasure.ActivedInSuit);
			RefreshStar();
			RefreshOperationBtn();
			ShareItemBtn.Init(null);
		});
	}

	private void Refresh()
	{
		mTreasureProps.RefreshProps();
		RefreshSuitInfo();
		RefreshStar();
		RefreshOperationBtn();
	}

	public void UpdateItemNameText(WorldMultiLanguageText nameText)
	{
		if (!((Object)(object)nameText == (Object)null))
		{
			nameText.SetKey("{0}", new CultureTextArg(TreasureSuitLanguage.GetItemNameKey(TreasureClassId)));
			UpdateItemLevel();
		}
	}

	private void UpdateItemLevel()
	{
		int num = mTreasurePropProvider.Level();
		((Component)mLevelText).gameObject.SetActive(num > 1);
		((Component)mBlessLevelRuleBtn).gameObject.SetActive(false);
		if (num <= 1)
		{
			return;
		}
		mLevelText.SetKey($"+{num}");
		mTreasurePropProvider.BaseLevel();
		int num2 = mTreasurePropProvider.BlessLevel();
		bool flag = num2 > 0;
		if (flag)
		{
			string textFormated = Services.TextFormatted("ui_blessing_treasure_level_tip_0", num - num2, num2);
			List<string> list = new List<string> { textFormated };
			foreach (string item in new List<string>
			{
				"ui_blessing_treasure_level_tip_1",
				Services.GetSeasonLevelUnEffectDesc(Services.PlanesRank),
				"ui_blessing_treasure_level_tip_3"
			})
			{
				list.Add(Services.Text(item));
			}
			((Component)mBlessLevelRuleBtn).gameObject.SetActive(true);
			HomeInfoTextKey titleTextArgs = new HomeInfoTextKey("ui_blessing_level");
			mBlessLevelRuleBtn.Init(titleTextArgs, list);
		}
		mLevelTextView.Refresh(flag, needChangeOutline: false);
	}

	protected virtual void RefreshStar()
	{
		int num = mTreasurePropProvider.Star();
		int starMax = TreasureInfo.StarMax;
		bool isSuper = num >= starMax;
		Quality quality = ((mItemData == null) ? Services.GetQuality(TreasureInfo.ClassId) : mItemData.GetQuality());
		CurrentStarInfo.Show(quality, num, starMax - 1, isSuper, showBlankStar: true);
	}

	private void SetSuitInfo(TreasureSuitInfo info,Dictionary<int,int> stars)=>mSuit.Show(TreasureClassId,mCanOperation,info,stars);

	private void RefreshSuitInfo()
	{
		TreasureSuitInfo treasureSuitInfo = Services.GetSuitByTreasure(TreasureClassId);
		bool flag = treasureSuitInfo != null;
		Dictionary<int, int> dictionary = null;
		if (flag)
		{
			dictionary = new Dictionary<int, int>();
			TreasureSuitQueries.GetSuitStartInfo(treasureSuitInfo,Services.Player,dictionary);
		}
		SetSuitInfo(treasureSuitInfo, dictionary);
	}









	private OperationBtnStatus GetOperationBtnStatus()
	{
		if (mItemData == null || !mCanOperation)
		{
			return OperationBtnStatus.None;
		}
		if (!ItemParam.Active)
		{
			return OperationBtnStatus.Activate;
		}
		if (ItemParam.Star >= TreasureInfo.StarMax && Services.IsTreasureMaxLevel(TreasureInfo.ClassId))
		{
			return OperationBtnStatus.TrainFinished;
		}
		return OperationBtnStatus.Train;
	}

	private void RefreshOperationBtn()
	{
		OperationBtnStatus operationBtnStatus = GetOperationBtnStatus();
		mButtonStateSwither.SetState(operationBtnStatus);
		((Component)mReplaceBtn).gameObject.SetActive(mCanShowReplace && operationBtnStatus == OperationBtnStatus.Train);
		if (operationBtnStatus != OperationBtnStatus.Activate)
		{
			return;
		}
		IReadOnlyDictionary<int,long> materials = Services.ActivationMaterials(mItemData.GetQuality(),ItemParam.Star);
		if (materials.Count == 0)
		{
			mActivateContent.Init(0, 0L);
		}
		foreach (KeyValuePair<int, long> item in materials)
		{
			mActivateContent.Init(item.Key, item.Value);
			mTopBarItemDict.TryAdd(PlayerItemType.Money, item.Key);
		}
		mTopBar.UpdateCurrencyData(mTopBarItemDict);
	}

	private void OnActivateBtnClick()
	{
		if (mItemData != null)
		{
			int num = Services.CanActivateTreasure(TreasureClassId);
			if (num != 0)
			{
				Services.ShowError(num);
				return;
			}
			if(Services.Activate(mItemData))Services.Close(((Component)this).gameObject);
		}
	}

	private void OnTrainBtnClick()
    {
        Services.PopupPromote(this,mItemData,()=>Services.Close(gameObject));
    }
}

}
