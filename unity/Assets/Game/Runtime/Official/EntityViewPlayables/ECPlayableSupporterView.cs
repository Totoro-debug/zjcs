// Migrated official application source; see entity playable view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityViewPlayables
{
public class ECPlayableSupporterView : ECViewBase, IECPlayableSupporter
{
	[SerializeField]
	private bool mIsIgnoreFadeOut;

	[Tooltip("物件出现时播放的playable, 播放完毕后才会播放其他需要的playable")]
	[SerializeField]
	private GameObject mAppearPlayable;

	[Tooltip("物件播放出现playable后，持续播放")]
	[SerializeField]
	private GameObject mLoopPlayable;

	[SerializeField]
	private List<ECPlayableBaseHandlerData> mECPlayableHandlers;

	private Action mOnFadeInCompleted;

	private Action mOnStartFadeInOrOut;

	private IEntityFadeAnimationSupporter mEntityFadeAnimationSupporter;

	private MyPlayable mCurrentPlayable;

	private Action<MyPlayable> mOnLoopPlayableStop;

	private GameObject mSource;

	private bool mIsNew;

	private bool mIsStarted;

	public bool IsVisible { get; private set; }

	public ECViewEntityData EcEntity => mEntity;

	public bool IsNewEntity => mIsNew;

	public IClientVisibilitySupporter ClientVisibilitySupporter => mClientVisibilitySupporter;

	public IEntityFadeAnimationSupporter EntityFadeAnimationSupporter => mEntityFadeAnimationSupporter;

	GameObject IECPlayableSupporter.Source => mSource;

	public IMyPlayableManager MyPlayableManager { get; set; }

	public IServiceProvider ServiceProvider { get; set; }

	public event Action OnPlayableSupporterUpdated;

	public void StartPlayableHandler<T>() where T : ECPlayableBaseHandler
	{
		foreach (ECPlayableBaseHandlerData mECPlayableHandler in mECPlayableHandlers)
		{
			if (mECPlayableHandler.Handler is T)
			{
				mECPlayableHandler.Handler.Visible();
			}
		}
	}

	public void StopPlayableHandler<T>() where T : ECPlayableBaseHandler
	{
		foreach (ECPlayableBaseHandlerData mECPlayableHandler in mECPlayableHandlers)
		{
			if (mECPlayableHandler.Handler is T)
			{
				mECPlayableHandler.Handler.Invisible();
			}
		}
	}

	public T GetPlayableHandler<T>() where T : ECPlayableBaseHandler
	{
		foreach (ECPlayableBaseHandlerData mECPlayableHandler in mECPlayableHandlers)
		{
			if (mECPlayableHandler.Handler is T result)
			{
				return result;
			}
		}
		return null;
	}

	protected override void Load()
	{
		base.Load();
		mSource = ((Component)mOwner.transform.parent).gameObject;
		mIsNew = !mIsStatic;
		mIsStarted = false;
		IsVisible = false;
		mCurrentPlayable = null;
		mOnLoopPlayableStop = OnLoopPlayableStop;
		mEntityFadeAnimationSupporter = mEntityFadeAnimationBridge.EntityFadeAnimationSupporter;
		if (mOnFadeInCompleted == null)
		{
			mOnFadeInCompleted = OnFadeInCompleted;
		}
		if (mOnStartFadeInOrOut == null)
		{
			mOnStartFadeInOrOut = OnStartFadeInOrOut;
		}
		if (!mIsIgnoreFadeOut)
		{
			mEntityFadeAnimationSupporter.AddOnFadeInCompletedEvent(mOnFadeInCompleted);
			mEntityFadeAnimationSupporter.AddOnFadeOutStartEvent(mOnStartFadeInOrOut);
			mEntityFadeAnimationSupporter.AddOnFadeInStartEvent(mOnStartFadeInOrOut);
		}
		foreach (ECPlayableBaseHandlerData mECPlayableHandler in mECPlayableHandlers)
		{
			if (mECPlayableHandler == null || mECPlayableHandler.Handler == null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
			else
			{
				mECPlayableHandler.Handler.Init(this, ServiceProvider);
			}
		}
	}

	protected override void OnShow()
	{
		base.OnShow();
		DoStartPlayableHandlers();
	}

	private void OnLoopPlayableStop(MyPlayable loopPlayable)
	{
		if ((Object)(object)loopPlayable != (Object)null)
		{
			loopPlayable.OnStop -= mOnLoopPlayableStop;
		}
		if ((Object)(object)mCurrentPlayable == (Object)(object)loopPlayable)
		{
			mCurrentPlayable = null;
		}
		mIsNew = false;
		if (mIsStarted && CheckCanPlayable())
		{
			PlayLoopAndVisibleHandlers();
		}
	}

	protected override void OnHide()
	{
		DoStopPlayableHandler();
		base.OnHide();
	}

	protected override void Unload()
	{
		StopCurrentPlayable();
		mIsStarted = false;
		IsVisible = false;
		foreach (ECPlayableBaseHandlerData mECPlayableHandler in mECPlayableHandlers)
		{
			if (mECPlayableHandler != null && mECPlayableHandler.Handler != null)
			{
				mECPlayableHandler.Handler.Release();
			}
		}
		if (!mIsIgnoreFadeOut)
		{
			mEntityFadeAnimationSupporter.RemoveOnFadeInCompletedEvent(mOnFadeInCompleted);
			mEntityFadeAnimationSupporter.RemoveOnFadeOutStartEvent(mOnStartFadeInOrOut);
			mEntityFadeAnimationSupporter.RemoveOnFadeInStartEvent(mOnStartFadeInOrOut);
		}
		base.Unload();
	}

	private void DoStartPlayableHandlers()
	{
		if (mIsStarted || !CheckCanPlayable())
		{
			return;
		}
		mIsStarted = true;
		if (mIsNew && (Object)(object)mAppearPlayable != (Object)null)
		{
			StopCurrentPlayable();
			mCurrentPlayable = PlayPlayable(mAppearPlayable, mSource, mSource);
			if ((Object)(object)mCurrentPlayable != (Object)null)
			{
				mCurrentPlayable.OnStop += mOnLoopPlayableStop;
				return;
			}
			mIsNew = false;
		}
		PlayLoopAndVisibleHandlers();
	}

	private void PlayLoopAndVisibleHandlers()
	{
		StopCurrentPlayable();
		if ((Object)(object)mLoopPlayable != (Object)null)
		{
			mCurrentPlayable = PlayPlayable(mLoopPlayable, mSource, mSource);
		}
		foreach (ECPlayableBaseHandlerData mECPlayableHandler in mECPlayableHandlers)
		{
			if (mECPlayableHandler != null && mECPlayableHandler.Handler != null)
			{
				mECPlayableHandler.Handler.Visible();
			}
		}
		IsVisible = true;
		OnPlayableSupporterUpdated?.Invoke();
	}

	private void DoStopPlayableHandler()
	{
		if (!mIsStarted)
		{
			return;
		}
		mIsStarted = false;
		mIsNew = false;
		StopCurrentPlayable();
		foreach (ECPlayableBaseHandlerData mECPlayableHandler in mECPlayableHandlers)
		{
			if (mECPlayableHandler != null && mECPlayableHandler.Handler != null)
			{
				mECPlayableHandler.Handler.Invisible();
			}
		}
		IsVisible = false;
		OnPlayableSupporterUpdated?.Invoke();
	}

	private void StopCurrentPlayable()
	{
		if (!((Object)(object)mCurrentPlayable == (Object)null))
		{
			mCurrentPlayable.OnStop -= mOnLoopPlayableStop;
			mCurrentPlayable.Stop(immediate: true);
			mCurrentPlayable = null;
		}
	}

	protected void OnFadeInCompleted()
	{
		DoStartPlayableHandlers();
	}

	protected void OnStartFadeInOrOut()
	{
		DoStopPlayableHandler();
	}

	private bool CheckCanPlayable()
	{
		if (!mIsIgnoreFadeOut && !mEntityFadeAnimationSupporter.IsIn)
		{
			return false;
		}
		if (!mClientVisibilitySupporter.IsVisible)
		{
			return false;
		}
		return true;
	}

	T IECPlayableSupporter.GetECView<T>()
	{
		return GetView<T>();
	}

	T IECPlayableSupporter.GetComponent<T>()
	{
		return GetComponent<T>();
	}

	public MyPlayable PlayPlayable(GameObject playable, GameObject source, GameObject target)
	{
		if ((Object)(object)playable == (Object)null)
		{
			return null;
		}
		return MyPlayableManager.Play(playable, source, target, 1f, null, null, mClientVisibilitySupporter);
	}
}

}
