// Migrated from the selected official code; see Map13 entity presentation source receipt.
using System;

namespace Zjcs.Official.World.Map13Entities
{

[Serializable]
public class EntityMapDisplayActionInfo
{
	public int EntityClassId;

	public int GroupId;

	public int MapClassId;

	public string AreaTag;

	public GroundType GroundType;

	public string Tag;

	public int State;

	public int InteractionActionId;

	public int InteractionEntityActionId;

	public int RegionActivateActionId;

	public const int __ID__ = 1555962125;





	public int GetTypeId()
	{
		return 1555962125;
	}



	public override string ToString()
	{
		return "{ EntityClassId:" + EntityClassId + ",GroupId:" + GroupId + ",MapClassId:" + MapClassId + ",AreaTag:" + AreaTag + ",GroundType:" + GroundType.ToString() + ",Tag:" + Tag + ",State:" + State + ",InteractionActionId:" + InteractionActionId + ",InteractionEntityActionId:" + InteractionEntityActionId + ",RegionActivateActionId:" + RegionActivateActionId + ",}";
	}
}
}
