using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
[RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
[RequireComponent(typeof(Animator))]
public sealed class PopupWindow : MonoBehaviour, IAnimationClipSource
{
	public enum EState
	{
		None,
		Showing,
		Show,
		Hiding,
		Hide
	}

	[SerializeField]
	private AnimationClip mShowClip;

	[SerializeField]
	private AnimationClip mHideClip;

	[SerializeField]
	private AnimationClip mLoopClip;

	private PlayableGraph mGraph;

	private PlayableOutput mPlayableOutput;

	private Playable mLastPlayable;

	public GAction<PopupWindow> OnShowing = new GAction<PopupWindow>();

	public GAction<PopupWindow> OnShow = new GAction<PopupWindow>();

	public GAction<PopupWindow> OnHiding = new GAction<PopupWindow>();

	public GAction<PopupWindow> OnHide = new GAction<PopupWindow>();

	public GAction OnCanvasSortingOrderChanged = new GAction();

	[SerializeField]
	private WindowMaskType mMaskType;

	[Header("属性")]
	[SerializeField]
	private WindowType mWindowType;

	[SerializeField]
	private bool mFullScreen;

	[SerializeField]
	private bool mCloseByClickMask;

	[SerializeField]
	private bool mAutoCloseWhenContainerDisable;

	[SerializeField]
	private bool mIsDuplicate = true;

	public GAction OnWindowDestory = new GAction();

	public GAction<GameObject> OnPauseEvent = new GAction<GameObject>();

	public GAction<GameObject> OnResumeEvent = new GAction<GameObject>();

	private Animator mAnimator;

	private Canvas mCanvase;

	private CanvasGroup mCanvasGroup;

	public WindowDependence WindowDependence = new WindowDependence();

	private float mShowStartTime;

	private Action mMaskClickAction;

	private const float kCloseWindowProtectedTime = 0.5f;

	private SerialDisposable mSerialDisposable = new SerialDisposable();

	private CancellationDisposable mDisableCancellation;

	private UniTaskPipeline mTaskPipeline = new UniTaskPipeline();

	private bool mIsDestroying;

	public IMap13PopupWindowHost WindowManager { private get; set; }


	public bool IsVisible => ((Component)this).gameObject.activeSelf;

	public WindowMaskType MaskType => mMaskType;

	public WindowType WindowType => mWindowType;

	public bool FullScreen => mFullScreen;

	public bool FullScreenDisplay
	{
		get
		{
			if (mFullScreen)
			{
				return true;
			}
			if (mMaskType == WindowMaskType.BlurMask)
			{
				return true;
			}
			return false;
		}
	}

	public bool CloseByClickMask
	{
		get
		{
			return mCloseByClickMask;
		}
		set
		{
			mCloseByClickMask = value;
		}
	}

	public EState State { get; private set; }

	public bool IsPause { get; private set; }

	public WindowLifeType WindowLifeType { get; set; }

	public string WindowNode { get; set; }

	public string WindowName { get; set; }

	public string WindowPath { get; set; }

	public bool AutoCloseWhenContainerDisable => mAutoCloseWhenContainerDisable;

	public bool IsDuplicate
	{
		get
		{
			if (WindowType != WindowType.Dialog)
			{
				return false;
			}
			return mIsDuplicate;
		}
	}

	public Animator Animator
	{
		get
		{
			if ((Object)(object)mAnimator == (Object)null)
			{
				mAnimator = ((Component)this).GetComponent<Animator>();
			}
			return mAnimator;
		}
	}

	public Canvas Canvas
	{
		get
		{
			if ((Object)(object)mCanvase == (Object)null)
			{
				mCanvase = ((Component)this).GetComponent<Canvas>();
			}
			return mCanvase;
		}
	}

	public CanvasGroup CanvasGroup
	{
		get
		{
			if ((Object)(object)mCanvasGroup == (Object)null)
			{
				mCanvasGroup = ((Component)this).GetComponent<CanvasGroup>();
			}
			return mCanvasGroup;
		}
	}

	private void Awake()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		mGraph = PlayableGraph.Create(((Object)((Component)this).gameObject).name);
		mPlayableOutput = (PlayableOutput)AnimationPlayableOutput.Create(mGraph, "AnimationClip", Animator);
		mGraph.SetTimeUpdateMode((DirectorUpdateMode)1);
	}

	private void OnDestroy()
	{
		mIsDestroying = true;
		if (mGraph.IsValid())
		{
			mGraph.Destroy();
		}
		OnWindowDestory.SafeInvoke();
		mSerialDisposable.Dispose();
	}

	private void OnEnable()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected Obj, but got Unknown
		mGraph.Play();
		if (mDisableCancellation == null)
		{
			mDisableCancellation = new CancellationDisposable();
		}
	}

	private void OnDisable()
	{
		mGraph.Stop();
		mDisableCancellation.Dispose();
		mDisableCancellation = null;
	}

	public async UniTask Show(bool instant = false)
    {
        var cancellationDisposable = new CancellationDisposable();
        mSerialDisposable.Disposable = cancellationDisposable;
        await mTaskPipeline.StartTask(() => InternalShow(cancellationDisposable.Token));
        async UniTask InternalShow(CancellationToken cancellationToken)
        {
            if (IsVisible) { UnityEngine.Debug.Log(gameObject.name + " has activated!", gameObject); return; }
            if (State == EState.Showing || State == EState.Show) return;
            mShowStartTime = Time.time;
            State = EState.Showing;
            gameObject.SetActive(true);
            OnShowing.SafeInvoke(this);
            WindowManager.OnWindowsOpening.SafeInvoke(gameObject);
            try { await PlayPlayable(mShowClip, instant, true, cancellationToken); }
            catch (Exception) { throw; }
            finally
            {
                if (!mIsDestroying)
                {
                    State = EState.Show;
                    OnShow.SafeInvoke(this);
                    WindowManager.OnWindowOpened.SafeInvoke(gameObject);
                    PlayPlayable(mLoopClip, false);
                }
            }
        }
    }

	public async UniTask Hide(bool instant = false)
    {
        var cancellationDisposable = new CancellationDisposable();
        mSerialDisposable.Disposable = cancellationDisposable;
        if (mDisableCancellation == null) mDisableCancellation = new CancellationDisposable();
        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationDisposable.Token, mDisableCancellation.Token);
        await mTaskPipeline.StartTask(() => InternalHide(cancellationTokenSource.Token));
        async UniTask InternalHide(CancellationToken cancellationToken)
        {
            if (!IsVisible) { UnityEngine.Debug.Log(gameObject.name + " has unactivated!", gameObject); return; }
            if (State == EState.Hiding || State == EState.Hide) return;
            State = EState.Hiding;
            OnHiding.SafeInvoke(this);
            WindowManager.OnWindowClosing.SafeInvoke(gameObject);
            try { await PlayPlayable(mHideClip, instant, false, cancellationToken); }
            catch (Exception) { throw; }
            finally
            {
                if (!mIsDestroying)
                {
                    State = EState.Hide;
                    gameObject.SetActive(false);
                    OnHide.SafeInvoke(this);
                    WindowManager.OnWindowClosed.SafeInvoke(gameObject);
                }
            }
        }
    }

	private async UniTask PlayPlayable(AnimationClip clip, bool instant, bool supportPause, CancellationToken cancellationToken)
    {
        if (clip == null) return;
        PlayPlayable(clip, instant);
        if (instant) return;
        float totalSeconds = clip.length;
        while (totalSeconds > 0f)
        {
            if (cancellationToken.IsCancellationRequested) return;
            await UniTask.NextFrame(cancellationToken, true);
            if (!(IsPause & supportPause)) totalSeconds -= Time.deltaTime;
        }
        await UniTask.Yield((PlayerLoopTiming)8, cancellationToken, true);
    }

	private void PlayPlayable(AnimationClip clip, bool instant)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)clip == (Object)null))
		{
			Playable sourcePlayable = PlayableOutputExtensions.GetSourcePlayable<PlayableOutput>(mPlayableOutput);
			if (PlayableExtensions.IsValid<Playable>(sourcePlayable))
			{
				PlayableExtensions.Destroy<Playable>(sourcePlayable);
			}
			AnimationClipPlayable val = AnimationClipPlayable.Create(mGraph, clip);
			PlayableOutputExtensions.SetSourcePlayable<PlayableOutput, AnimationClipPlayable>(mPlayableOutput, val);
			PlayableExtensions.SetTime<AnimationClipPlayable>(val, (double)(instant ? clip.length : 0f));
		}
	}

	public void SetCanvasEnable(bool enable)
	{
		CanvasGroup.alpha = (enable ? 1 : 0);
		CanvasGroup.interactable = enable;
		CanvasGroup.blocksRaycasts = enable;
	}

	public void OnMaskClick()
	{
		if (CanCloseByMask())
		{
			if (mMaskClickAction != null)
			{
				mMaskClickAction();
			}
			else
			{
				WindowManager.Close(((Component)this).gameObject, instant: false);
			}
		}
	}

	public bool Pause()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (IsPause)
		{
			return false;
		}
		IsPause = true;
		mLastPlayable = PlayableOutputExtensions.GetSourcePlayable<PlayableOutput>(mPlayableOutput);
		PlayableOutputExtensions.SetSourcePlayable<PlayableOutput, Playable>(mPlayableOutput, Playable.Null);
		SetCanvasEnable(enable: false);
		OnPauseEvent.SafeInvoke(((Component)this).gameObject);
		return true;
	}

	public bool Resume()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPause)
		{
			return false;
		}
		IsPause = false;
		SetCanvasEnable(enable: true);
		if (PlayableExtensions.IsValid<Playable>(mLastPlayable))
		{
			PlayableOutputExtensions.SetSourcePlayable<PlayableOutput, Playable>(mPlayableOutput, mLastPlayable);
		}
		OnResumeEvent.SafeInvoke(((Component)this).gameObject);
		return true;
	}

	public void GetAnimationClips(List<AnimationClip> results)
	{
		if ((Object)(object)mShowClip != (Object)null)
		{
			results.Add(mShowClip);
		}
		if ((Object)(object)mHideClip != (Object)null)
		{
			results.Add(mHideClip);
		}
		if ((Object)(object)mLoopClip != (Object)null)
		{
			results.Add(mLoopClip);
		}
	}

	public void SetHideAnimClip(AnimationClip clip)
	{
		mHideClip = clip;
	}

	public void SetMaskClickAction(Action action)
	{
		mMaskClickAction = action;
	}

	private bool CanCloseByMask()
	{
		if (!CloseByClickMask)
		{
			return false;
		}
		if (Time.time - mShowStartTime <= 0.5f)
		{
			return false;
		}
		return true;
	}

	public void SetCanvasSortingOrder(int order)
	{
		Canvas.overrideSorting = true;
		Canvas.sortingOrder = order;
		OnCanvasSortingOrderChanged.Invoke();
	}

	public PopupWindow()
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected Obj, but got Unknown
	}
}

}
