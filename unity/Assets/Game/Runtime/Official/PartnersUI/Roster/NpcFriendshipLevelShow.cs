// Ported from active official client; see partner-roster-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerRosterLanguage;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.SkillsUI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NpcFriendshipLevelShow : UIBase
{
    private IPartnerRosterUiServices services;
    public void Configure(IPartnerRosterUiServices value) { services = value; }

	public enum PreviewState
	{
		None,
		Add,
		MaxLevel
	}

	[SerializeField]
	private List<Image> mFilledImages;

	[SerializeField]
	private List<Image> mPreviewImages;

	[SerializeField]
	private MultiLanguageText mLevelText;

	[SerializeField]
	private MultiLanguageText mPreviewExpText;

	[SerializeField]
	private WorldStateSwitcher<bool> mMaxLevelStateSwitcher;

	[SerializeField]
	private WorldStateSwitcher<PreviewState> mPreviewStateSwitcher;

	private int mClassId;

	private int mFriendshipLevel;

	private long mFriendship;

	private long mAddFriendship;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		services.NpcFriendshipUpdated += MainPlayer_OnNpcFriendshipUpdate;
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		if (services.MainPlayerAvailable)
		{
			services.NpcFriendshipUpdated -= MainPlayer_OnNpcFriendshipUpdate;
		}
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnNpcFriendshipUpdate(int npcClassId)
	{
		if (mClassId == npcClassId)
		{
			NpcRosterItem byNpcClassId = services.GetNpc(npcClassId);
			mFriendship = byNpcClassId.FriendshipExp;
			mFriendshipLevel = byNpcClassId.FriendshipLevel;
			Refresh();
		}
	}

	public void Init(int npcClassId, long npcFriendship, int npcFriendshipLevel, long addFriendship = 0L)
	{
		InitWrapper(() =>
		{
			mClassId = npcClassId;
			mFriendship = npcFriendship;
			mFriendshipLevel = npcFriendshipLevel;
			mAddFriendship = addFriendship;
		});
	}

	public void Init(int npcClassId)
	{
		if (services.MainPlayerAvailable)
		{
			long npcFriendship = 0L;
			int npcFriendshipLevel = 1;
			NpcRosterItem byNpcClassId = services.GetNpc(npcClassId);
			if (byNpcClassId != null)
			{
				npcFriendship = byNpcClassId.FriendshipExp;
				npcFriendshipLevel = byNpcClassId.FriendshipLevel;
			}
			Init(npcClassId, npcFriendship, npcFriendshipLevel, 0L);
		}
	}

	private void Refresh()
	{
		bool flag = mFriendshipLevel >= services.FriendshipMaxLevel(mClassId);
		float fillAmount;
		if (flag)
		{
			mMaxLevelStateSwitcher.SetState(state: true);
			fillAmount = 1f;
		}
		else
		{
			long currentThreshold = services.FriendshipExp(mClassId, mFriendshipLevel);
			long nextThreshold = services.FriendshipExp(mClassId, mFriendshipLevel + 1);
			long num = mFriendship - currentThreshold;
			long num2 = nextThreshold - currentThreshold;
			fillAmount = (float)num / (float)num2;
			mLevelText.SetKey("{0}", mFriendshipLevel);
			mMaxLevelStateSwitcher.SetState(state: false);
			if (mAddFriendship > 0)
			{
				ShowPreviewContent(num, num2);
			}
		}
		if (mAddFriendship > 0)
		{
			mPreviewExpText.SetKey("+{0}", mAddFriendship);
			mPreviewStateSwitcher.SetState((!flag) ? PreviewState.Add : PreviewState.MaxLevel);
		}
		else
		{
			mPreviewStateSwitcher.SetState(PreviewState.None);
		}
		foreach (Image mFilledImage in mFilledImages)
		{
			mFilledImage.fillAmount = fillAmount;
		}
	}

	private void ShowPreviewContent(long currentExp, long nextExp)
	{
		float fillAmount = (float)(currentExp + mAddFriendship) / (float)nextExp;
		foreach (Image mPreviewImage in mPreviewImages)
		{
			mPreviewImage.fillAmount = fillAmount;
		}
	}
}

}
