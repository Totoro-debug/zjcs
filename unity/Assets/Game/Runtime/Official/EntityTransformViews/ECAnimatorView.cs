// Migrated complete original application class; see entity transform receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using AttachAgent = Zjcs.Official.StoryAttachment.AttachAgent;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using UnityEngine;

namespace Zjcs.Official.EntityTransformViews
{
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
