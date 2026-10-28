// Migrated source application/query code; see map13-entity-state-views receipt.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13EntityStateViews
{

[Serializable]

public class StageStateInfo
{
	[Tooltip("进入本状态时触发的结果")]

	public List<StageCompCfg> EnterResults = new List<StageCompCfg>();

	[Tooltip("状态的子状态，根据选择器的逻辑进行选择要运行的状态")]

	public List<SubStageStateInfo> SubStateList = new List<SubStageStateInfo>();
}

}
