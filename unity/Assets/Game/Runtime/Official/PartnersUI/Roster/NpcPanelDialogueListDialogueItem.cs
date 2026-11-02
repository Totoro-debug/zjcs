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

public class NpcPanelDialogueListDialogueItem : UIBase, IShowSequenceItem
{
    private IPartnerDialogueUiServices services;
    public void Configure(IPartnerDialogueUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        mTextProcessor = new PartnerDialoguePlayerNameProcessor(services.PlayerName ?? "");
    }

	[SerializeField]
	private GameObject mContentRoot;

	[SerializeField]
	private WorldStateSwitcher<PartnerDialogueStatus> mStateSwitcher;

	[SerializeField]
	private List<MultiLanguageText> mTexts;

	[SerializeField]
	private Button mButton;

	[SerializeField]
	private GameObject mRedDot;

	[SerializeField]
	private Animation mAnimation;

	[SerializeField]
	private AnimationClip mFadeInClip;

	private IWorldTextProcessor mTextProcessor;

	private PartnerDialogueData mNpcDialogueDataWrap;

	private bool mInstantFadeIn;

	protected void Awake()
	{
		((UnityEvent)mButton.onClick).AddListener((UnityAction)OnBtnClicked);

	}

	protected override void OnDestroy()
	{
		mTextProcessor = null;
		base.OnDestroy();
	}

	private void OnBtnClicked()
	{
		services.ShowDialogue(mNpcDialogueDataWrap);
	}

	public void Show()
	{
		mContentRoot.SetActive(true);
		TryPlayFadeIn();
	}

	public void Hide()
	{
		mContentRoot.SetActive(false);
	}

	public void Init(PartnerDialogueData data, bool instant)
	{
		InitWrapper(() =>
		{
			mContentRoot.SetActive(false);
			mNpcDialogueDataWrap = data;
			mInstantFadeIn = instant;
			Refresh();
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		services.DialogueUpdated += MainPlayer_OnNpcDialogueUpdate;
	}

	protected override void OnDisableAndInited()
	{
		if (services.MainPlayerAvailable)
		{
			services.DialogueUpdated -= MainPlayer_OnNpcDialogueUpdate;
		}
		base.OnDisableAndInited();
	}

	private void TryPlayFadeIn()
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
		Refresh();
	}

	private void Refresh()
	{
		PartnerDialogueStatus dialogueStatus = GetPartnerDialogueStatus();
		mStateSwitcher.SetState(dialogueStatus);
		RefreshRedDot();
		RefreshText();
	}

	private void RefreshText()
	{
		string npcDialogueTextKey = PartnerDialogueListRules.GetNpcDialogueTextKey(mNpcDialogueDataWrap.DialogueClassId, services.DialogueStartNodeId);
		SetText(npcDialogueTextKey);
	}

	private void SetText(string key, string param = null)
	{
		foreach (MultiLanguageText mText in mTexts)
		{
			if (string.IsNullOrEmpty(param))
			{
				mText.SetKey(key, null, mTextProcessor);
				continue;
			}
			mText.SetKey(key, null, mTextProcessor, param);
		}
	}

	private void RefreshRedDot()
	{
		bool active = PartnerDialogueListRules.HasRedDot(mNpcDialogueDataWrap, services);
		mRedDot.SetActive(active);
	}

	private PartnerDialogueStatus GetPartnerDialogueStatus()
	{
		return mNpcDialogueDataWrap.DialogueStatus;
	}
}

}
