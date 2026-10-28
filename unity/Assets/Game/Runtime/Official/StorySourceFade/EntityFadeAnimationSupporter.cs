// Migrated official application source; see source-actor fading receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using System;
using UnityEngine;

namespace Zjcs.Official.StorySourceFade
{
public class EntityFadeAnimationSupporter : IEntityFadeAnimationSupporter
{
	private SpriteRendererList mSpriteRendererList;

	private int mFadeOutPriority = int.MaxValue;

	public bool IsIn
	{
		get
		{
			if (!((Object)(object)mSpriteRendererList != (Object)null))
			{
				return true;
			}
			return mSpriteRendererList.IsIn;
		}
	}

	public Transform ModelTransform
	{
		get
		{
			if (!((Object)(object)mSpriteRendererList != (Object)null))
			{
				return null;
			}
			return ((Component)mSpriteRendererList).transform;
		}
	}

	public EntityFadeAnimationSupporter(SpriteRendererList spriteRendererList)
	{
		mSpriteRendererList = spriteRendererList;
	}

	public void FadeIn(float durationS = -1f, bool hasEvent = true, int priority = 0)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null) && mFadeOutPriority >= priority)
		{
			mSpriteRendererList.FadeIn(durationS, hasEvent);
			mFadeOutPriority = int.MaxValue;
		}
	}

	public void ImmediateFadeIn(bool hasEvent = true, int priority = 0)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null) && mFadeOutPriority >= priority)
		{
			mSpriteRendererList.ImmediateFadeIn(hasEvent);
			mFadeOutPriority = int.MaxValue;
		}
	}

	public void FadeOut(float durationS = -1f, bool hasEvent = true, int priority = 0)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mFadeOutPriority = priority;
			mSpriteRendererList.FadeOut(durationS, hasEvent);
		}
	}

	public void ImmediateFadeOut(bool hasEvent = true, int priority = 0)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mFadeOutPriority = priority;
			mSpriteRendererList.ImmediateFadeOut(hasEvent);
		}
	}

	public void AddOnFadeOutStartEvent(Action onFadeOutStart)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mSpriteRendererList.OnFadeOutStart += onFadeOutStart;
		}
	}

	public void AddOnFadeOutCompletedEvent(Action onFadeOutCompleted)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mSpriteRendererList.OnFadeOutCompleted += onFadeOutCompleted;
		}
	}

	public void AddOnFadeInStartEvent(Action onFadeInStart)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mSpriteRendererList.OnFadeInStart += onFadeInStart;
		}
	}

	public void AddOnFadeInCompletedEvent(Action onFadeInCompleted)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mSpriteRendererList.OnFadeInCompleted += onFadeInCompleted;
		}
	}

	public void RemoveOnFadeOutStartEvent(Action onFadeOutStart)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mSpriteRendererList.OnFadeOutStart -= onFadeOutStart;
		}
	}

	public void RemoveOnFadeOutCompletedEvent(Action onFadeOutCompleted)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mSpriteRendererList.OnFadeOutCompleted -= onFadeOutCompleted;
		}
	}

	public void RemoveOnFadeInStartEvent(Action onFadeInStart)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mSpriteRendererList.OnFadeInStart -= onFadeInStart;
		}
	}

	public void RemoveOnFadeInCompletedEvent(Action onFadeInCompleted)
	{
		if (!((Object)(object)mSpriteRendererList == (Object)null))
		{
			mSpriteRendererList.OnFadeInCompleted -= onFadeInCompleted;
		}
	}

	public void ResetFadeAnimation()
	{
		ImmediateFadeIn();
		mFadeOutPriority = int.MaxValue;
	}
}

}
