// Migrated from selected official terrain code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using UnityEngine;

[CreateAssetMenu(fileName = "MapGroundTile", menuName = "2D/MyTile/MapGroundTile")]
public class MapGroundTile : MyTile
{
	public GroundType GroundType;

	public List<Pos2d> PosList = new List<Pos2d> { Pos2d.Zero };

	public int Height;
}

}
