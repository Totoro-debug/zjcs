// Migrated from the selected official code; see Map13 entity presentation source receipt.
using System.Collections.Generic;

namespace Zjcs.Official.World.Map13Entities
{

public class EntityMapDisplayInfoParser
{
	public const int kGroupIgnoreKey = 0;

	public const int kMapClassIdIgnoreKey = 0;

	public const string kAreaTagIgnoreKey = "IgnoreKey";

	public const GroundType kGroundTypeIgnoreKey = GroundType.IgnoreKey;

	public const string kTagIgnoreKey = "";

	private Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>>>>> mInfos;

	public void Load(EntityMapDisplayInfo[] configs)
	{
		mInfos = new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>>>>>();
		foreach (EntityMapDisplayInfo entityMapDisplayInfo in configs)
		{
			if (!string.IsNullOrEmpty(entityMapDisplayInfo.DirectionGuiderItemUIPath))
			{
				entityMapDisplayInfo.DirectionGuiderItemUIPath = "UI/DirectionGuider/" + entityMapDisplayInfo.DirectionGuiderItemUIPath;
			}
			if (!mInfos.TryGetValue(entityMapDisplayInfo.EntityClassId, out var value))
			{
				value = new Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>>>>();
				mInfos.Add(entityMapDisplayInfo.EntityClassId, value);
			}
			if (!value.TryGetValue(entityMapDisplayInfo.GroupId, out var value2))
			{
				value2 = new Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>>>();
				value.Add(entityMapDisplayInfo.GroupId, value2);
			}
			if (!value2.TryGetValue(entityMapDisplayInfo.MapClassId, out var value3))
			{
				value3 = new Dictionary<string, Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>>();
				value2.Add(entityMapDisplayInfo.MapClassId, value3);
			}
			if (!value3.TryGetValue(entityMapDisplayInfo.AreaTag, out var value4))
			{
				value4 = new Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>();
				value3.Add(entityMapDisplayInfo.AreaTag, value4);
			}
			if (!value4.TryGetValue(entityMapDisplayInfo.GroundType, out var value5))
			{
				value5 = new Dictionary<string, EntityMapDisplayInfo>();
				value4.Add(entityMapDisplayInfo.GroundType, value5);
			}
			string tag = entityMapDisplayInfo.Tag;
			value5.Add(tag, entityMapDisplayInfo);
		}
	}

	public EntityMapDisplayInfo Get(int entityClassId, int groupId, int mapClassId, string areaTag, GroundType groundType, string tag)
	{
		if (!mInfos.TryGetValue(entityClassId, out var value))
		{
			return null;
		}
		if (!value.TryGetValue(groupId, out var value2) && !value.TryGetValue(0, out value2))
		{
			return null;
		}
		if (!value2.TryGetValue(mapClassId, out var value3) && !value2.TryGetValue(0, out value3))
		{
			return null;
		}
		areaTag = ((areaTag == null) ? "IgnoreKey" : areaTag);
		if (!value3.TryGetValue(areaTag, out var value4) && !value3.TryGetValue("IgnoreKey", out value4))
		{
			return null;
		}
		if (!value4.TryGetValue(groundType, out var value5) && !value4.TryGetValue(GroundType.IgnoreKey, out value5))
		{
			return null;
		}
		tag = tag ?? "";
		if (!value5.TryGetValue(tag, out var value6) && !value5.TryGetValue("", out value6))
		{
			return null;
		}
		return value6;
	}

	public List<EntityMapDisplayInfo> GetAllEntityClassIdInfo(int entityClassId)
	{
		List<EntityMapDisplayInfo> list = new List<EntityMapDisplayInfo>();
		if (!mInfos.TryGetValue(entityClassId, out var value))
		{
			return list;
		}
		foreach (KeyValuePair<int, Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>>>> item in value)
		{
			foreach (KeyValuePair<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>>> item2 in item.Value)
			{
				foreach (KeyValuePair<string, Dictionary<GroundType, Dictionary<string, EntityMapDisplayInfo>>> item3 in item2.Value)
				{
					foreach (KeyValuePair<GroundType, Dictionary<string, EntityMapDisplayInfo>> item4 in item3.Value)
					{
						foreach (KeyValuePair<string, EntityMapDisplayInfo> item5 in item4.Value)
						{
							list.Add(item5.Value);
						}
					}
				}
			}
		}
		return list;
	}


}
}
