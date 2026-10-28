// Migrated original source; see map14-story-bubble-runtime source receipt.
using Object = UnityEngine.Object;
using UIBase = Zjcs.Official.ProfessionUI.UIBase;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using MyEntity = Zjcs.Official.StoryBubbles.BubbleEntityBinding;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.StoryBubbles
{
public class BubbleDialog : UIBase
{
	[SerializeField]
	private AttachToObjUI mAttachToObj;

	[SerializeField]
	private RectTransform mMoveRootTran;

	[SerializeField]
	private MultiLanguageText mDialogAngryText;

	[SerializeField]
	private MultiLanguageText mDialogNormalText;

	[SerializeField]
	private GameObject mDialogAngryNode;

	[SerializeField]
	private GameObject mDialogNormalNode;

	[SerializeField]
	private Button mNextBtn;

	[Tooltip("显示每个字符所需要的时间")]
	[SerializeField]
	private float mWordInterval = 0.05f;

	[SerializeField]
	private TMP_TypeWriterEffect mNormalWriteEffect;

	[SerializeField]
	private TMP_TypeWriterEffect mAngryWriteEffect;

	[NonSerialized]
	public bool TalkFinish;

	private int mDialogId;

	private bool mAutoClose;

	private bool mInstant;

	private bool mIsClickPlay;

	private Queue<BubbleDialogInfo> mTextQueue = new Queue<BubbleDialogInfo>();

	private Dictionary<int, MyEntity> mDialogEntitys = new Dictionary<int, MyEntity>();

	private Coroutine mCoroutine;

	private WaitForSeconds mWaitForSeconds;

	private float mDuration;

	private IBubbleServices Services => BubbleServices.Current;

	public void Init(int dialogId, float playDeltaTime, bool isAutoClose, bool isClickPlay, bool instant = false)
	{
		InitWrapper(() =>
		{
			DoInit(dialogId, playDeltaTime, isAutoClose, isClickPlay, instant);
		});
	}

	protected void DoInit(int dialogId, float playDeltaTime, bool autoClose, bool isClickPlay, bool instant = false)
	{
		mDialogId = dialogId;
		mAutoClose = autoClose;
		mIsClickPlay = isClickPlay;
		mInstant = instant;
		mDuration = 0f;
		if (isClickPlay)
		{
			((UnityEvent)mNextBtn.onClick).AddListener((UnityAction)DoNext);
		}
		else
		{
			mWaitForSeconds = new WaitForSeconds(playDeltaTime);
		}
		Services.GetDialogInfosById(dialogId).ForEach((BubbleDialogInfo x) =>
		{
			mDialogEntitys.TryAdd(x.EntityClassId, (MyEntity)null);
		});
		foreach (MyEntity value3 in Services.ServerEntities)
		{
			if (mDialogEntitys.TryGetValue(value3.ClassId, out var value) && (Object)(object)value == (Object)null)
			{
				mDialogEntitys[value3.ClassId] = value3;
			}
		}
		foreach (MyEntity value4 in Services.ClientEntities)
		{
			if (mDialogEntitys.TryGetValue(value4.ClassId, out var value2) && (Object)(object)value2 == (Object)null)
			{
				mDialogEntitys[value4.ClassId] = value4;
			}
		}
	}

	public void Play(int dialogId, int order, MyEntity entity, float duration = 0f, bool instant = false)
	{
		mInstant = instant;
		mDuration = duration;
		BubbleDialogInfo bubbleDialogInfo = Services.Get(dialogId, order);
		if (bubbleDialogInfo != null)
		{
			PlayDialog(bubbleDialogInfo);
			AttachToObj(bubbleDialogInfo, entity);
		}
	}

	public void Play(int dialogId)
	{
		List<BubbleDialogInfo> dialogInfosById = Services.GetDialogInfosById(dialogId);
		mTextQueue.Clear();
		foreach (BubbleDialogInfo item in dialogInfosById)
		{
			mTextQueue.Enqueue(item);
		}
		TalkFinish = false;
		DoNext();
	}

	private void PlayDialog(BubbleDialogInfo info)
	{
		string text = Services.Localize("bubble_dialog_" + info.Id + "_" + info.Order);
		SetContentAndState(text, info.IsAngry);
	}

	protected virtual void DoTalkFinish()
	{
		mDialogId = 0;
		mTextQueue.Clear();
		mDialogEntitys.Clear();
		if (mCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mCoroutine);
		}
		if (mIsClickPlay)
		{
			((UnityEvent)mNextBtn.onClick).RemoveListener((UnityAction)DoNext);
		}
		if (mAutoClose)
		{
			Services.Close(((Component)this).gameObject);
		}
		TalkFinish = true;
	}

	private void DoNext()
	{
		if (IsQueueEmpty())
		{
			DoTalkFinish();
			return;
		}
		SetContentAndState(null);
		BubbleDialogInfo info = mTextQueue.Dequeue();
		PlayDialog(info);
		AttachToObj(info);
		if (!mIsClickPlay)
		{
			mCoroutine = ((MonoBehaviour)this).StartCoroutine(WaitPlayTalk());
		}
	}

	private IEnumerator WaitPlayTalk()
	{
		yield return mWaitForSeconds;
		DoNext();
	}

	private void AttachToObj(BubbleDialogInfo info)
	{
		mAttachToObj.myOffset = (Vector3)info.OffsetToRoot;
		MyEntity value;
		if (info.IsSelf)
		{
			mAttachToObj.AttachObject = Services.SelfObject;
		}
		else if (mDialogEntitys.TryGetValue(info.EntityClassId, out value) && (Object)(object)value != (Object)null)
		{
			mAttachToObj.AttachObject = ((Component)value).gameObject;
		}
	}

	private void AttachToObj(BubbleDialogInfo info, MyEntity myEnt)
	{
		mAttachToObj.myOffset = (Vector3)info.OffsetToRoot;
		mAttachToObj.AttachObject = ((Component)myEnt).gameObject;
	}

	protected bool IsQueueEmpty()
	{
		return mTextQueue.Count == 0;
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (mDialogId > 0)
		{
			if (Services.TryGetHomeUI(out var home)) home.SetActive(false);
			if (Services.TryGetWorldPanel(out var world)) world.HideUI();
			if (Services.HasBottomBar) Services.HideBottomBar(this);
			if (Services.HasPlayerInfo) Services.HidePlayerInfo(this);
			Play(mDialogId);
		}
	}

	protected override void OnDisableAndInited()
	{
		if (Services.TryGetHomeUI(out var home)) home.SetActive(true);
		if (Services.TryGetWorldPanel(out var world)) world.ShowUI();
		if (Services.HasBottomBar) Services.TryShowBottomBar(this);
		if (Services.HasPlayerInfo) Services.TryShowPlayerInfo(this);
		mAttachToObj.AttachObject = null;
		mCoroutine = null;
		mWaitForSeconds = null;
		SetContentAndState(null);
		base.OnDisableAndInited();
	}

	public void SetContentAndState(string content, bool angryState = false)
	{
		if (string.IsNullOrEmpty(content))
		{
			mDialogAngryNode.SetActive(false);
			mDialogNormalNode.SetActive(false);
			return;
		}
		mDialogAngryNode.SetActive(angryState);
		mDialogNormalNode.SetActive(!angryState);
		if (angryState)
		{
			mDialogAngryText.Format(content);
		}
		else
		{
			mDialogNormalText.Format(content);
		}
		if (!mInstant)
		{
			TMP_TypeWriterEffect tMP_TypeWriterEffect = (angryState ? mAngryWriteEffect : mNormalWriteEffect);
			if ((Object)(object)tMP_TypeWriterEffect != (Object)null)
			{
				if (mDuration > 0f)
				{
					tMP_TypeWriterEffect.StartTypeWithFixedDuration(mDuration, OnPlaySingleTalkFinish);
				}
				else
				{
					tMP_TypeWriterEffect.StartType(mWordInterval, OnPlaySingleTalkFinish);
				}
			}
		}
		if (!string.IsNullOrEmpty(content) && ((Component)this).gameObject.activeSelf)
		{
			ForceUpdateLayout(angryState ? mDialogAngryNode : mDialogNormalNode);
		}
	}

	private void OnPlaySingleTalkFinish(bool isBreakOff)
	{
	}

	private void ForceUpdateLayout(GameObject go)
	{
		RectTransform component = go.GetComponent<RectTransform>();
		component.ForceUpdateRectTransforms();
		LayoutRebuilder.ForceRebuildLayoutImmediate(component);
	}
}

}
