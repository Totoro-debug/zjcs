// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	[DisallowMultipleComponent]
	public class ApplyBySource : ApplyOnInit
	{
		[Tooltip("不限制目标组件类型时，将apply组件设置成target对象transform")]
		public bool ApplyTargetOfNullAttr;

		protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
		{
			SetMyApplyComponent(playable, applyAttr);
		}

		protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
		{
			SetMyApplyComponent(playable, applyAttr);
		}

		private void SetMyApplyComponent(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
		{
			MyPlayableApplyUtils.SetPlayableApplyComponent(playable, applyAttr, myPlayable.mySource, ApplyTargetOfNullAttr);
		}
	}
}
