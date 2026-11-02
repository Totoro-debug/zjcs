// Migrated official status application source; see status view runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
public class ApplyOverrideToStatusSource : ApplyOverrideSourceOrTarget
{
	protected override bool TryGetOverrideGameObject(MyPlayable playable, out GameObject overrideObject)
	{
		overrideObject = null;
		MyFightStatus status = playable.TryGetParam("Status") as MyFightStatus;
		if ((Object)(object)status == (Object)null)
		{
			Debug.LogError(playable.name + " is not status's playable");
			return false;
		}
		overrideObject = status.Context.Creator;
		return (Object)(object)overrideObject != (Object)null;
	}
}

}
