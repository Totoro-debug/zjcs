// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryBubbles;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

public class NpcDialoguePanelItemContent : UIBase
{
    private IPartnerDialoguePlaybackServices services;
    public void Configure(IPartnerDialoguePlaybackServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	private enum AnimationStyle
	{
		Normal,
		Instant
	}

	[SerializeField]
	private Component mPlayerIcon;

	[SerializeField]
	private MultiLanguageText mText;

	[SerializeField]
	private TMP_TypeWriterEffect mTypeWriterEffect;

	[SerializeField]
	private WorldStateSwitcher<AnimationStyle> mStateSwitcher;

	private IWorldTextProcessor mTextProcessor;

	private long mActorId;

	private int mDialogueClassId;

	private int mDialogueNodeId;

	private bool mInstant;

	public bool IsPlaying
	{
		get
		{
			if ((UnityEngine.Object)(object)mTypeWriterEffect == (UnityEngine.Object)null || !((Component)mTypeWriterEffect).gameObject.activeInHierarchy)
			{
				return false;
			}
			return mTypeWriterEffect.IsPlaying;
		}
	}

	public void Init(long actorId, int dialogueClassId, int dialogueNodeId, bool instant = true)
	{
		InitWrapper(() =>
		{
			mActorId = actorId;
			mDialogueClassId = dialogueClassId;
			mDialogueNodeId = dialogueNodeId;
			mInstant = instant;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (mTextProcessor == null)
		{
			mTextProcessor = new PartnerDialoguePlayerNameProcessor(services.PlayerName ?? "");
		}
		mStateSwitcher.SetState(mInstant ? AnimationStyle.Instant : AnimationStyle.Normal);
		RefreshItem();
	}

	protected override void OnDisableAndInited()
	{
		mTextProcessor = null;
		base.OnDisableAndInited();
	}

	private void RefreshItem()
	{
		PartnerConversationNode npcDialogueInfo = services.Configuration.Get(mDialogueClassId, mDialogueNodeId);
		if (npcDialogueInfo == null)
		{
			Debug.LogError("NpcDialoguePanelItemContent: original Error branch.");
		}
		else
		{
			if (((PartnerDialogueSpokesMan)npcDialogueInfo.SpokesMan) == PartnerDialogueSpokesMan.System)
			{
				RefreshSystemItem(npcDialogueInfo);
			}
			else
			{
				RefreshPlayerNpcItem(npcDialogueInfo);
			}
			RefreshContent(npcDialogueInfo);
		}
	}

	private void RefreshSystemItem(PartnerConversationNode info)
	{
		if ((UnityEngine.Object)(object)mPlayerIcon != (UnityEngine.Object)null)
		{
			((Component)mPlayerIcon).gameObject.SetActive(false);
		}
	}

	private void RefreshPlayerNpcItem(PartnerConversationNode info)
	{
		services.InitializeActorIcon(mPlayerIcon, mActorId, (PartnerDialogueSpokesMan)info.SpokesMan);
		((Component)mPlayerIcon).gameObject.SetActive(true);
	}

	private void RefreshContent(PartnerConversationNode info)
	{
		string npcDialogueTextKey = PartnerDialogueListRules.GetNpcDialogueTextKey(mDialogueClassId, mDialogueNodeId);
		mText.SetKey(npcDialogueTextKey, null, mTextProcessor);
		((Component)mText).gameObject.SetActive(true);
		if ((UnityEngine.Object)(object)mTypeWriterEffect != (UnityEngine.Object)null && !mInstant)
		{
			mTypeWriterEffect.StartType();
		}
	}

	public void FastEndTypeWriterEffect()
	{
		mTypeWriterEffect.FastEndType();
		mStateSwitcher.SetState(AnimationStyle.Instant);
	}
}

}
