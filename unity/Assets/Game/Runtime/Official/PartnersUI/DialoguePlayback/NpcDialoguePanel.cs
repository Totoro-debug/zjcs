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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NpcDialoguePanel : UIBase, LoopScrollDataSource
{
    private IPartnerDialoguePlaybackServices services;
    public void Configure(IPartnerDialoguePlaybackServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	[SerializeField]
	private PanelTitle mPanelTitle;

	[SerializeField]
	private LoopVerticalScrollRect mLoop;

	[SerializeField]
	private NpcDialoguePanelOption mDialogueOption;

	[SerializeField]
	private Button mFastEndTypeWriterBtn;

	[SerializeField]
	private float mDialoguePlayIntervalS = 1f;

	private NpcDialoguePanelItem mPlayingItem;

	private List<int> mDialogueNodes = new List<int>();

	private PartnerDialogueData mData;

	private bool mInstant = true;

	private Coroutine mDelayCo;

	private WaitForSeconds mWaitForSeconds;

	private Coroutine mMoveNextCo;

	private int DialogueClassId => mData.DialogueClassId;

	private WaitForSeconds WaitForSeconds
	{
		get
		{
			if (mWaitForSeconds == null)
			{
				mWaitForSeconds = new WaitForSeconds(mDialoguePlayIntervalS);
			}
			return mWaitForSeconds;
		}
	}

	private bool IsPlaying
	{
		get
		{
			if ((UnityEngine.Object)(object)mPlayingItem != (UnityEngine.Object)null)
			{
				return mPlayingItem.IsPlaying;
			}
			return false;
		}
	}

	public void ProvideData(Transform transform, int index)
	{
		if (index < 0 || index >= mDialogueNodes.Count)
		{
			Debug.LogError("NpcDialoguePanel: original Error branch.");
			return;
		}
		int num = mDialogueNodes[index];
		PartnerConversationNode npcDialogueInfo = services.Configuration.Get(DialogueClassId, num);
		if (npcDialogueInfo == null)
		{
			Debug.LogError("NpcDialoguePanel: original Error branch.");
			return;
		}
		long actorId = 0L;
		if (((PartnerDialogueSpokesMan)npcDialogueInfo.SpokesMan) == PartnerDialogueSpokesMan.NPC)
		{
			actorId = mData.NpcId;
		}
		else if (((PartnerDialogueSpokesMan)npcDialogueInfo.SpokesMan) == PartnerDialogueSpokesMan.Player)
		{
			actorId = mData.PlayerId;
		}
		bool instant = GetInstant(index, mDialogueNodes.Count);
		NpcDialoguePanelItem component = ((Component)transform).GetComponent<NpcDialoguePanelItem>();
		component.Init(actorId, DialogueClassId, num, instant);
		LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)(object)((transform is RectTransform) ? transform : null));
		if (!instant && (Object)(object)mPlayingItem != (UnityEngine.Object)null)
		{
			Debug.LogError("NpcDialoguePanel: original Error branch.");
		}
		if (!instant)
		{
			mPlayingItem = component;
			((Component)mFastEndTypeWriterBtn).gameObject.SetActive(true);
		}
	}

	private bool GetInstant(int index, int count)
	{
		if (count == 1)
		{
			return mInstant;
		}
		if (!mInstant)
		{
			return index != count - 1;
		}
		return true;
	}

	public void Init(PartnerDialogueData data)
	{
		InitWrapper(() =>
		{
			mData = data;
			mInstant = data.CurrentNode != services.DialogueStartNodeId;
			((Component)mFastEndTypeWriterBtn).gameObject.SetActive(false);
			mDialogueNodes.Clear();
			mDialogueNodes.AddRange(data.DialogueNodes);
			((Component)mDialogueOption).gameObject.SetActive(false);
			InitPanelName();
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		services.DialogueUpdated += MainPlayer_OnNpcDialogueUpdate;
		((UnityEvent)mFastEndTypeWriterBtn.onClick).AddListener((UnityAction)OnFastEndTypeWriterClick);
		InitLoop();
		TryMoveToNextNode();
	}

	protected override void OnDisableAndInited()
	{
		if (services.MainPlayerAvailable)
		{
			services.DialogueUpdated -= MainPlayer_OnNpcDialogueUpdate;
		}
		((UnityEvent)mFastEndTypeWriterBtn.onClick).RemoveListener((UnityAction)OnFastEndTypeWriterClick);
		ResetInitState();
		base.OnDisableAndInited();
	}

	public override void ResetInitState()
	{
		mData = null;
		mDialogueNodes.Clear();
		base.ResetInitState();
	}

	private void MainPlayer_OnNpcDialogueUpdate(PartnerDialogueData obj)
	{
		if (obj.DialogueId == mData.DialogueId)
		{
			TryRefreshDialogues(instant: false);
		}
	}

	private void TryRefreshDialogues(bool instant)
	{
		if (mDelayCo != null)
		{
			((MonoBehaviour)this).StopCoroutine(mDelayCo);
			mDelayCo = null;
		}
		mDelayCo = ((MonoBehaviour)this).StartCoroutine(DelayRefreshDialogues(instant));
	}

	private IEnumerator DelayRefreshDialogues(bool instant)
	{
		yield return WaitForSeconds;
		mPlayingItem = null;
		mDelayCo = null;
		mInstant = instant;
		mDialogueNodes.Clear();
		mDialogueNodes.AddRange(mData.DialogueNodes);
		mLoop.totalCount = mDialogueNodes.Count;
		mLoop.RefillCellsFromEnd(0, alignStart: false, OnRefillCellsEnd);
	}

	private void OnRefillCellsEnd()
	{
		LoopScrollToBottom(1f);
		mInstant = true;
		TryMoveToNextNode();
	}

	private void InitLoop()
	{
		mPlayingItem = null;
		mLoop.dataSource = this;
		mLoop.totalCount = mDialogueNodes.Count;
		mLoop.RefillCells(0, 0f, () =>
		{
			LoopScrollToBottom(1f);
		});
	}

	private void TryMoveToNextNode()
	{
		if (mMoveNextCo != null)
		{
			((MonoBehaviour)this).StopCoroutine(mMoveNextCo);
			mMoveNextCo = null;
		}
		mMoveNextCo = ((MonoBehaviour)this).StartCoroutine(AutoMoveToNext());
	}

	private IEnumerator AutoMoveToNext()
	{
		int currentNode = mData.CurrentNode;
		PartnerConversationNode info = services.Configuration.Get(DialogueClassId, currentNode);
		if ((info.NextNodes?.Count ?? 0) > 0)
		{
			yield return (object)new WaitUntil((Func<bool>)(() => !IsPlaying));
			((Component)mFastEndTypeWriterBtn).gameObject.SetActive(false);
			if (NexNodesIsOption(info))
			{
				yield return (object)new WaitForSeconds(0.3f);
				mDialogueOption.Init(mData.DialogueId, info);
				((Component)mDialogueOption).gameObject.SetActive(true);
				LoopScrollToBottom(0.5f);
			}
			else
			{
				yield return TryAckNode(info.NextNodes[0]);
			}
			mMoveNextCo = null;
		}
	}

	private bool NexNodesIsOption(PartnerConversationNode info)
	{
		List<int> nextNodes = info.NextNodes;
		if (nextNodes != null)
		{
			return nextNodes.Count > 1;
		}
		return false;
	}

	private IEnumerator TryAckNode(int dialogueNodeId)
	{
		PartnerConversationNode npcDialogueInfo = services.Configuration.Get(DialogueClassId, dialogueNodeId);
		if (npcDialogueInfo == null)
		{
			Debug.LogError("NpcDialoguePanel: original Error branch.");
		}
		else
		{
			if (!services.IsNodeUnlocked(DialogueClassId, dialogueNodeId))
			{
				yield break;
			}
			PartnerDialogueAck yco = services.Acknowledge(mData.DialogueId, dialogueNodeId, false);
			yield return yco;
			if (!yco.Error)
			{
				Debug.Log("NpcDialoguePanel: original Info branch.");
			}
		}
	}

	private void InitPanelName()
	{
		if (!services.Npcs.TryGetValue(mData.NpcId, out var obj) || obj == null)
		{
			Debug.LogError("NpcDialoguePanel: original Error branch.");
		}
		else
		{
			string nameKey = services.GetNpcNameKey(obj.NpcClassId);
			mPanelTitle.SetKey(nameKey);
		}
	}

	protected void OnFastEndTypeWriterClick()
	{
		((Component)mFastEndTypeWriterBtn).gameObject.SetActive(false);
		if ((UnityEngine.Object)(object)mPlayingItem != (UnityEngine.Object)null)
		{
			mPlayingItem.FastEndTypeWriterEffect();
		}
	}

	private void LoopScrollToBottom(float time)
	{
		int index = mLoop.totalCount - 1;
		mLoop.ScrollToCellWithinTime(index, time);
	}
}

}
