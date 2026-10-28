// Migrated original application/read-query code; see Map13 visibility-view receipt.
using System;
using Zjcs.Official.EntityViews;

namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public sealed class Map13EntitySourceQuery
{
    public ECViewEntityData Entity;
    public EcEntitySourceType EntitySourceType { get; set; }
    public Map13OwnershipData Ownership { get; set; } = new Map13OwnershipData();
    public DateTime? CreateTime { get; set; }
    public Map13WorldActivityData Activity;
    public Func<int, Map13EntityDurationData> EntityParameters, ActivityParameters;
public bool IsMine(EntityOwnershipType type, long ownerId)
	{
		if (Ownership == null)
		{
			return false;
		}
		if (Ownership.Type == type)
		{
			return Ownership.OwnerId == ownerId;
		}
		return false;
	}
public float GetDuration()
	{
		if (!CreateTime.HasValue)
		{
			return -1f;
		}
		if (EntitySourceType == EcEntitySourceType.Explore)
		{
			return GetExploreEntityDuration();
		}
		if (EntitySourceType == EcEntitySourceType.ActivityRefresh)
		{
			return GetActivityEntityDuration();
		}
		return -1f;
	}
private float GetExploreEntityDuration()
	{
		Map13EntityDurationData entityExtendParameterInfo = EntityParameters(Entity.ClassId);
		if (entityExtendParameterInfo == null)
		{
			return -1f;
		}
		return entityExtendParameterInfo.DurationWhenExplore;
	}
private float GetActivityEntityDuration()
	{
		Map13WorldActivityData component = Activity;
		if (component == null)
		{
			return -1f;
		}
		Map13EntityDurationData mapActivityEntityInfo = ActivityParameters(component.ActivityClassId);
		if (mapActivityEntityInfo.EntityDuration > 0)
		{
			return mapActivityEntityInfo.EntityDuration;
		}
		return -1f;
	}
}

}
