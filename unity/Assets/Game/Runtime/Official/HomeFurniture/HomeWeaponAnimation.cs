// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Spine;
using Spine.Unity;
using Animation = Spine.Animation;
using AnimationState = Spine.AnimationState;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;
public static class HomeWeaponAnimation
{
public static void PlayAnimation(IHomeWeaponAnimationBindings bindings, SkeletonAnimation skeletonAnimation, string animationName, bool loop, float mixDuration = 0f, bool endResumeLast = false)
	{
		if ((Object)(object)skeletonAnimation == (Object)null)
		{
			Debug.LogError("skeletonAnimation为空！Anim:" + (animationName));
		}
		else
		{
			if ((Object)(object)((SkeletonRenderer)skeletonAnimation).skeletonDataAsset == (Object)null)
			{
				return;
			}
			AnimationState animationState = skeletonAnimation.AnimationState;
			if (animationState == null)
			{
				skeletonAnimation.loop = loop;
				skeletonAnimation.AnimationName = animationName;
				return;
			}
			Animation orLoadSpineAnimation = bindings.GetOrLoadSpineAnimation((ISkeletonComponent)(object)skeletonAnimation, animationName);
			if (orLoadSpineAnimation == null)
			{
				Debug.LogWarning("不存在指定的动画:" + animationName);
			}
			else
			{
				SetAnimation(animationState, orLoadSpineAnimation, loop, mixDuration, endResumeLast);
				bindings.UpdateAnimation((SkeletonRenderer)(object)skeletonAnimation);
			}
		}
	}
public static void PlayAnimation(IHomeWeaponAnimationBindings bindings, SkeletonGraphic skeletonGraphic, string animationName, bool loop, float mixDuration = 0f, bool endResumeLast = false)
	{
		if (string.IsNullOrEmpty(animationName))
		{
			return;
		}
		AnimationState animationState = skeletonGraphic.AnimationState;
		if (animationState == null)
		{
			skeletonGraphic.startingLoop = loop;
			skeletonGraphic.startingAnimation = animationName;
			return;
		}
		Animation orLoadSpineAnimation = bindings.GetOrLoadSpineAnimation((ISkeletonComponent)(object)skeletonGraphic, animationName);
		if (orLoadSpineAnimation == null)
		{
			Debug.LogWarning("不存在指定的动画:" + (animationName));
		}
		else
		{
			SetAnimation(animationState, orLoadSpineAnimation, loop, mixDuration, endResumeLast);
			bindings.UpdateAnimation(skeletonGraphic);
		}
	}
private static void SetAnimation(AnimationState animationState, Animation animation, bool loop, float mixDuration, bool endResumeLast)
	{
		TrackEntry current = animationState.GetCurrent(0);
		Animation val = ((current != null) ? current.Animation : null);
		TrackEntry val2 = animationState.SetAnimation(0, animation, loop);
		val2.MixDuration = mixDuration;
		val2.MixTime = ((mixDuration > 0f) ? 0.001f : 0f);
		if ((!loop & endResumeLast) && val != null)
		{
			animationState.AddAnimation(0, val, true, 0f);
		}
	}
}

}
