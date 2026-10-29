// Migrated original MapEntityExtra.GetEntityTag; see Map14 display-data source receipt.
using System;
namespace Zjcs.Official.World.Map14DisplayTags
{
public interface IMapEntityTagInputs
{ int GetLogicalEntityId(object entity); string GetEntityTag(object entity,int logicalId); }
public static class OfficialMapEntityTagQuery
{
public static string GetEntityTag(object ecEntity, IMapEntityTagInputs inputs)
	{
		int loEntIdByEntId = inputs.GetLogicalEntityId(ecEntity);
		string entityTag = inputs.GetEntityTag(ecEntity, loEntIdByEntId);
		if (string.IsNullOrEmpty(entityTag))
		{
			return string.Empty;
		}
		return entityTag;
	}
}
}
