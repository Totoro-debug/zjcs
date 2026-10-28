// Migrated original tree-playable application source; see source receipt.
using UnityEngine;
using Zjcs.Official.World.Map13EntityStateViews;
// Original status-count query only. No icon/resource loading, award execution
// or state transition is provided by this explicit local-data binding.

namespace Zjcs.Official.World.Map13TreePlayables
{
public sealed class Map13AwardCountBinding : MonoBehaviour
{
    public Map13WorldStageQuery Stage;
    public Map13AwardItemQuery Awards;
    private Map13WorldStageQuery mWorldStageComponent;
public long GetStatusItemCount()
	{
		if (mWorldStageComponent == null)
		{
			mWorldStageComponent = Stage;
		}
		int stateComponentIndex = mWorldStageComponent.GetStateComponentIndex(StageComponentType.ResultAwardItem);
		if (!Awards.TryGetAwardItemInfo(stateComponentIndex, out var result))
		{
			return 0L;
		}
		return result.Count;
	}
}

}
