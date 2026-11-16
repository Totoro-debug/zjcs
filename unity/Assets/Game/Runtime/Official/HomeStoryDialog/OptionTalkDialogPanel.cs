// Adapted from the selected original client; see home-story-dialog source manifest.
namespace Zjcs.Official.HomeStoryDialog
{
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeStoryUI;
using Zjcs.Official.HomeStoryCharacter;
using Zjcs.Official.StoryBubbles;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using DialogInfo = StoryDialogInfo;
using DialogRoleInfo = StoryDialogRoleInfo;

public class OptionTalkDialogPanel : TalkDialog
{
	[SerializeField]
	private OptionSlot OptionSlot;

	[SerializeField]
	private GameObject mClickMask;

	private string mStoryName;

	private int mDialogId;

	private bool mIsTalkFinish;

	private Queue<DialogInfo> mTextQueue = new Queue<DialogInfo>();

	private Dictionary<int, string> mDialogSubFormatedText;

	private List<DialogOptionDataBase> mOptions;

	private Action<int> mOnOptionClicked;

	private Action<int> mOnTalkFinished;

	private bool mAutoClose;

	private bool mShowOptionsAtBegin;

	private AutoPlayConfig mAutoPlayConfig;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (mDialogId >= 0)
		{
			Play();
		}
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
	}

	public void Init(string storyName, int dialogId, bool immediately, bool autoClose, bool showOptionsAtBegin, List<DialogOptionDataBase> options, Action<int> onOptionClicked, Action<int> onTalkFinished = null, Dictionary<int, string> dialogParams = null, bool isOpenBlackBg = true, AutoPlayConfig autoPlayConfig = null)
	{
		InitWrapper(() =>
		{
			DoInit(storyName, dialogId, dialogParams, autoClose, showOptionsAtBegin, options, onOptionClicked, onTalkFinished, isOpenBlackBg, autoPlayConfig);
			InitTextQueue(dialogId, immediately);
		});
	}

	public void TimelineInit(string storyName, int dialogId, int order, float playDuration)
	{
		InitWrapper(() =>
		{
			mDuration = playDuration;
			mDarkMask.SetActive(false);
			DoInit(storyName, dialogId, null, autoClose: false, isOpenBlackBg: true);
			InitTextQueue(dialogId, order);
		});
	}

	public void SetOptions(List<DialogOptionDataBase> options, Action<int> onOptionClicked)
	{
		mOptions = options;
		mOnOptionClicked = onOptionClicked;
		ShowOptions();
	}

	private void DoInit(string storyName, int dialogId, Dictionary<int, string> dialogParams, bool autoClose, bool showOptionsAtBegin, List<DialogOptionDataBase> options, Action<int> onOptionClicked, Action<int> onTalkFinished, bool isOpenBlackBg, AutoPlayConfig autoPlayConfig = null)
	{
		DoInit(storyName, dialogId, dialogParams, autoClose, isOpenBlackBg, autoPlayConfig);
		mShowOptionsAtBegin = showOptionsAtBegin;
		mOptions = options;
		mOnTalkFinished = onTalkFinished;
		mOnOptionClicked = onOptionClicked;
		mClickMask.SetActive(false);
		mDarkMask.SetActive(false);
		foreach (OptionBaseItem item in OptionSlot.OptionItems)
			if (item is OptionTalkDialogItem option) option.Services = Services;
		OptionSlot.Reset();
	}

	protected void DoInit(string storyName, int dialogId, Dictionary<int, string> formatedText, bool autoClose, bool isOpenBlackBg, AutoPlayConfig autoPlayConfig = null)
	{
		mStoryName = storyName;
		mDialogId = dialogId;
		mDialogSubFormatedText = formatedText;
		mAutoClose = autoClose;
		mAutoPlayConfig = autoPlayConfig;
		((Component)mBlackBgImg).gameObject.SetActive(isOpenBlackBg);
		if (isOpenBlackBg)
		{
			((Graphic)mBlackBgImg).color = GetBgImageColor();
		}
	}

	private void InitTextQueue(int dialogId, bool isImmediately)
	{
		List<DialogInfo> dialogInfosById = Services.GetDialogInfos(mStoryName, dialogId);
		if (dialogInfosById == null)
		{
			Debug.LogError("OptionTalkDialogPanel: original error branch.");
		}
		mTextQueue.Clear();
		if (dialogInfosById == null)
		{
			return;
		}
		if (isImmediately)
		{
			mTextQueue.Enqueue(dialogInfosById[dialogInfosById.Count - 1]);
			return;
		}
		foreach (DialogInfo item in dialogInfosById)
		{
			mTextQueue.Enqueue(item);
		}
	}

	private void InitTextQueue(int dialogId, int order)
	{
		mTextQueue.Clear();
		if (order >= 0)
		{
			List<DialogInfo> dialogInfosById = Services.GetDialogInfos(mStoryName, dialogId);
			if (dialogInfosById != null && dialogInfosById.Count > order)
			{
				mTextQueue.Enqueue(dialogInfosById[order]);
			}
		}
	}

	private void Play()
	{
		mIsTalkFinish = false;
		PlayNext();
		if (!mIsTalkFinish && mAutoPlayConfig != null && mAutoPlayConfig.IsAutoPlay)
		{
			mCancellationTokenSource = new CancellationTokenSource();
			UniTaskExtensions.Forget(WaitAutoPlayDialog(mCancellationTokenSource.Token));
		}
		if (mShowOptionsAtBegin)
		{
			mTextWriterEffect.FastEndType();
			ShowOptions();
		}
	}

	private async UniTask WaitAutoPlayDialog(CancellationToken cancellationToken)
	{
		await UniTask.WaitForSeconds(mAutoPlayConfig.AutoPlayWaitTime, false, (PlayerLoopTiming)8, cancellationToken, false);
		while (!mIsTalkFinish) await AutoPlaying(cancellationToken);
	}

	private async UniTask AutoPlaying(CancellationToken cancellationToken)
	{
		PlayNext();
		await UniTask.WaitForSeconds(mAutoPlayConfig.AutoPlayDura, false, (PlayerLoopTiming)8, cancellationToken, false);
	}

	private void PlayNext()
	{
		if (IsQueueEmpty())
		{
			DoTalkFinish();
			return;
		}
		mTalkContentTxt.SetKey("");
		PlayContent(mTextQueue.Dequeue());
	}

	private void OnSingleTalkFinished(bool isBreakOff)
	{
		mInTypeWriting = false;
		mWaitClickForNext = true;
	}

	private void DoTalkFinish()
	{
		if (!ShowOptions())
		{
			ExitTalkDialog();
		}
	}

	private bool ShowOptions()
	{
		if (!IsQueueEmpty())
		{
			return false;
		}
		if (mOptions == null || mOptions.Count == 0)
		{
			mClickMask.SetActive(false);
			return false;
		}
		mClickMask.SetActive(true);
		((Component)OptionSlot).gameObject.SetActive(true);
		OptionSlot.ShowOptions(mOptions, OnOptionClicked);
		mDarkMask.SetActive(true);
		return true;
	}

	private void OnOptionClicked(int index)
	{
		((Component)OptionSlot).gameObject.SetActive(false);
		mClickMask.SetActive(false);
		mDarkMask.SetActive(false);
		mOnOptionClicked?.Invoke(index);
		ExitTalkDialog();
	}

	private void ExitTalkDialog()
	{
		if (mOptions != null && mOptions.Count > 0)
		{
			mClickMask.SetActive(true);
			((Component)OptionSlot).gameObject.SetActive(false);
			mDarkMask.SetActive(false);
		}
		if (!mIsTalkFinish)
		{
			if (mCancellationTokenSource != null)
			{
				mCancellationTokenSource.Cancel();
				mCancellationTokenSource.Dispose();
				mCancellationTokenSource = null;
			}
			mTextQueue.Clear();
			if (mAutoClose)
			{
				Services.CloseWindow(((Component)this).gameObject);
			}
			mIsTalkFinish = true;
			int obj = mDialogId;
			mDialogId = -1;
			mOnTalkFinished?.Invoke(obj);
			mOnTalkFinished = null;
		}
	}

	protected override void OnNextBtnClick()
	{
		if (mInTypeWriting)
		{
			mTextWriterEffect.StopType();
		}
		else if (mWaitClickForNext)
		{
			mWaitClickForNext = false;
			PlayNext();
		}
	}

	private void PlayContent(DialogInfo info)
	{
		DialogRoleInfo dialogRoleInfo = Services.GetDialogRole(info.RoleId);
		string talkContent = GetTalkContent(info);
		RefreshTalkContent(info.RoleId, dialogRoleInfo.PortraitPath, info.RoleFaceType, GetVoiceEventKey(info), new StoryTextKeyArgWrap("{0}", talkContent), OnSingleTalkFinished);
		PlayPlayable(info.RoleId, info.PlayablePath);
		PlayTalkBubble(info.RoleId);
	}

	private string GetVoiceEventKey(DialogInfo info)
	{
		if (mShowOptionsAtBegin)
		{
			return string.Empty;
		}
		return StoryDialogLanguage.VoiceEventKey(info);
	}

	private string GetTalkContent(DialogInfo info)
	{
		if (mDialogSubFormatedText != null && mDialogSubFormatedText.TryGetValue(info.Order, out var value))
		{
			return value;
		}
		return Services.Localize(StoryDialogLanguage.DialogTextKey(mStoryName, info.Id, info.Order));
	}

	protected bool IsQueueEmpty()
	{
		return mTextQueue.Count == 0;
	}
}

}
