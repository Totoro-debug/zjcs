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
public class WorldStageStateCfg
{
	public string myUIName;

	[Tooltip("切换到此状态才会播放的Playable")]
	public GameObject myStartPlayable;

	public GameObject myLoopPlayable;

	public ECWorldStageInteractionOperationItemData InteractionOperation;
}

}
