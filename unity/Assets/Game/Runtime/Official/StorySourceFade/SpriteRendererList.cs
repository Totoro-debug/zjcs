// Migrated official application source; see source-actor fading receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StorySourceFade
{
public class SpriteRendererList : MonoBehaviour
{
	public enum State
	{
		FadeIned,
		FadeIning,
		FadeOuted,
		FadeOuting
	}

	private static readonly Color mDefaultColor = Color.white;

	public List<SpriteRenderer> mySpriteList;

	[SerializeField]
	private List<FordingSpriteRenderer> mFordingSpriteList;

	public List<GameObject> FadeInCompleteActiveGO;

	[SerializeField]
	private List<CustomRendererFade> mCustomFadeList;

	[SerializeField]
	private NativeActorFadeRenderer mSkeletonRenderer;

	public float myFadeinTime = 1f;

	public float myFadeoutTime = 0.5f;

	public float AheadInTime;

	private bool mIsIn = true;

	private float mAsyncFadeInTaskDurationS = -1f;

	private float mAlpha = 1f;

	private AttachAgent mAttachAgent;

	private Color mColor = mDefaultColor;

	private State mCurrentState;

	private bool mHasEvent;

	private bool mIsFadeInGo = true;

	public NativeActorFadeRenderer SkeletonRenderer
	{
		get
		{
			return mSkeletonRenderer;
		}
		set
		{
			mSkeletonRenderer = value;
			UpdateSkeletonRendererAlpha(mSkeletonRenderer);
			UpdateSkeletonRendererColor();
		}
	}

	public bool IsIn
	{
		get
		{
			return mIsIn;
		}
		protected set
		{
			mIsIn = value;
		}
	}

	public float Alpha
	{
		get
		{
			return mAlpha;
		}
		set
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			mAlpha = value;
			mColor.a = value;
			foreach (SpriteRenderer mySprite in mySpriteList)
			{
				if ((Object)(object)mySprite != (Object)null)
				{
					Color color = mySprite.color;
					mySprite.color = new Color(color.r, color.g, color.b, mAlpha);
				}
			}
			foreach (FordingSpriteRenderer mFordingSprite in mFordingSpriteList)
			{
				if ((Object)(object)mFordingSprite != (Object)null)
				{
					Color color2 = mFordingSprite.Color;
					mFordingSprite.Color = new Color(color2.r, color2.g, color2.b, mAlpha);
					mFordingSprite.Refresh();
				}
			}
			UpdateSkeletonRendererAlpha(mSkeletonRenderer);
			if (mCustomFadeList == null)
			{
				return;
			}
			foreach (CustomRendererFade mCustomFade in mCustomFadeList)
			{
				mCustomFade.SetAlpha(mAlpha);
			}
		}
	}

	public event Action OnFadeOutStart;

	public event Action OnFadeOutCompleted;

	public event Action OnFadeInStart;

	public event Action OnFadeInCompleted;

	private void OnEnable()
	{
		if (!(mAsyncFadeInTaskDurationS <= 0f))
		{
			FadeIn(mAsyncFadeInTaskDurationS);
			mAsyncFadeInTaskDurationS = -1f;
		}
	}

	private void OnDisable()
	{
		((MonoBehaviour)this).StopAllCoroutines();
		if (mCurrentState == State.FadeIning)
		{
			ImmediateFadeIn(mHasEvent);
		}
		else if (mCurrentState == State.FadeOuting)
		{
			ImmediateFadeOut(mHasEvent);
		}
		ResetFadeState();
		mAttachAgent = null;
	}

	private void ResetFadeState()
	{
		mAsyncFadeInTaskDurationS = -1f;
		IsIn = true;
		FadeInCompleteChangeGO(isActive: true);
		Alpha = 1f;
		mCurrentState = State.FadeIned;
	}

	public void AddSprites(List<SpriteRenderer> sprites)
	{
		mySpriteList.AddRange(sprites);
		Alpha = mAlpha;
	}

	public void RemoveSprites(List<SpriteRenderer> sprites)
	{
		foreach (SpriteRenderer sprite in sprites)
		{
			mySpriteList.Remove(sprite);
		}
	}

	public void SetColor(Color color, bool hasAlphe = true)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		bool flag = mDefaultColor != mColor;
		if (hasAlphe)
		{
			mAlpha = mColor.a;
		}
		mColor = new Color(color.r, color.g, color.b, mAlpha);
		foreach (SpriteRenderer mySprite in mySpriteList)
		{
			if ((Object)(object)mySprite != (Object)null && (flag || !(mySprite.color != mDefaultColor)))
			{
				mySprite.color = mColor;
			}
		}
		foreach (FordingSpriteRenderer mFordingSprite in mFordingSpriteList)
		{
			if ((Object)(object)mFordingSprite != (Object)null && (flag || !(mFordingSprite.Color != mDefaultColor)))
			{
				mFordingSprite.Color = mColor;
				mFordingSprite.Refresh();
			}
		}
		UpdateSkeletonRendererColor();
	}

	public void FadeIn(float durationS = -1f, bool hasEvent = true)
	{
		mHasEvent = hasEvent;
		float fadeTime = ((durationS > 0f) ? durationS : myFadeinTime);
		if (!((Behaviour)this).isActiveAndEnabled)
		{
			mAsyncFadeInTaskDurationS = fadeTime;
			return;
		}
		if (FloatUtils.IsZero(durationS))
		{
			ImmediateFadeIn(mHasEvent);
			return;
		}
		IsIn = false;
		if (mHasEvent)
		{
			OnFadeInStart?.Invoke();
		}
		((MonoBehaviour)this).StopAllCoroutines();
		mCurrentState = State.FadeIning;
		FadeInCompleteChangeGO(isActive: false);
		((MonoBehaviour)this).StartCoroutine(DoFadeIn(0f, 1f, fadeTime, mHasEvent));
	}

	public void ImmediateFadeIn(bool hasEvent = true)
	{
		mAsyncFadeInTaskDurationS = -1f;
		if (mCurrentState != State.FadeIned)
		{
			IsIn = true;
			mHasEvent = hasEvent;
			if (mHasEvent)
			{
				OnFadeInStart?.Invoke();
			}
			FadeInCompleteChangeGO(isActive: true);
			((MonoBehaviour)this).StopAllCoroutines();
			Alpha = 1f;
			if (mHasEvent)
			{
				OnFadeInCompleted?.Invoke();
			}
			mCurrentState = State.FadeIned;
		}
	}

	private IEnumerator DoFadeIn(float start, float end, float fadeTime, bool hasEvent = true)
	{
		if (start > end)
		{
			yield break;
		}
		float time = 0f;
		float inTime = fadeTime - AheadInTime;
		do
		{
			time += Time.deltaTime;
			Alpha = Mathf.Lerp(start, end, time / fadeTime);
			if (!IsIn && time >= inTime)
			{
				CompleteFadeIn(hasEvent);
			}
			yield return 0;
		}
		while (time < fadeTime);
		FadeInCompleteChangeGO(isActive: true);
		Alpha = 1f;
		if (!IsIn)
		{
			CompleteFadeIn(hasEvent);
		}
		mCurrentState = State.FadeIned;
	}

	public void FadeOut(float durationS = -1f, bool hasEvent = true)
	{
		mHasEvent = hasEvent;
		mAsyncFadeInTaskDurationS = -1f;
		if (!((Behaviour)this).isActiveAndEnabled)
		{
			ImmediateFadeOut(mHasEvent);
			return;
		}
		if (FloatUtils.IsZero(durationS))
		{
			ImmediateFadeOut(mHasEvent);
			return;
		}
		IsIn = false;
		FadeInCompleteChangeGO(isActive: false);
		((MonoBehaviour)this).StopAllCoroutines();
		if (mHasEvent)
		{
			OnFadeOutStart?.Invoke();
		}
		mCurrentState = State.FadeOuting;
		((MonoBehaviour)this).StartCoroutine(DoFadeOut(1f, 0f, (durationS > 0f) ? durationS : myFadeoutTime, mHasEvent));
	}

	public bool ImmediateFadeOut(bool hasEvent = true)
	{
		mAsyncFadeInTaskDurationS = -1f;
		if (mCurrentState == State.FadeOuted)
		{
			return false;
		}
		IsIn = false;
		mHasEvent = hasEvent;
		if (mHasEvent)
		{
			OnFadeOutStart?.Invoke();
		}
		FadeInCompleteChangeGO(isActive: false);
		((MonoBehaviour)this).StopAllCoroutines();
		Alpha = 0f;
		if (mHasEvent)
		{
			OnFadeOutCompleted?.Invoke();
		}
		mCurrentState = State.FadeOuted;
		return true;
	}

	private IEnumerator DoFadeOut(float start, float end, float fadeTime, bool hasEvent = true)
	{
		if (!(start < end))
		{
			float time = 0f;
			do
			{
				time += Time.deltaTime;
				Alpha = Mathf.Lerp(start, end, time / fadeTime);
				yield return 0;
			}
			while (time < fadeTime);
			if (hasEvent)
			{
				OnFadeOutCompleted?.Invoke();
			}
			mCurrentState = State.FadeOuted;
		}
	}

	private void CompleteFadeIn(bool hasEvent = true)
	{
		IsIn = true;
		if (hasEvent)
		{
			OnFadeInCompleted?.Invoke();
		}
	}

	private void FadeInCompleteChangeGO(bool isActive)
	{
		if (isActive == mIsFadeInGo)
		{
			return;
		}
		mIsFadeInGo = isActive;
		if (FadeInCompleteActiveGO.Count == 0)
		{
			return;
		}
		if ((Object)(object)mAttachAgent == (Object)null)
		{
			mAttachAgent = ((Component)this).GetComponentInParent<AttachAgent>(true);
		}
		foreach (GameObject item in FadeInCompleteActiveGO)
		{
			if (!((Object)(object)item == (Object)null) && (Object)(object)mAttachAgent != (Object)null)
			{
				mAttachAgent.SetBoneActiveStatus(((Object)item).name, isActive);
			}
		}
	}

	private void UpdateSkeletonRendererAlpha(NativeActorFadeRenderer skeletonRenderer)
	{
		if ((Object)(object)skeletonRenderer == (Object)null || skeletonRenderer.Skeleton == null)
		{
			return;
		}
		skeletonRenderer.Skeleton.A = mAlpha;
		if (mAlpha > 0f)
		{
			if (!((Component)skeletonRenderer).gameObject.activeSelf)
			{
				((Component)skeletonRenderer).gameObject.SetActive(true);
			}
		}
		else if (((Component)skeletonRenderer).gameObject.activeSelf)
		{
			((Component)skeletonRenderer).gameObject.SetActive(false);
		}
	}

	private void UpdateSkeletonRendererColor()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)mSkeletonRenderer == (Object)null) && mSkeletonRenderer.Skeleton != null)
		{
			NativeActorFadeColorExtensions.SetColor(mSkeletonRenderer.Skeleton, mColor);
		}
	}

	public static SpriteRendererList PlayableSourceGetSpriteRendererList(MyPlayable playable)
	{
		return playable.mySource.GetComponentInChildren<SpriteRendererList>(true);
	}

	private void OnValidate()
	{
		foreach (SpriteRenderer mySprite in mySpriteList)
		{
			if ((Object)(object)mySprite == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
		}
		foreach (FordingSpriteRenderer mFordingSprite in mFordingSpriteList)
		{
			if ((Object)(object)mFordingSprite == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
		}
		foreach (GameObject item in FadeInCompleteActiveGO)
		{
			if ((Object)(object)item == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
		}
	}

	public SpriteRendererList()
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
	}

	static SpriteRendererList()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
	}
}

}
