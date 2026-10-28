// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using UnityEngine;





namespace Zjcs.Official.World.Map13GridVisibility
{
[Serializable]
public class GridTransformComponentBodyPosInfo
{

	public Pos2d Pos;

	public GroundType GroundType;

	[Tooltip("打勾则忽略点击")]
	public bool IgnoreClick;

	[Tooltip("打勾则表示这个物件地形要添加到覆写地形中")]
	public bool OverrideGroundType;
}

}
