// Selected official controller; see partner-gift-controller-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerGiftNpcInfo;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerGiftNpcData;
using PlayerItemDataWrap = Zjcs.Official.ProfessionUI.IProfessionItemData;
using NpcFriendshipLevelInfo = Zjcs.Official.PartnersUI.PartnerGiftLevelInfo;
using NpcFriendshipLevelInfoParser = Zjcs.Official.PartnersUI.IPartnerGiftLevels;
using NpcGiftInfoParser = Zjcs.Official.PartnersUI.IPartnerGiftExperience;
using NpcFriendshipTagInfo = Zjcs.Official.PartnersUI.PartnerGiftTagInfo;
using NpcGiftInfo = Zjcs.Official.PartnersUI.PartnerGiftInfo;
using Player = Zjcs.Official.PartnersUI.IPartnerGiftPlayer;
using NpcMeetType = System.Int32;
using AwardSourceData = System.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NpcDetailPanelGiftContent : UIBase, LoopScrollDataSource
{
	public enum GiftState
	{
		Normal,
		MaxLevel,
		SubRankLimit
	}

	[SerializeField]
	private LoopVerticalScrollRect mScrollRect;

	[SerializeField]
	private Button mResetBtn;

	[SerializeField]
	private Button mAutoSelectBtn;

	[SerializeField]
	private Button mGiveGiftBtn;

	[SerializeField]
	private Button mCustomFriendshipTagBtn;

	[SerializeField]
	private MultiLanguageText mFriendshipTagText;

	[SerializeField]
	private Image mFriendshipTagBg;

	[SerializeField]
	private Transform mSlot;

	[SerializeField]
	private GameObject mEmptyTipGo;

	[SerializeField]
	private GameObject mPropItemGroupPrefab;

	[SerializeField]
	private NpcGiftBlueDotItem mNpcGiftBlueDotItem;

	[SerializeField]
	private WorldStateSwitcher<GiftState> mStateSwitcher;

	private NpcInfo mNpcInfo;

	private PlayerNpcDataWrap mNpcData;

	private List<PlayerItemDataWrap> mNpcGiftShowList = new List<PlayerItemDataWrap>();

	private List<PlayerItemDataWrap> mNpcGiftSelectList = new List<PlayerItemDataWrap>();

	private Action<ItemIcon> mAddBtnClickAction;

	private Action<ItemIcon> mReduceBtnClickAction;

	private Dictionary<int, long> mSelectedGiftDict = new Dictionary<int, long>();

	private Dictionary<int, ItemIcon> mGiftItemIconDict = new Dictionary<int, ItemIcon>();

	private Action mResetExpPreviewAction;

	private Action mGiveGiftAction;

	private Action<Dictionary<int, long>> mShowExpPreviewAction;

	private PartnerGiftItemCompare mCompare;

	private GameObjectPool<NpcFriendshipPropItemGroup> mPool;

	private List<NpcFriendshipPropItemGroup> mPropItems = new List<NpcFriendshipPropItemGroup>();


	private PartnerGiftResourceBag mDisposableBag;

    private IPartnerGiftUiServices services;
    public void Configure(IPartnerGiftUiServices value)
    {
        services = value ?? throw new ArgumentNullException(nameof(value));
        mNpcGiftBlueDotItem.Configure(value);
    }

	private void Awake()
	{
		mAddBtnClickAction = OnClickAddBtn;
		mReduceBtnClickAction = OnClickReduceBtn;
		mPool = new GameObjectPool<NpcFriendshipPropItemGroup>(((Component)this).transform, mPropItemGroupPrefab);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag = new PartnerGiftResourceBag();
		((UnityEvent)mResetBtn.onClick).AddListener((UnityAction)OnClickResetBtn);
		((UnityEvent)mAutoSelectBtn.onClick).AddListener((UnityAction)OnClickAutoSelectBtn);
		((UnityEvent)mGiveGiftBtn.onClick).AddListener((UnityAction)OnClickGiveGiftBtn);
		((UnityEvent)mCustomFriendshipTagBtn.onClick).AddListener((UnityAction)OnCustomFriendShipTag);
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		((UnityEvent)mResetBtn.onClick).RemoveListener((UnityAction)OnClickResetBtn);
		((UnityEvent)mAutoSelectBtn.onClick).RemoveListener((UnityAction)OnClickAutoSelectBtn);
		((UnityEvent)mGiveGiftBtn.onClick).RemoveListener((UnityAction)OnClickGiveGiftBtn);
		((UnityEvent)mCustomFriendshipTagBtn.onClick).RemoveListener((UnityAction)OnCustomFriendShipTag);
		mPool.ReturnMany(mPropItems);
		mDisposableBag.Dispose();
		base.OnDisableAndInited();
	}

	public void Init(PlayerNpcDataWrap npcDataWrap, NpcInfo npcInfo, Action giveGiftAction, Action resetAction, Action<Dictionary<int, long>> showAction)
	{
		InitWrapper(() =>
		{
			mNpcData = npcDataWrap;
			mNpcInfo = npcInfo;
			mGiveGiftAction = giveGiftAction;
			mResetExpPreviewAction = resetAction;
			mShowExpPreviewAction = showAction;
			mCompare = new PartnerGiftItemCompare(npcInfo, services);
			mNpcGiftBlueDotItem.AddNpcInfo(mNpcInfo);
		});
	}

	private void Refresh()
	{
		mGiftItemIconDict.Clear();
		mSelectedGiftDict.Clear();
		mResetExpPreviewAction?.Invoke();
		_ = services.Gifts;
		IEnumerable<PlayerItemDataWrap> allCanSendToNpcGift = services.Player.GetAllCanSendToNpcGift(mNpcInfo.Id);
		List<PlayerItemDataWrap> list = new List<PlayerItemDataWrap>();
		foreach (PlayerItemDataWrap item in allCanSendToNpcGift)
		{
			if (!mNpcInfo.IsStarGodNpc || mNpcInfo.GiftPreferenceDict.ContainsKey(item.Info.ClassId))
			{
				list.Add(item);
			}
		}
		mNpcGiftShowList.Clear();
		mNpcGiftShowList.AddRange(list);
		mNpcGiftShowList.Sort(mCompare.CompareShowGift);
		mNpcGiftSelectList.Clear();
		mNpcGiftSelectList.AddRange(list);
		mNpcGiftSelectList.Sort(mCompare.CompareSelectGift);
		mEmptyTipGo.SetActive(mNpcGiftShowList.Count == 0);
		mScrollRect.dataSource = this;
		mScrollRect.totalCount = mNpcGiftShowList.Count;
		mScrollRect.RefillCells();
		GiftState giftState = GetGiftState();
		mStateSwitcher.SetState(giftState);
		ShowLevelUpPropContent();
		UpdateFriendShipTag();
	}

	private void UpdateFriendShipTag()
	{
		if (mNpcData == null)
		{
			UnityEngine.Debug.LogError("NpcDetailPanelGiftContent UpdateFriendShipTag Error, mNpcData is null");
			return;
		}
		if (mNpcInfo == null)
		{
			UnityEngine.Debug.LogError("NpcDetailPanelGiftContent UpdateFriendShipTag Error, mNpcInfo is null");
			return;
		}
		NpcFriendshipTagInfo tagInfoByRoleTypeAndLevel = services.GetTagInfoByRoleTypeAndLevel(mNpcInfo.NpcRoleType, mNpcData.FriendShipLevel);
		if (tagInfoByRoleTypeAndLevel != null)
		{
			bool flag = false;
			if (mNpcInfo.CanCustomFriendshipTag)
			{
				flag = mNpcData.FriendShipLevel >= services.CanCustomTagFriendShipLevel;
			}
			string key = ((flag && !string.IsNullOrEmpty(mNpcData.CustomFriendShipTag)) ? mNpcData.CustomFriendShipTag : services.GetNpcFriendshipTagNameKey(tagInfoByRoleTypeAndLevel.ClassId));
			mFriendshipTagText.SetKey(key);
			mFriendshipTagBg.sprite = mDisposableBag.Add(services.LoadSprite(tagInfoByRoleTypeAndLevel.TagBg)).Result;
			((Component)mCustomFriendshipTagBtn).gameObject.SetActive(flag);
		}
	}

	private void OnCustomFriendShipTag()
	{
		services.PopupInput((IPartnerGiftInputDialog go) =>
		{
			string title = "ui_npc_change_tag";
			string placeholder = "ui_npc_input_tag";
			go.Init(title, placeholder, (string input) =>
			{
				if (!services.IsAllCharacterValid(input))
				{
					services.ShowError(1150, null, false);
				}
				else
				{
					int num = services.CheckNpcFriendshipTagLegal(input);
					if (num != 0)
					{
						services.ShowError(num, null, false);
					}
					else
					{
						services.SetCustomTag(mNpcInfo.Id, input, (string repTag) =>
						{
							string key = string.Empty;
							if (string.IsNullOrEmpty(repTag))
							{
								NpcFriendshipTagInfo tagInfoByRoleTypeAndLevel = services.GetTagInfoByRoleTypeAndLevel(mNpcInfo.NpcRoleType, mNpcData.FriendShipLevel);
								if (tagInfoByRoleTypeAndLevel != null)
								{
									key = (string.IsNullOrEmpty(mNpcData.CustomFriendShipTag) ? services.GetNpcFriendshipTagNameKey(tagInfoByRoleTypeAndLevel.ClassId) : mNpcData.CustomFriendShipTag);
								}
							}
							else
							{
								services.SystemMessage(services.GetText("ui_npc_change_tag_success"));
								services.Player.SetNpcFriendshipTag(mNpcInfo.Id, repTag);
								key = repTag;
							}
							mFriendshipTagText.SetKey(key);
							services.Close(go.GameObject);
						});
					}
				}
			});
			go.SetNpcFriendshipTagModerationClickMask();
		});
	}

	public void ProvideData(Transform transform, int idx)
	{
		ItemIconNpcGift component = ((Component)transform).GetComponent<ItemIconNpcGift>();
		PlayerItemDataWrap playerItemDataWrap = mNpcGiftShowList[idx];
		services.ConfigureGiftIcon(component);
		component.Init(playerItemDataWrap);
		component.SetClickListener(mAddBtnClickAction);
		component.SetReduceBtnClickListener(mReduceBtnClickAction);
		RefreshItemIcon(component, 0L);
		if (!mGiftItemIconDict.ContainsKey(playerItemDataWrap.Id))
		{
			mGiftItemIconDict.Add(playerItemDataWrap.Id, component);
		}
		else
		{
			mGiftItemIconDict[playerItemDataWrap.Id] = component;
		}
		if (mSelectedGiftDict.TryGetValue(playerItemDataWrap.Id, out var value))
		{
			RefreshItemIcon(component, value);
		}
	}

	private void ShowLevelUpPropContent()
	{
		mPool.ReturnMany(mPropItems);
		int previewLevel = GetPreviewLevel();
		if (mNpcData.FriendShipLevel == previewLevel)
		{
			return;
		}
		int num = 0;
		IReadOnlyDictionary<PropType, long> friendshipLevelPropDiff = services.GetFriendshipLevelPropDiff(mNpcInfo.Properties, mNpcData.FriendShipLevel, previewLevel);
		Dictionary<int, List<(PropType, long)>> dictionary = new Dictionary<int, List<(PropType, long)>>();
		foreach (KeyValuePair<PropType, long> item in friendshipLevelPropDiff)
		{
			if (item.Value != 0L && services.IsUnlockTravelNpcProp(item.Key))
			{
				int key = num / 2;
				if (!dictionary.TryGetValue(key, out var value))
				{
					value = new List<(PropType, long)>();
					dictionary.Add(key, value);
				}
				value.Add((item.Key, item.Value));
				num++;
			}
		}
		foreach (KeyValuePair<int, List<(PropType, long)>> item2 in dictionary)
		{
			NpcFriendshipPropItemGroup npcFriendshipPropItemGroup = mPool.Borrow(mSlot);
			mPropItems.Add(npcFriendshipPropItemGroup);
			bool showBg = item2.Key % 2 == 0;
			npcFriendshipPropItemGroup.Configure(services);
			npcFriendshipPropItemGroup.Init(item2.Value, showBg);
		}
	}

	private int GetPreviewLevel()
	{
		NpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser = services.Levels;
		int maxLevel = npcFriendshipLevelInfoParser.GetMaxLevel(mNpcData.NpcClassId);
		int result = Math.Min(mNpcData.FriendShipLevel + 1, maxLevel);
		int allExpByDict = services.Gifts.GetAllExpByDict(mSelectedGiftDict, mNpcInfo.GiftPreferenceDict);
		if (allExpByDict == 0)
		{
			return result;
		}
		int friendshipLevelByExp = npcFriendshipLevelInfoParser.GetFriendshipLevelByExp(mNpcData.NpcClassId, mNpcData.FriendShip + allExpByDict, services.Player.SubRank);
		if (mNpcData.FriendShipLevel == friendshipLevelByExp)
		{
			return result;
		}
		return friendshipLevelByExp;
	}

	private void OnClickAddBtn(ItemIcon itemIcon)
	{
		if (IsMaxLevel())
		{
			services.SystemMessage(services.GetText("ui_npc_detail_give_gift_max_level_tip"));
			return;
		}
		if (IsFriendshipLevelLimit())
		{
			services.SystemMessage(services.GetText("ui_npc_detail_select_gift_level_limit_tip"));
			return;
		}
		if (IsFriendshipOverflow())
		{
			services.SystemMessage(services.GetText("ui_npc_detail_select_gift_overflow_tip"));
			return;
		}
		int classId = itemIcon.myInfo.ClassId;
		if (mSelectedGiftDict.TryGetValue(classId, out var value))
		{
			if (value >= itemIcon.myItemData.Count)
			{
				return;
			}
			mSelectedGiftDict[classId] = value + 1;
		}
		else
		{
			mSelectedGiftDict.Add(classId, 1L);
		}
		RefreshItemIcon(itemIcon, mSelectedGiftDict[classId]);
		ShowLevelUpPropContent();
		mShowExpPreviewAction?.Invoke(mSelectedGiftDict);
	}

	private void OnClickReduceBtn(ItemIcon itemIcon)
	{
		int classId = itemIcon.myInfo.ClassId;
		if (mSelectedGiftDict.TryGetValue(classId, out var value) && value > 0)
		{
			long num = value - 1;
			if (num <= 0)
			{
				mSelectedGiftDict.Remove(classId);
			}
			else
			{
				mSelectedGiftDict[classId] = num;
			}
			RefreshItemIcon(itemIcon, num);
			ShowLevelUpPropContent();
			mShowExpPreviewAction?.Invoke(mSelectedGiftDict);
		}
	}

	private void OnClickResetBtn()
	{
		if (mSelectedGiftDict.Count == 0)
		{
			services.SystemMessage(services.GetText("ui_npc_detail_gift_unselected_tip"));
		}
		else
		{
			Refresh();
		}
	}

	private void OnClickAutoSelectBtn()
	{
		if (mNpcGiftShowList.Count == 0)
		{
			services.SystemMessage(services.GetText("ui_npc_detail_gift_unselected_tip"));
			return;
		}
		if (IsFriendshipLevelLimit())
		{
			services.SystemMessage(services.GetText("ui_npc_detail_select_gift_level_limit_tip"));
			return;
		}
		int allExpByDict = services.Gifts.GetAllExpByDict(mSelectedGiftDict, mNpcInfo.GiftPreferenceDict);
		long num = mNpcData.FriendShip + allExpByDict;
		NpcFriendshipLevelInfo targetLevelInfo = GetTargetLevelInfo(num);
		if (targetLevelInfo == null)
		{
			services.SystemMessage(services.GetText("ui_npc_detail_select_gift_overflow_tip"));
			return;
		}
		long diffExp = targetLevelInfo.FriendshipExp - num;
		DoAutoSelectGift(diffExp);
	}

	private void OnClickGiveGiftBtn()
	{
		if (mSelectedGiftDict.Count == 0)
		{
			services.SystemMessage(services.GetText("ui_npc_detail_give_gift_unselected_tip"));
		}
		else if (IsFriendshipOverflow())
		{
			string textKey = "ui_npc_detail_give_gift_overflow_tip";
			services.PopupConfirm((IPartnerGiftConfirmDialog go) =>
			{
				go.Init(textKey, () =>
				{
					DoGiveGift();
				});
			});
		}
		else
		{
			DoGiveGift();
		}
	}

	private void DoGiveGift()
	{
		foreach (KeyValuePair<int, long> item in mSelectedGiftDict)
		{
			if (item.Value <= 0)
			{
				UnityEngine.Debug.LogError("NpcDetailPanelGiftContent DoGiveGift Error, mSelectedGiftDict Values <= 0");
				return;
			}
		}
		services.GiveGift(mNpcData.NpcClassId, mSelectedGiftDict, () =>
		{
			Refresh();
			mGiveGiftAction?.Invoke();
		});
	}

	private void DoAutoSelectGift(long diffExp)
	{
		foreach (PlayerItemDataWrap mNpcGiftSelect in mNpcGiftSelectList)
		{
			NpcGiftInfo info = services.GetGiftInfo(mNpcGiftSelect.Info);
			int giftFriendship = GetGiftFriendship(info);
			long val = (long)Math.Ceiling((double)diffExp / (double)giftFriendship);
			long num = 0L;
			if (mSelectedGiftDict.TryGetValue(mNpcGiftSelect.Id, out var value))
			{
				if (value >= mNpcGiftSelect.Count)
				{
					continue;
				}
				num = Math.Min(mNpcGiftSelect.Count - value, val);
				mSelectedGiftDict[mNpcGiftSelect.Id] = value + num;
			}
			else
			{
				num = Math.Min(mNpcGiftSelect.Count, val);
				mSelectedGiftDict.Add(mNpcGiftSelect.Id, num);
			}
			diffExp -= (int)(num * giftFriendship);
			if (mGiftItemIconDict.TryGetValue(mNpcGiftSelect.Id, out var value2))
			{
				RefreshItemIcon(value2, mSelectedGiftDict[mNpcGiftSelect.Id]);
			}
			if (diffExp <= 0)
			{
				break;
			}
		}
		ShowLevelUpPropContent();
		mShowExpPreviewAction?.Invoke(mSelectedGiftDict);
	}

	private bool IsFriendshipLevelLimit()
	{
		NpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser = services.Levels;
		int maxSubRankLevel = npcFriendshipLevelInfoParser.GetMaxSubRankLevel(mNpcData.NpcClassId, services.Player.SubRank);
		if (mNpcData.FriendShipLevel >= maxSubRankLevel)
		{
			return true;
		}
		int allExpByDict = services.Gifts.GetAllExpByDict(mSelectedGiftDict, mNpcInfo.GiftPreferenceDict);
		long friendshipExp = mNpcData.FriendShip + allExpByDict;
		if (npcFriendshipLevelInfoParser.GetFriendshipLevelByExp(mNpcData.NpcClassId, friendshipExp, services.Player.SubRank) >= maxSubRankLevel)
		{
			return true;
		}
		return false;
	}

	private GiftState GetGiftState()
	{
		if (IsMaxLevel())
		{
			return GiftState.MaxLevel;
		}
		if (IsFriendshipLevelLimit())
		{
			return GiftState.SubRankLimit;
		}
		return GiftState.Normal;
	}

	private int GetGiftFriendship(NpcGiftInfo info)
	{
		double num = ((!mNpcInfo.GiftPreferenceDict.TryGetValue(info.ClassId, out var value)) ? services.NpcGiftDefaultPreferenceFactor : value);
		return (int)Math.Round((double)info.Friendship * num);
	}

	private void RefreshItemIcon(ItemIcon itemIcon, long currentCount = 0L)
	{
		itemIcon.SetSubmitCount(currentCount);
		itemIcon.HideText();
		ItemIconNpcGift obj = itemIcon as ItemIconNpcGift;
		obj.SetSelectedGoActive(currentCount > 0);
		itemIcon.SetAttachment(ItemIconAttachmentType.ReduceTip, currentCount > 0);
		bool flag = mNpcInfo.GiftPreferenceDict.ContainsKey(itemIcon.myInfo.ClassId);
		obj.SetPreferenceGoActive(flag);
		mNpcGiftBlueDotItem.Refresh();
		bool show = flag && currentCount <= 0 && mNpcGiftBlueDotItem.IsShow;
		itemIcon.SetAttachment(ItemIconAttachmentType.Upgrade, show);
	}

	private NpcFriendshipLevelInfo GetTargetLevelInfo(long targetFriendship)
	{
		NpcFriendshipLevelInfo result = null;
		foreach (KeyValuePair<int, NpcFriendshipLevelInfo> item in services.Levels.Get(mNpcData.NpcClassId))
		{
			if (item.Key > mNpcData.FriendShipLevel && item.Value.FriendshipExp > targetFriendship)
			{
				result = item.Value;
				break;
			}
		}
		return result;
	}

	private bool IsFriendshipOverflow()
	{
		NpcFriendshipLevelInfo maxLevelInfo = services.Levels.GetMaxLevelInfo(mNpcData.NpcClassId);
		int allExpByDict = services.Gifts.GetAllExpByDict(mSelectedGiftDict, mNpcInfo.GiftPreferenceDict);
		return mNpcData.FriendShip + allExpByDict > maxLevelInfo.FriendshipExp;
	}

	private bool IsMaxLevel()
	{
		return services.Levels.GetMaxLevel(mNpcData.NpcClassId) == mNpcData.FriendShipLevel;
	}
}

}
