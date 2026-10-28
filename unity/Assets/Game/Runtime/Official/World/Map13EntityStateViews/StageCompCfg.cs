// Migrated source application/query code; see map13-entity-state-views receipt.
using System;
using UnityEngine;

namespace Zjcs.Official.World.Map13EntityStateViews
{

[Serializable]

public class StageCompCfg
{
	[Tooltip("对应到脚本名")]

	public StageComponentType CompType;

	[Tooltip("这个数字会传递给配置的脚本(怎么用看业务，一般表示下标)")]

	public int CompIndex;
}

}
