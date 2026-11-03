// Migrated current official framework source; see status custom runtime receipt.
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{

[DisallowMultipleComponent]
public class ApplyByCustom : ApplyOnInit
{
	[Tooltip("可任意指定的GameObject作为apply对象")]
	public GameObject CustomApplyObj;

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
		MyPlayableApplyUtils.SetPlayableApplyComponent(playable, applyAttr, CustomApplyObj, ApplyTargetOfNullAttr);
	}
}

}
