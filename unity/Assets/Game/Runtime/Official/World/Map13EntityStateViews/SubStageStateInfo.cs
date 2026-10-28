// Migrated source application/query code; see map13-entity-state-views receipt.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13EntityStateViews
{

[Serializable]

public class SubStageStateInfo
{
	[Tooltip("判断是否满足状态条件，不满足则不能走到结果")]

	public List<StageCompCfg> Conditions = new List<StageCompCfg>();

	[Tooltip("玩家可进行一些行为")]

	public StageCompCfg Behaviour;

	[Tooltip("行为结束后触发的所有结果")]

	public List<StageCompCfg> Results = new List<StageCompCfg>();

	public object Select { get; internal set; }
}

}
