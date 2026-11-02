// Migrated official status application source; see status view runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
public abstract class ApplyOverrideSourceOrTarget : ApplyOnInit
{
	[Tooltip("对Source进行override，不勾选则对target")]
	public bool OverrideSource = true;

	[Tooltip("查找Source施加的状态，不勾选则查找Target施加的状态")]
	public bool UseSourceDataToOverride = true;

	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		if (TryGetOverrideGameObject(playable, out var overrideObject) && !((Object)(object)overrideObject == (Object)null))
		{
			if (OverrideSource)
			{
				playable.mySource = overrideObject;
			}
			else
			{
				playable.myTarget = overrideObject;
			}
		}
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
	}

	protected abstract bool TryGetOverrideGameObject(MyPlayable playable, out GameObject overrideObject);
}

}
