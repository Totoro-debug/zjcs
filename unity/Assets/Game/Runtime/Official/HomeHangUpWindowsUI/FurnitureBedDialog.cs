// Selected original client methods; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using FastHangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowFastHangUpInfo;
using Player = Zjcs.Official.HomeHangUpWindowsUI.IHomeHangUpWindowPlayer;
namespace Zjcs.Official.HomeHangUpWindowsUI
{

public class FurnitureBedDialog : UIBase
{
    public IHomeHangUpWindowServices Services { protected get; set; }
	[SerializeField]
	private MultiLanguageText mSubNameText;

	public MultiLanguageText FastHangUpBtnTxt;

	public MultiLanguageText Name;

	public MultiLanguageText Desc;

	public Transform ImageSlot;

	[Header("挂机相关")]
	public MultiLanguageText HangUpOutputTxt;

	public Button GetHangUpRewardBtn;

	public Button FastHangUpBtn;

	public Image HangUpRewardBtnImg;

	public FurnitureOutputInfo OutputInfo;

	public Image HangUpProgress;

	public OfficialWwiseEvent FastHangUpSoundEvent;

	[Header("折叠")]
	public WorldStateSwitcher<bool> mLevelFoldoutStateSwitcher;

	public WorldStateSwitcher<bool> mOutputFoldoutStateSwitcher;

	public Button LevelFoldoutBtn;

	public Button OutputFoldoutBtn;

	public bool LevelFoldout;

	public bool OutputFoldout;

	[SerializeField]
	private PlayerPropItem mBlessLevelPropItem;

	[SerializeField]
	private PlayerPropItem mBaseOutputPropItem;

	[SerializeField]
	private PlayerPropItem mGodessNumPropItem;

	[SerializeField]
	private PlayerPropItem mGodessAddPropItem;

	[SerializeField]
	private FurnitureHangUpQuickReceive mFurnitureHangUpQuickReceive;

	[SerializeField]
	private WorldStateSwitcher<int> mTipLevelDotSwitcher;

	protected EFurniture mFurniture;

	protected HangUpInfo mHangUpInfo;

	protected FastHangUpInfo mFastHangUpInfo;

	private Coroutine mHangUpTimerCoroutine;

	private readonly List<int> mHangUpIdList = new List<int>(1);

	private bool mIsFree;

	private Dictionary<PropType, long> mBranchExplorePropValue = new Dictionary<PropType, long>();

	private HomeWindowResourceBag mDisposableBag = new HomeWindowResourceBag();

	private IDisposable mTipLevelDisposable;

	protected int HangUpId => mHangUpInfo.Id;

	public event Action OnSlept;

	public void Init(EFurniture furniture)
	{
		InitWrapper(null);
		mFurnitureHangUpQuickReceive.Init(furniture);
		mFurniture = furniture;
		Services.TryGetMainHangUpInfo(furniture, out mHangUpInfo);
		mFastHangUpInfo = Services.GetFastHangUpInfo(mHangUpInfo.FastHangUpId);
		HomeWindowFurnitureInfo furnitureInfo = Services.GetFurnitureInfo(furniture);
		mHangUpIdList.Clear();
		mHangUpIdList.Add(HangUpId);
		int classId = furnitureInfo.ClassId;
		FastHangUpBtn.gameObject.SetActive(mFastHangUpInfo.FastHangUpItemDic != null);
		Name.SetKey(Services.GetItemNameKey(classId));
		if (Desc != null)
		{
			Desc.SetKey(Services.GetItemDescKey(classId));
		}
		string furnitureTypeNameKey = Services.GetFurnitureTypeNameKey(furniture);
		string text = Services.Text(furnitureTypeNameKey);
		mSubNameText.SetKey(text);
		foreach (Transform child in ImageSlot) Destroy(child.gameObject);
		Instantiate(mDisposableBag.Add(Services.Resources.LoadAsset<GameObject>(Services.GetFurnitureIconPrefabPath(mFurniture))).Result, ImageSlot).GetComponent<FurnitureIconWidget>().SetState(isRequired: true);
	}

	private void Awake()
	{
		FastHangUpBtn.onClick.AddListener(OnFastHangUpBtnClick);
		LevelFoldoutBtn.onClick.AddListener(OnLevelFoldoutBtn);
		OutputFoldoutBtn.onClick.AddListener(OnFoldoutFoldoutBtn);
		if (GetHangUpRewardBtn != null)
		{
			GetHangUpRewardBtn.onClick.AddListener(OnGetHangUpReward);
		}
		mLevelFoldoutStateSwitcher.SetState(LevelFoldout);
		mOutputFoldoutStateSwitcher.SetState(OutputFoldout);
		mDisposableBag ??= new HomeWindowResourceBag();
	}

	protected override void OnDestroy()
	{
		mDisposableBag?.Dispose();
		base.OnDestroy();
	}

	private void OnLevelFoldoutBtn()
	{
		LevelFoldout = !LevelFoldout;
		mLevelFoldoutStateSwitcher.SetState(LevelFoldout);
	}

	private void OnFoldoutFoldoutBtn()
	{
		OutputFoldout = !OutputFoldout;
		mOutputFoldoutStateSwitcher.SetState(OutputFoldout);
	}

	protected override void OnEnableAndInited()
	{
		Services.Player.OnFastHangUp += OnFastHangUp;
		Services.Player.OnAddOutputAdditionData += new Action<object>(OnAddOutputAdditionData);
		mTipLevelDisposable = Services.SubscribeTips(OnTipLevelChange);
		Refresh();
		UpdateTipLevelDotSwitcher(Services.GetTipLevel(mFurniture));
		base.OnEnableAndInited();
	}

	protected override void OnDisableAndInited()
	{
		if (Services.Player != null)
		{
			Services.Player.OnFastHangUp -= OnFastHangUp;
			Services.Player.OnAddOutputAdditionData -= new Action<object>(OnAddOutputAdditionData);
		}
		if (mHangUpTimerCoroutine != null)
		{
			StopCoroutine(mHangUpTimerCoroutine);
		}
		mTipLevelDisposable?.Dispose();
		mTipLevelDisposable = null;
		base.OnDisableAndInited();
	}

	private void OnFastHangUp(int hangUpId)
	{
		if (hangUpId == HangUpId)
		{
			FastHangUpSoundEvent?.Post(gameObject);
			RefreshHangUpTimer();
		}
	}

	private void OnAddOutputAdditionData(object dataWrap)
	{
		RefreshHangUpTimer();
	}

	private void OnGetHangUpReward()
	{
		_ = Services.Player;
		if (!Services.CanGetHangUpReward(HangUpId))
		{
			Services.SystemMessage(Services.Text("fast_hang_up_no_award"));
			return;
		}
		Services.Collect(mHangUpIdList, (IHomeWindowAwardMessage rep) =>
		{
			if (rep.DataList != null && rep.DataList.Count > 0)
			{
				Services.Player.Award(rep.DataList, rep.SourceData);
				Services.MergeAward(rep.DataList);
				Services.PopupAwardList(rep.DataList);
			}
			Services.PublishCollected();
			RefreshHangUpTimer();
		});
	}

	private void OnFastHangUpBtnClick()
	{
		if (Services.Player.IsFunctionLimit(Services.HomeBedSpeedConfirmFunctionId))
		{
			FastHnagUp();
		}
		else if (IsPlayerReachRankMaxLevelAndCanRankUp())
		{
			Services.Confirm("home_bed_confirm1", FastHnagUp);
		}
		else if (!IsPlayerActiveCurRankFirstMainRegion())
		{
			Services.Confirm("home_bed_confirm2", FastHnagUp);
		}
		else
		{
			FastHnagUp();
		}
	}

	private void FastHnagUp()
	{
		if (mIsFree)
		{
			Player mainPlayer = Services.Player;
			bool flag = true;
			long num = 0L;
			foreach (int value in mFastHangUpInfo.FastHangUpItemDic.Values)
			{
				num += value;
			}
			foreach (int hangUpId in mFastHangUpInfo.HangUpIdList)
			{
				if (!mainPlayer.TryGetFastHangUpTip(hangUpId, num, out var exceedItemId, out var titleKey, out var key) || (exceedItemId == Services.PlayerExpItemId && !mainPlayer.CanGetExp()))
				{
					continue;
				}
				flag = false;
				Services.Confirm(key, titleKey, FreeFastHangUp);
				break;
			}
			if (flag)
			{
				FreeFastHangUp();
			}
		}
		else
		{
			Services.OpenFastHangUp(mFastHangUpInfo.FastHangUpId, gameObject);
		}
	}

	private bool IsPlayerReachRankMaxLevelAndCanRankUp()
	{
		int highSubRank = Services.GetRankHighSubRank(Services.Player.SubRank);
		if (Services.IsRankLimited(highSubRank + 1))
		{
			return false;
		}
		Services.GetMaxPlayerLevel(highSubRank, out var baseLevel, out var _);
		if (Services.Player.Level < baseLevel)
		{
			return false;
		}
		return true;
	}

	private bool IsPlayerActiveCurRankFirstMainRegion()
	{
		if (!Services.TryGetFirstMainMapRegion(Services.Player.SubRank, out var value))
		{
			return true;
		}
		return Services.Player.IsMapRegionPublic(value.ClassId, value.Region);
	}

	private void OnTipLevelChange(HangUpTipLevelChangeData data)
	{
		if (data.FurnitureType == mFurniture)
		{
			UpdateTipLevelDotSwitcher(data.TipLevel);
		}
	}

	private void UpdateTipLevelDotSwitcher(int tipLevel)
	{
		mTipLevelDotSwitcher.SetState(tipLevel);
	}

	private void FreeFastHangUp()
	{
		Services.Close(gameObject);
		Services.OperFastHangUp(mFastHangUpInfo.FastHangUpId, mFastHangUpInfo.AutoReceive, null, OnSlept);
	}

	private void Refresh()
	{
		int hangUpLevel = Services.Player.GetHangUpLevel(HangUpId);
		if (OutputInfo != null)
		{
			OutputInfo.ShowInfo(HangUpId, hangUpLevel);
			Player mainPlayer = Services.Player;
			mBlessLevelPropItem.Init("ui_common_world_level", mainPlayer.GetBlessingLevel(), -1L);
			mGodessNumPropItem.Init("hangup_output_add_loss_goddess_nums", mainPlayer.GetLostGoddessActiveCount(), -1L);
			double hangUpReward = Services.GetHangUpReward(HangUpId, 1.0, hangUpLevel, isBase: true);
			mBranchExplorePropValue.Clear();
			Services.Player.PlayerPropCenter.GetProps(OutputInfo.BranchExploreEPropHandleTypes, mBranchExplorePropValue);
			double prop = Services.GetProp(mBranchExplorePropValue, PropType.HangUpExpAddPercent);
			double value = FurnitureOutputInfo.CalcSumSpeed(hangUpReward, 0.0, prop);
			mGodessAddPropItem.Init("hangup_output_add_loss_goddess", FurnitureOutputInfo.GetPropValue(value, Services.FormatCount));
			mBaseOutputPropItem.Init("ui_common_world_level", FurnitureOutputInfo.GetPropValue(hangUpReward, Services.FormatCount));
		}
		RefreshHangUp();
	}

	protected virtual void RefreshHangUp()
	{
		HomeWindowItemInfo hangUpRewardItem = Services.GetHangUpRewardItem(HangUpId);
		HangUpRewardBtnImg.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>(hangUpRewardItem.Icon)).Result;
		mIsFree = Services.Player.CanFreeFastHangUp(mFastHangUpInfo.FastHangUpId);
		FastHangUpBtnTxt.SetKey(mIsFree ? "item_desc_bed" : "ui_sleep_text");
		RefreshHangUpTimer();
	}

	private void RefreshHangUpTimer()
	{
		if (mHangUpTimerCoroutine != null)
		{
			StopCoroutine(mHangUpTimerCoroutine);
		}
		if (isActiveAndEnabled)
		{
			mHangUpTimerCoroutine = StartCoroutine(HangUpTimer());
		}
	}

	private IEnumerator HangUpTimer()
	{
		Player mainPlayer = Services.Player;
		int hangUpId = HangUpId;
		long rewardPoolCount = Services.GetHangUpAwardCount(hangUpId, out var hangUpSeconds);
		long maxCount = Services.GetHangUpMaxRewardStore(hangUpId);
		maxCount += Services.CalcOutputAdditionExtStore(hangUpId, maxCount);
		double hourOutput = Services.GetHangUpReward(hangUpId, 1.0);
		rewardPoolCount += Services.CalcOutputAdditionUnsettleExtRewardCount(hangUpId);
		long sumCount = rewardPoolCount;
		SetOutputCountTxt(sumCount, maxCount);
		while (true)
		{
			double num = hourOutput * (hangUpSeconds / 3600.0);
			num += (double)Services.CalcOutputAdditionExtCount(hangUpId, num);
			long num2 = rewardPoolCount + (long)num;
			if (num2 > sumCount)
			{
				sumCount = num2;
				SetOutputCountTxt(sumCount, maxCount);
			}
			if (HangUpProgress != null)
			{
				HangUpProgress.fillAmount = 1f * (float)sumCount / (float)maxCount;
			}
			if (sumCount < maxCount)
			{
				yield return null;
				hangUpSeconds += (double)Time.deltaTime;
				continue;
			}
			break;
		}
	}

	private void SetOutputCountTxt(long sumCount, long maxCount)
	{
		if (!(HangUpOutputTxt == null))
		{
			HangUpOutputTxt.SetKey("{0}/{1}", CultureNumberArg.Compact(sumCount), CultureNumberArg.Compact(maxCount));
		}
	}
}

}
