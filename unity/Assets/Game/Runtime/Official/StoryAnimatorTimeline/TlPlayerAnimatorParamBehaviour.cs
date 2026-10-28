// Migrated original source; see map14-story-animator-timeline receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryAnimation;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryAnimatorTimeline
{
[Serializable]
public class TlPlayerAnimatorParamBehaviour : TlAnimatorParamBehaviour
{
	[Header("动画重写的基本配置")]
	public string OriginClipName;

	public AnimationClip OverrideClip;

	protected override Animator GetAnimator(object playerData)
	{
		GameObject myEntity = PlayerAnimatorTimelineServices.Current.PlayerEntity;
		if ((Object)(object)myEntity == (Object)null)
		{
			return null;
		}
		AnimationController component = myEntity.GetComponent<AnimationController>();
		SetAnimatorOverrideClip(component);
		if (!((Object)(object)component == (Object)null))
		{
			return component.myAnimator;
		}
		return null;
	}

	private void SetAnimatorOverrideClip(AnimationController animationController)
	{
		if (!((Object)(object)OverrideClip == (Object)null) && !string.IsNullOrEmpty(OriginClipName) && !((Object)(object)animationController == (Object)null))
		{
			animationController.SetAnimatorClip(OriginClipName, OverrideClip);
		}
	}
}

}
