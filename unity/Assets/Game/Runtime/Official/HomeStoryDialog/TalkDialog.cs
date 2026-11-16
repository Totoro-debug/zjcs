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

public class TalkDialog : UIBase
{
	[Serializable]
	public class AutoPlayConfig
	{
		[Tooltip("是否自动播放")]
		public bool IsAutoPlay;

		[Tooltip("等待多久开始自动播放")]
		public float AutoPlayWaitTime;

		[Tooltip("自动播放间隔")]
		public float AutoPlayDura;
	}

	private class StoryTalkTextProcessor : IWorldTextProcessor
	{
		private string mPlayerName;

		public StoryTalkTextProcessor(string playerName)
		{
			mPlayerName = playerName;
		}

		public string Process(string text)
		{
			return StoryDialogLanguage.ReplacePlayerName(text, mPlayerName);
		}
	}

	[SerializeField]
	protected MultiLanguageText mTalkContentTxt;

	[SerializeField]
	protected Button mNextBtn;

	[Tooltip("显示每个字符所需要的时间")]
	[SerializeField]
	protected float mWordInterval;

	[SerializeField]
	protected MultiLanguageText mCharacterNameTxt;

	[SerializeField]
	protected GameObject mCharacterNameBack;

	[SerializeField]
	protected TalkDialogCharacterController mCharacterController;

	[SerializeField]
	protected Image mBlackBgImg;

	[SerializeField]
	protected Color mBlackBgColorDarkLess;

	[SerializeField]
	protected Color mBlackBgColorDark;

	[Tooltip("打字机配置")]
	[SerializeField]
	protected TMP_TypeWriterEffect mTextWriterEffect;

	[SerializeField]
	protected GameObject mDarkMask;

	public const float kWaitShowAnimEndDuration = 0.2f;

	private Dictionary<int, MyPlayable> mRolePlayables = new Dictionary<int, MyPlayable>();

	protected MyPlayable mBubblePlayable;

	protected int mLastRoleId;

	protected float mDuration;

	protected CancellationTokenSource mCancellationTokenSource;

	protected bool mInTypeWriting;

	protected bool mWaitClickForNext;

	private StoryTalkTextProcessor mTalkTextProcessor;

	protected IStoryDialogServices Services { get; private set; }

	private void Awake()
	{
		var scope = GetComponentInParent<HomeStoryDialogScope>();
		Services = scope != null ? scope.Services : null;
		if (Services == null) throw new InvalidOperationException("The original story dialog scope is required.");
		mCharacterController.Services = Services;
		mTalkContentTxt.Localizer = Services.Localize;
		mCharacterNameTxt.Localizer = Services.Localize;
		mTalkContentTxt.FontProvider = Services.Font;
		mCharacterNameTxt.FontProvider = Services.Font;
		((UnityEvent)mNextBtn.onClick).AddListener((UnityAction)OnNextBtnClick);
		PopupWindow.OnShowing += new Action<PopupWindow>(OnShowing);
		PopupWindow.OnShow += new Action<PopupWindow>(OnShowEnd);
		mTalkTextProcessor = new StoryTalkTextProcessor(Services.PlayerName);
	}

	protected override void OnDestroy()
	{
		PopupWindow popupWindow = PopupWindow;
		if ((Object)(object)popupWindow != (Object)null)
		{
			popupWindow.OnShowing -= new Action<PopupWindow>(OnShowing);
			popupWindow.OnShow -= new Action<PopupWindow>(OnShowEnd);
		}
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mInTypeWriting = false;
		mWaitClickForNext = false;
	}

	protected override void OnDisableAndInited()
	{
		mTalkContentTxt.SetKey("");
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
		foreach (KeyValuePair<int, MyPlayable> mRolePlayable in mRolePlayables)
		{
			if ((Object)null != (Object)(object)mRolePlayable.Value)
			{
				mRolePlayable.Value.Stop();
			}
		}
		mRolePlayables.Clear();
		StopTalkBubble();
		if (Services.HasStorySettings)
		{
			Services.PostVoice(Services.DialogVoiceStopEventKey, ((Component)this).gameObject);
		}
		Services.ReleasePlayableResources();
		base.OnDisableAndInited();
	}

	protected Color GetBgImageColor()
	{
		PopupWindow lastWindowWithIgnore = Services.LastWindowIgnoringTips();
		if (lastWindowWithIgnore != null)
		{
			if (lastWindowWithIgnore != PopupWindow) return mBlackBgColorDark;
			return ((Graphic)mBlackBgImg).color;
		}
		return Services.ActivePanelHasSceneGraphic() ? mBlackBgColorDark : mBlackBgColorDarkLess;
	}

	protected virtual void OnNextBtnClick()
	{
	}

	private void OnShowing(PopupWindow window)
	{
		((Component)mNextBtn).gameObject.SetActive(false);
	}

	private void OnShowEnd(PopupWindow window)
	{
		((Component)mNextBtn).gameObject.SetActive(true);
	}

	protected void PlayPlayable(int roleId, string playablePath)
	{
		DialogRoleInfo dialogRoleInfo = Services.GetDialogRole(roleId);
		if (mRolePlayables.TryGetValue(dialogRoleInfo.EntityClassId, out var value))
		{
			if ((Object)null != (Object)(object)value)
			{
				value.Stop();
			}
			mRolePlayables.Remove(dialogRoleInfo.EntityClassId);
		}
		MyPlayable myPlayable = Services.PlayWorldNpcPlayable(roleId, playablePath);
		if ((Object)null != (Object)(object)myPlayable)
		{
			mRolePlayables.Add(dialogRoleInfo.EntityClassId, myPlayable);
		}
	}

	protected void PlayTalkBubble(int roleId)
	{
		if (mLastRoleId != roleId)
		{
			StopTalkBubble();
			mBubblePlayable = Services.PlayWorldNpcPlayable(roleId, Services.TalkBubblePlayablePath);
			mLastRoleId = roleId;
		}
	}

	protected void StopTalkBubble()
	{
		if ((Object)null != (Object)(object)mBubblePlayable)
		{
			mBubblePlayable.Stop();
			mBubblePlayable = null;
		}
	}

	protected void RefreshTalkContent(int roleId, string portrait, string face, string voice, StoryTextKeyArgWrap text, Action<bool> onTalkComplete)
	{
		Services.GetDialogRole(roleId);
		mCharacterController.SetPortrait(roleId, portrait, face, () =>
		{
			RefreshRoleName(roleId);
			RefreshTalkText(onTalkComplete, text.Key, text.Args);
			RefreshTalkVoice(voice);
		});
	}

	private void RefreshRoleName(int roleId)
	{
		StoryTextKeyArgWrap roleName = StoryDialogLanguage.RoleName(roleId, Services.PlayerRoleId, Services.PlayerName);
		bool flag = Services.ContainsText(roleName.Key);
		mCharacterNameBack.SetActive(flag);
		if (flag)
		{
			mCharacterNameTxt.SetKey(roleName.Key, roleName.Args);
		}
	}

	private void RefreshTalkText(Action<bool> onTextDisplayFinish, string key, params object[] args)
	{
		mTalkContentTxt.SetKey(key, null, mTalkTextProcessor, args);
		mInTypeWriting = true;
		if (Mathf.Approximately(mDuration, 0f))
		{
			mTextWriterEffect.StartType(mWordInterval, onTextDisplayFinish);
		}
		else
		{
			mTextWriterEffect.StartTypeWithFixedDuration(mDuration, onTextDisplayFinish);
		}
	}

	private void RefreshTalkVoice(string voice)
	{
		Services.PostVoice(Services.DialogVoiceStopEventKey, ((Component)this).gameObject);
		if (!string.IsNullOrEmpty(voice))
		{
			Services.PostVoice(voice, ((Component)this).gameObject);
		}
	}
}

}
