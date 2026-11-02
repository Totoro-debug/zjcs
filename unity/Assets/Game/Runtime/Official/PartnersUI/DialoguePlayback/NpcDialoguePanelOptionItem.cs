// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryBubbles;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NpcDialoguePanelOptionItem : UIBase
{
    private IPartnerDialoguePlaybackServices services;
    public void Configure(IPartnerDialoguePlaybackServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	[SerializeField]
	private MultiLanguageText mText;

	[SerializeField]
	private TextMeshProUGUI mEmojiText;

	[SerializeField]
	private Button mSelectBtn;

	private long mDialogueId;

	private int mDialogueClassId;

	private int mDialogueNodeId;

	public event Action OnSelect;

	protected void Awake()
	{
		((UnityEvent)mSelectBtn.onClick).AddListener((UnityAction)OnSelectBtnClicked);
	}

	private void OnSelectBtnClicked()
	{
		services.Acknowledge(mDialogueId, mDialogueNodeId, true);
		OnSelect?.Invoke();
	}

	public void Init(long dialogueId, int dialogueClassId, int dialogueNodeId)
	{
		InitWrapper(() =>
		{
			mDialogueId = dialogueId;
			mDialogueClassId = dialogueClassId;
			mDialogueNodeId = dialogueNodeId;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh(mDialogueClassId, mDialogueNodeId);
	}

	protected override void OnDisableAndInited()
	{
		ResetInitState();
		base.OnDisableAndInited();
	}

	public override void ResetInitState()
	{
		mDialogueId = 0L;
		mDialogueClassId = 0;
		mDialogueNodeId = 0;
		base.ResetInitState();
	}

	private void Refresh(int dialogueClassId, int dialogueNodeId)
	{
		if (services.Configuration.Get(dialogueClassId, dialogueNodeId) == null)
		{
			Debug.LogError("NpcDialoguePanelOptionItem: original Error branch.");
		}
		else
		{
			string npcDialogueOptionKey = PartnerDialoguePlaybackRules.GetNpcDialogueOptionKey(dialogueClassId, dialogueNodeId);
			mText.SetKey(npcDialogueOptionKey);
		}
	}
}

}
