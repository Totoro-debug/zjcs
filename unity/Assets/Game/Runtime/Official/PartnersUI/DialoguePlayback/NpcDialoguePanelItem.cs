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

public class NpcDialoguePanelItem : UIBase
{
    private IPartnerDialoguePlaybackServices services;
    public void Configure(IPartnerDialoguePlaybackServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	[SerializeField]
	private MonoDictionary<PartnerDialogueSpokesMan, NpcDialoguePanelItemContent> mSpokeContentDict;

	[SerializeField]
	private NpcDialoguePanelRewardItemContent mRewardItem;

	private long mActorId;

	private int mDialogueClassId;

	private int mDialogueNodeId;

	private bool mInstant;

	public bool IsPlaying
	{
		get
		{
			bool flag = false;
			foreach (KeyValuePair<PartnerDialogueSpokesMan, NpcDialoguePanelItemContent> item in mSpokeContentDict)
			{
				NpcDialoguePanelItemContent value = item.Value;
				if (!((Component)value).gameObject.activeInHierarchy || !value.IsPlaying)
				{
					continue;
				}
				if (flag)
				{
					Debug.LogError("NpcDialoguePanelItem: original Error branch.");
				}
				else
				{
					flag = true;
				}
			}
			return flag;
		}
	}

	public void Init(long actorId, int dialogueClassId, int dialogueNodeId, bool instant)
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
		PartnerConversationNode npcDialogueInfo = services.Configuration.Get(mDialogueClassId, mDialogueNodeId);
		foreach (KeyValuePair<PartnerDialogueSpokesMan, NpcDialoguePanelItemContent> item in mSpokeContentDict)
		{
			((Component)item.Value).gameObject.SetActive(false);
			if (item.Key == ((PartnerDialogueSpokesMan)npcDialogueInfo.SpokesMan))
			{
				item.Value.Init(mActorId, mDialogueClassId, mDialogueNodeId, mInstant);
				((Component)item.Value).gameObject.SetActive(true);
			}
		}
		if (npcDialogueInfo.RewardItem != null && npcDialogueInfo.RewardItem.Count > 0)
		{
			mRewardItem.Init(npcDialogueInfo.RewardItem);
			((Component)mRewardItem).gameObject.SetActive(true);
		}
		else
		{
			((Component)mRewardItem).gameObject.SetActive(false);
		}
	}

	public void FastEndTypeWriterEffect()
	{
		foreach (KeyValuePair<PartnerDialogueSpokesMan, NpcDialoguePanelItemContent> item in mSpokeContentDict)
		{
			NpcDialoguePanelItemContent value = item.Value;
			if (((Component)value).gameObject.activeInHierarchy && value.IsPlaying)
			{
				value.FastEndTypeWriterEffect();
			}
		}
	}
}

}
