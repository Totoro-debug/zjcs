// Original MyWorldStage.StageStateCfg declaration only.
using System;
using UnityEngine;
namespace Zjcs.Official.EntityWorldStages
{
	[Serializable]
	public class LegacyWorldStageStateCfg
	{
		public string myUIName;

		[Tooltip("切换到此状态才会播放的Playable")]
		public GameObject myStartPlayable;

		public GameObject myLoopPlayable;

		public ECWorldStageInteractionOperationItemData InteractionOperation;
	}
}
