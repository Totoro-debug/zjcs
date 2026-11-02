// Ported original controller; see partner-dialogue-list-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NpcPanelDialogueListNpcItem : UIBase
{
    private IPartnerDialogueUiServices services;
    public void Configure(IPartnerDialogueUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
    }

	private enum Status
	{
		Fold,
		Unfold
	}

	[SerializeField]
	private Animation mAnimation;

	[SerializeField]
	private AnimationClip mFadeInClip;

	[SerializeField]
	private Component mIcon;

	[SerializeField]
	private GameObject mRedDot;

	[SerializeField]
	private MultiLanguageText mRedDotCount;

	[SerializeField]
	private Button mBtn;

	[SerializeField]
	private Transform mDialogueRoot;

	[SerializeField]
	private ListItemShowSequence mItemShowSequence;

	[SerializeField]
	private GameObject mDialogueItemPrefab;

	[SerializeField]
	private WorldStateSwitcher<Status> mStateSwitcher;

	private Status mStatus;

	private PartnerDialogueNpcData mNpcData;

	private bool mInstantFadeIn;

	private List<PartnerDialogueData> mPlayerNpcDialogues;

	protected void Awake()
	{
		((UnityEvent)mBtn.onClick).AddListener((UnityAction)OnBtnClicked);
	}

	private void OnBtnClicked()
	{
		if (mPlayerNpcDialogues.Count <= 0)
		{
			services.ShowError(1506);
			return;
		}
		Status status = GetStatus();
		mStatus = status;
		mStateSwitcher.SetState(mStatus);
		TryRefreshDialogues(instant: false);
	}

	private Status GetStatus()
	{
		if (mStatus != Status.Fold)
		{
			_ = 1;
			return Status.Fold;
		}
		return Status.Unfold;
	}

	public void Init(PartnerDialogueNpcData npcData, List<PartnerDialogueData> dialogues, bool instant)
	{
		InitWrapper(() =>
		{
			mNpcData = npcData;
			mPlayerNpcDialogues = dialogues;
			mInstantFadeIn = instant;
			mStatus = Status.Fold;
			mDialogueItemPrefab.SetActive(false);
			services.InitializeNpcIcon(mIcon, mNpcData);
			TryRefreshDialogues(instant: true);
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		services.DialogueUpdated += MainPlayer_OnNpcDialogueUpdate;
		TryPlayFadeInAnimation();
	}

	protected override void OnDisableAndInited()
	{
		if (services.MainPlayerAvailable)
		{
			services.DialogueUpdated -= MainPlayer_OnNpcDialogueUpdate;
		}
		mNpcData = null;
		mPlayerNpcDialogues = null;
		base.OnDisableAndInited();
	}

	private void TryPlayFadeInAnimation()
	{
		if (!((UnityEngine.Object)(object)mAnimation == (UnityEngine.Object)null) && !((UnityEngine.Object)(object)mFadeInClip == (UnityEngine.Object)null))
		{
			string name = ((UnityEngine.Object)mFadeInClip).name;
			float time = 0f;
			if (mInstantFadeIn)
			{
				time = mAnimation.GetClip(name).length;
			}
			mAnimation[name].time = time;
			mAnimation.Play(name);
		}
	}

	private void MainPlayer_OnNpcDialogueUpdate(PartnerDialogueData obj)
	{
		if (obj.NpcId == mNpcData.NpcId)
		{
			RefreshRedDot();
		}
	}

	private void TryRefreshDialogues(bool instant)
	{
		if (mStatus == Status.Unfold)
		{
			RefreshDialogues(instant);
		}
		RefreshRedDot();
	}

	private void RefreshDialogues(bool instant)
	{
		((Component)mDialogueRoot).gameObject.SetActive(true);
		for (int index = mDialogueRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = mDialogueRoot.GetChild(index);
            if (child.gameObject.activeSelf) UnityEngine.Object.Destroy(child.gameObject);
        }
		foreach (PartnerDialogueData mPlayerNpcDialogue in mPlayerNpcDialogues)
		{
			NpcPanelDialogueListDialogueItem npcPanelDialogueListDialogueItem = services.Instantiate(mDialogueItemPrefab, mDialogueRoot, false).GetComponent<NpcPanelDialogueListDialogueItem>();
			npcPanelDialogueListDialogueItem.Init(mPlayerNpcDialogue, instant);
			mItemShowSequence.AddItem(npcPanelDialogueListDialogueItem);
		}
	}

	private void RefreshRedDot()
	{
		int redDotCount = PartnerDialogueListRules.GetRedDotCount(mPlayerNpcDialogues, services);
		mRedDot.SetActive(redDotCount > 0);
		mRedDotCount.Format(redDotCount);
	}
}

}
