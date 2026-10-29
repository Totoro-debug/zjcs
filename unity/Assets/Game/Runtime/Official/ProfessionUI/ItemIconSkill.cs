namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ItemIconSkill : ItemIcon
{
    private IProfessionSkillIconBindings mBindings;
    private IProfessionSkillIconBindings Bindings => mBindings ?? ProfessionSkillIconBindings.Required;
    public void Configure(IProfessionSkillIconBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	[Header("技能星级")]
	public SkillStarInfo SkillStarInfo;

	public GameObject SelectedObj;

	public BlessLevelTextViewChanger CurLevelTextView;

	[SerializeField]
	private GameObject mShadowObj;

	[Header("Debug")]
	[SerializeField]
	private TMP_Text mDebugProfessionRankText;

	private bool mShowSkillStar;

	private Action<IProfessionItemData, int, int> mOnSkillRankUpdateCache;

	protected override void Awake()
	{
		base.Awake();
		mOnSkillRankUpdateCache = MainPlayer_OnSkillRankUp;
	}

	public override ItemIcon Init(IProfessionItemInfo info)
	{
		base.Init(info);
		CurLevelTextView.Refresh(isBlessing: false);
		HideSkillStar();
		ShowSkillLevel(show: false);
		RefreshSkillSkin(myItemData);
		RefreshDebugProfessionRankDisplay();
		return this;
	}

	public override ItemIcon Init(IProfessionItemData data)
	{
		base.Init(data);
		RefreshSkillStar();
		RefreshDebugProfessionRankDisplay();
		return this;
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Bindings.MainPlayer.OnSkillRankUp += mOnSkillRankUpdateCache;
		Bindings.MainPlayer.OnSkillRankDown += mOnSkillRankUpdateCache;
		Bindings.MainPlayer.OnSkillRankUpBatch += MainPlayer_OnSkillRankUp;
		Bindings.MainPlayer.OnSkillSkillEquiped += Player_OnSkillSkinEquiped;
		Bindings.DebugShowSkillRankChanged += RefreshDebugProfessionRankDisplay;
		RefreshDebugProfessionRankDisplay();
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		Bindings.DebugShowSkillRankChanged -= RefreshDebugProfessionRankDisplay;
		if (Bindings.MainPlayer != null)
		{
			Bindings.MainPlayer.OnSkillRankUp -= mOnSkillRankUpdateCache;
			Bindings.MainPlayer.OnSkillRankDown -= mOnSkillRankUpdateCache;
			Bindings.MainPlayer.OnSkillRankUpBatch -= MainPlayer_OnSkillRankUp;
			Bindings.MainPlayer.OnSkillSkillEquiped -= Player_OnSkillSkinEquiped;
		}
	}

	private void MainPlayer_OnSkillRankUp(IProfessionItemData itemData, int preRank, int newRank)
	{
		if (myItemData != null && myInfo != null && myInfo.ClassId == itemData.Info.ClassId)
		{
			RefreshSkillItemRank();
		}
	}

	private void MainPlayer_OnSkillRankUp(Dictionary<int, int> skillRanks, Dictionary<int, int> preSkillRanks)
	{
		if (myItemData != null && myInfo != null && skillRanks.ContainsKey(myInfo.ClassId))
		{
			RefreshSkillItemRank();
		}
	}

	private void RefreshSkillItemRank()
	{
		InitQualityImage();
		if (mShowSkillStar)
		{
			RefreshSkillStar();
		}
	}

	private bool CheckItemDataMatch(IProfessionItemData itemData)
	{
		if (myInfo == null)
		{
			return false;
		}
		if (itemData != null)
		{
			return myInfo.ClassId == itemData.Info.ClassId;
		}
		return false;
	}

	public void ShowSkillLevel(bool show, int level = 0, bool isBlessing = false)
	{
		if (show && level != 0)
		{
			ProfessionItemText text = new ProfessionItemText { Key = "ui_common_level_style_3", Args = new object[] { level } };
			SetText(text);
		}
		else
		{
			HideText();
		}
		CurLevelTextView.Refresh(isBlessing);
	}

	public void ShowSkillRank(bool show, int rank)
	{
		if ((Object)(object)SkillStarInfo == (Object)null)
		{
			return;
		}
		if (show)
		{
			if (rank < 0)
			{
				IProfessionSkillParameter playerItemParamSkillWrap = myItemData == null ? null : Bindings.GetItemParamSkill(myItemData);
				if (playerItemParamSkillWrap != null)
				{
					rank = playerItemParamSkillWrap.GetRank();
				}
			}
			if (rank > 0)
			{
				SkillStarInfo.Show(rank, showStarNum: true);
				UpdateQualityImage(Bindings.GetRank(rank).Quality);
			}
			else
			{
				SkillStarInfo.Hide();
				InitQualityImage();
			}
		}
		else
		{
			SkillStarInfo.Hide();
		}
	}

	private void RefreshSkillStar()
	{
		if (myItemData != null && !((Object)(object)SkillStarInfo == (Object)null))
		{
			IProfessionSkillParameter itemParamSkill = Bindings.GetItemParamSkill(myItemData);
			if (itemParamSkill != null)
			{
				SkillStarInfo.Show(itemParamSkill.GetRank(), showStarNum: true);
				mShowSkillStar = true;
			}
		}
	}

	private void RefreshDebugProfessionRankDisplay()
	{
		if ((Object)(object)mDebugProfessionRankText == (Object)null)
		{
			return;
		}
		if (!Bindings.DebugShowSkillRank || myInfo == null)
		{
			((Component)mDebugProfessionRankText).gameObject.SetActive(false);
			return;
		}
		int professionRank = Bindings.GetDebugProfessionRank(myInfo.ClassId);
		if (professionRank <= 0)
		{
			((Component)mDebugProfessionRankText).gameObject.SetActive(false);
			return;
		}
		((Component)mDebugProfessionRankText).gameObject.SetActive(true);
		WorldMultiLanguageText component = ((Component)mDebugProfessionRankText).GetComponent<WorldMultiLanguageText>();
		if ((Object)(object)component != (Object)null)
		{
			component.myKey = string.Empty;
		}
		mDebugProfessionRankText.SetText(professionRank.ToString(), true);
	}

	public override void Clear()
	{
		base.Clear();
		SelectedObj.SetActive(false);
		if ((Object)(object)mDebugProfessionRankText != (Object)null)
		{
			((Component)mDebugProfessionRankText).gameObject.SetActive(false);
		}
	}

	public ItemIconSkill SetProfession(int targetType, int profession, out bool isMatchProfession)
	{
		isMatchProfession = !Bindings.IsPlayerTarget(targetType) || Bindings.IsSkillAvailableForProfession(myInfo.ClassId, profession);
		SetAttachment(ItemIconAttachmentType.ProfessionMismatchTip, !isMatchProfession);
		return this;
	}

	public ItemIconSkill CheckAndSetProfessionMismatchTip(out bool isMatchProfession)
	{
		bool flag = Bindings.IsPetSkill(myInfo);
		isMatchProfession = Bindings.MainPlayer.IsSkillAvailableForCurProfession(myInfo.ClassId);
		isMatchProfession |= flag;
		SetAttachment(ItemIconAttachmentType.ProfessionMismatchTip, !isMatchProfession);
		return this;
	}

	public ItemIconSkill SetSkillGMDisabled()
	{
		int classId = myInfo.ClassId;
		bool show = Bindings.IsSkillGMDisabled(classId);
		SetAttachment(ItemIconAttachmentType.GMDisabled, show);
		return this;
	}

	public void SetSelectedObjActive(bool value)
	{
		SelectedObj.SetActive(value);
	}

	public void HideSkillStar()
	{
		if ((Object)(object)SkillStarInfo != (Object)null)
		{
			SkillStarInfo.Hide();
		}
		mShowSkillStar = false;
	}

	public void SetShadowObjActive(bool value)
	{
		mShadowObj.SetActive(value);
	}

	public void SetClickPopupInfoDialog(object propDataProvider)
	{
		SetClickListener((ItemIcon itemIcon) =>
		{
            if (itemIcon.myItemData != null)
                Bindings.PopupSkillDetail(itemIcon.myItemData, propDataProvider);
            else if (itemIcon.myInfo != null)
                Bindings.PopupSkillDetail(itemIcon.myInfo.ClassId, 0L);
		});
	}

	private void Player_OnSkillSkinEquiped(int skillClassId, int skillSkinId)
	{
		if (myItemData != null && Bindings.MainPlayer.ContainsItemAtIndex(myItemData) && myItemData.Id == skillClassId)
		{
			RefreshSkillSkin(myItemData);
		}
	}

	private void RefreshSkillSkin(IProfessionItemData itemData)
	{
		if (itemData == null)
		{
			return;
		}
		IProfessionSkillParameter itemParamSkill = Bindings.GetItemParamSkill(itemData);
		if (itemParamSkill != null)
		{
			int skillSkinId = itemParamSkill.SkillSkinId;
			if (Bindings.HasSkillSkin(skillSkinId))
			{
				IProfessionItemInfo itemIcon = Bindings.GetItemInfo(skillSkinId);
				SetItemIcon(itemIcon);
			}
		}
	}

	public void SetWearObjActive(bool value)
	{
		SetAttachment(ItemIconAttachmentType.WearingAppendAppearance, value);
	}
}

}
