// Migrated original tree-playable application source; see source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryFollowBone;
using UnityEngine;


namespace Zjcs.Official.World.Map13TreePlayables
{
[RequireComponent(typeof(MyPlayableAnimator))]
public class ApplyAnimatorTriggerByAwardItemCount : ApplyOnInit
{
	[SerializeField]
	private bool mIsSource;

	[SerializeField]
	private MonoDictionary<long, string> mAnimatorTriggerName;

	[SerializeField]
	[Tooltip("不限制目标组件类型时，将apply组件设置成target对象transform")]
	private bool mApplyTargetOfNullAttr;

	private MyPlayableAnimator myPlayableAnimator;

	private Component myApplyComponent;

	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		SetMyApplyComponent(playable, applyAttr);
		SetPlayableAnimatorTrigger();
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		SetMyApplyComponent(playable, applyAttr);
		SetPlayableAnimatorTrigger();
	}

	private void SetPlayableAnimatorTrigger()
	{
		if ((Object)(object)myPlayableAnimator == (Object)null)
		{
			myPlayableAnimator = ((Component)this).GetComponent<MyPlayableAnimator>();
		}
		long statusItemCount = myApplyComponent.GetComponent<Map13AwardCountBinding>().GetStatusItemCount();
		if (mAnimatorTriggerName.TryGetValue(statusItemCount, out var value))
		{
			myPlayableAnimator.myTriggerParam = value;
			myPlayableAnimator.UpdateHashMapString();
		}
	}

	private void SetMyApplyComponent(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		MyPlayableApplyUtils.SetPlayableApplyComponent(playable, applyAttr, GetApplyGO(playable), mApplyTargetOfNullAttr);
		myApplyComponent = playable.myApplyComponent;
	}

	private GameObject GetApplyGO(MyPlayable playable)
	{
		if (mIsSource)
		{
			return playable.mySource;
		}
		return playable.myTarget;
	}
}

}
