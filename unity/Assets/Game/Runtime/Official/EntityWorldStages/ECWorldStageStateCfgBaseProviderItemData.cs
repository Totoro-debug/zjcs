// Migrated official client source; see entity world-stage receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityWorldStages
{
[Serializable]
public struct ECWorldStageStateCfgBaseProviderItemData
{
	[SerializeReference]
	public IECWorldStageStateCfgBaseProvider ECWorldStageCfgProvider;
}

}
