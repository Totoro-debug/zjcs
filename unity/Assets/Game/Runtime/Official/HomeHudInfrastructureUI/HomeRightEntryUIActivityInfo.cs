namespace Zjcs.Official.HomeHudInfrastructureUI
{
public sealed class HomeRightEntryUIActivityInfo
{
	public readonly int Id;

	public readonly int SortedId;

	public readonly string EntryPath;

	public readonly bool IsActivity;

	public readonly ActivityType? ActivityType;

	public readonly int? ActivityId;

    public HomeRightEntryUIActivityInfo(int id,int sortedId,string entryPath,bool isActivity,ActivityType? activityType,int? activityId)
    { Id=id;SortedId=sortedId;EntryPath=entryPath;IsActivity=isActivity;ActivityType=activityType;ActivityId=activityId; }
}
}
