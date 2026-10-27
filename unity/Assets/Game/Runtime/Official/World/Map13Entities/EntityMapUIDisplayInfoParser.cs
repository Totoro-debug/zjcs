// Migrated from the selected official code; see Map13 entity presentation source receipt.
using System.Collections.Generic;

namespace Zjcs.Official.World.Map13Entities
{

public class EntityMapUIDisplayInfoParser
{
	private Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<string, EntityMapUIDisplayInfo>>>>> mInfos;

	public void Load(EntityMapUIDisplayInfo[] configs)
	{
		mInfos = new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<string, EntityMapUIDisplayInfo>>>>>();
		EntityMapUIDisplayInfo[] array = configs;
		foreach (EntityMapUIDisplayInfo entityMapUIDisplayInfo in array)
		{
			if (!string.IsNullOrEmpty(entityMapUIDisplayInfo.MiniMapMark))
			{
				entityMapUIDisplayInfo.MiniMapMark = "UI/Map/MiniMap/MiniMapMark/" + entityMapUIDisplayInfo.MiniMapMark;
			}
			if (!mInfos.TryGetValue(entityMapUIDisplayInfo.EntityClassId, out var value))
			{
				value = new Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<string, EntityMapUIDisplayInfo>>>>();
				mInfos.Add(entityMapUIDisplayInfo.EntityClassId, value);
			}
			if (!value.TryGetValue(entityMapUIDisplayInfo.GroupId, out var value2))
			{
				value2 = new Dictionary<int, Dictionary<string, Dictionary<string, EntityMapUIDisplayInfo>>>();
				value.Add(entityMapUIDisplayInfo.GroupId, value2);
			}
			if (!value2.TryGetValue(entityMapUIDisplayInfo.MapClassId, out var value3))
			{
				value3 = new Dictionary<string, Dictionary<string, EntityMapUIDisplayInfo>>();
				value2.Add(entityMapUIDisplayInfo.MapClassId, value3);
			}
			if (!value3.TryGetValue(entityMapUIDisplayInfo.AreaTag, out var value4))
			{
				value4 = new Dictionary<string, EntityMapUIDisplayInfo>();
				value3.Add(entityMapUIDisplayInfo.AreaTag, value4);
			}
			string tag = entityMapUIDisplayInfo.Tag;
			value4.Add(tag, entityMapUIDisplayInfo);
		}
	}

	public EntityMapUIDisplayInfo Get(int entityClassId, int groupId, int mapClassId, string areaTag, string tag)
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
		tag = tag ?? "";
		if (!value4.TryGetValue(tag, out var value5) && !value4.TryGetValue("", out value5))
		{
			return null;
		}
		return value5;
	}


}
}
