// Migrated original application/read-query code; see Map13 visibility-view receipt.
using System;
using System.Collections.Generic;
using Zjcs.Official.EntityViews;

namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public interface IMap13EntityVisibilityBinding
{ IClientVisibilitySupporter ClientVisibilitySupporter { get; } }
public sealed class Map13OwnershipData { public EntityOwnershipType Type; public long OwnerId; }
public sealed class Map13EntityDurationData { public float DurationWhenExplore; public int EntityDuration; }
public sealed class Map13WorldActivityData { public int ActivityClassId; }
public abstract class Map13PresentationServices
{
    public abstract long PlayerId { get; }
    public abstract DateTime GetDateTime();
    public static Map13PresentationServices For(IServiceProvider provider) =>
        provider?.GetService(typeof(Map13PresentationServices)) as Map13PresentationServices
        ?? throw new InvalidOperationException("Original presentation player/time service is not bound.");
public float CalcEndIntervalS(DateTime startTime, float intervalS)
	{
		DateTime dateTime = GetDateTime();
		DateTime dateTime2 = startTime.AddSeconds(intervalS);
		if (dateTime2 <= dateTime)
		{
			return 0f;
		}
		return (float)(dateTime2 - dateTime).TotalSeconds;
	}
}

}
