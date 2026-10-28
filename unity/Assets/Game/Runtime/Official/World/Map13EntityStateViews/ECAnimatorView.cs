// Migrated source application/query code; see map13-entity-state-views receipt.
using Object = UnityEngine.Object;
using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StorySourceFade;
using UnityEngine;

namespace Zjcs.Official.World.Map13EntityStateViews
{
[Serializable]
public class ECAnimatorView : ECViewBase
{
	[SerializeField]
	private Animator mAnimator;

	protected override int mOrder => 0;

	public AnimationController AnimationController { get; private set; }

	protected override void Load()
	{
		base.Load();
		AnimationController = ((Component)mOwner.transform.parent).gameObject.GetComponent<AnimationController>();
		if ((Object)(object)AnimationController == (Object)null)
		{
			AnimationController = ((Component)mOwner.transform.parent).gameObject.AddComponent<AnimationController>();
		}
		AnimationController.myAnimator = mAnimator;
		AnimationController.Init();
	}
}

}
