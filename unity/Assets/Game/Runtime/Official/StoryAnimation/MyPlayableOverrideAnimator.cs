// Migrated official source; see battle-rules/animator-runtime-source-manifest.json.
using Object = UnityEngine.Object;
using System;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace Zjcs.Official.StoryAnimation
{
[MyPlayableApply(new Type[] { typeof(AnimationController) })]
public class MyPlayableOverrideAnimator : MyPlayableAnimator
{
	[Header("动画重写的基本配置")]
	public string OriginClipName;

	public AnimationClip OverrideClip;

	protected override void PlayImpl()
	{
		SetOverrideClip();
		base.PlayImpl();
	}

	private void SetOverrideClip()
	{
		AnimationController animationController = myApplyComponent as AnimationController;
		if ((Object)(object)animationController == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
		else if (!string.IsNullOrEmpty(OriginClipName) && !((Object)(object)OverrideClip == (Object)null))
		{
			animationController.SetAnimatorClip(OriginClipName, OverrideClip);
		}
	}
}

}
