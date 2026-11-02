// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryBubbles;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using UnityEngine;

public class NpcDialoguePanelOption : UIBase
{
    private IPartnerDialoguePlaybackServices services;
    public void Configure(IPartnerDialoguePlaybackServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	[SerializeField]
	private GameObject mTextOptionsGo;

	[SerializeField]
	private List<NpcDialoguePanelOptionItem> mTextOptions;

	private long mDialogueId;

	private PartnerConversationNode mInfo;

	public void Init(long dialogueId, PartnerConversationNode info)
	{
		InitWrapper(() =>
		{
			mDialogueId = dialogueId;
			mInfo = info;
			foreach (NpcDialoguePanelOptionItem mTextOption in mTextOptions)
			{
				((Component)mTextOption).gameObject.SetActive(false);
			}
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		RegisterOptionEvent();
		RefreshOptions();
	}

	protected override void OnDisableAndInited()
	{
		UnregisterOptionEvent();
		ResetInitState();
		base.OnDisableAndInited();
	}

	public override void ResetInitState()
	{
		mInfo = null;
		base.ResetInitState();
	}

	private void RefreshOptions()
	{
		RefreshTextOptions();
	}

	private void RefreshTextOptions()
	{
		List<int> nextNodes = mInfo.NextNodes;
		int count = nextNodes.Count;
		for (int i = 0; i < mTextOptions.Count; i++)
		{
			NpcDialoguePanelOptionItem npcDialoguePanelOptionItem = mTextOptions[i];
			((Component)npcDialoguePanelOptionItem).gameObject.SetActive(false);
			if (i < count)
			{
				npcDialoguePanelOptionItem.Init(mDialogueId, mInfo.ClassId, nextNodes[i]);
				((Component)npcDialoguePanelOptionItem).gameObject.SetActive(true);
			}
		}
	}

	private void RegisterOptionEvent()
	{
		foreach (NpcDialoguePanelOptionItem mTextOption in mTextOptions)
		{
			mTextOption.OnSelect += OnOptionSelected;
		}
	}

	private void UnregisterOptionEvent()
	{
		foreach (NpcDialoguePanelOptionItem mTextOption in mTextOptions)
		{
			mTextOption.OnSelect -= OnOptionSelected;
		}
	}

	private void OnOptionSelected()
	{
		((Component)this).gameObject.SetActive(false);
	}
}

}
