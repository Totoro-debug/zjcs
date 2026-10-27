// Migrated from the selected official code; see Map13 entity presentation source receipt.
using System;

namespace Zjcs.Official.World.Map13Entities
{

[Serializable]
public class EntityMapDisplayInfo
{
	public int EntityClassId;

	public int GroupId;

	public int MapClassId;

	public string AreaTag;

	public GroundType GroundType;

	public string Tag;

	public string DisplayPath;

	public string ReddotGraphName;

	public string DirectionGuiderItemUIPath;

	public const int __ID__ = 454028407;





	public int GetTypeId()
	{
		return 454028407;
	}



	public override string ToString()
	{
		return "{ EntityClassId:" + EntityClassId + ",GroupId:" + GroupId + ",MapClassId:" + MapClassId + ",AreaTag:" + AreaTag + ",GroundType:" + GroundType.ToString() + ",Tag:" + Tag + ",DisplayPath:" + DisplayPath + ",ReddotGraphName:" + ReddotGraphName + ",DirectionGuiderItemUIPath:" + DirectionGuiderItemUIPath + ",}";
	}
}
}
