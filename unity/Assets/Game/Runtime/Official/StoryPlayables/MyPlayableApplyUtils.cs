// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public static class MyPlayableApplyUtils
	{
		public static void SetPlayableApplyComponent(MyPlayable playable, MyPlayableApplyAttribute applyAttr, GameObject targetGO, bool applyTargetOfNullAttr = false)
		{
			if (!((Object)(object)playable == (Object)null) && !((Object)(object)targetGO == (Object)null))
			{
				Component val = (playable.myApplyComponent = ((applyAttr == null) ? ((Component)(object)(applyTargetOfNullAttr ? targetGO.transform : null)) : applyAttr.GetApplyComponent(playable, targetGO)));
				if ((Object)(object)val != (Object)null)
				{
					MyPlayableApplyObj.Bind(val.gameObject, playable);
				}
			}
		}
	}
}
