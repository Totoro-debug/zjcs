// Migrated from the selected official code; see Map13 entity presentation source receipt.
using System.Collections.Generic;

namespace Zjcs.Official.World.Map13Entities
{

public class EntityMapDisplayActionInfoParser
{
	private const int kDefaultState = -1;

	private Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, Dictionary<int, EntityMapDisplayActionInfo>>>>>>> mInfos;

	public void Load(EntityMapDisplayActionInfo[] configs)
	{
		mInfos = new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, Dictionary<int, EntityMapDisplayActionInfo>>>>>>>();
		foreach (EntityMapDisplayActionInfo entityMapDisplayActionInfo in configs)
		{
			if (!mInfos.TryGetValue(entityMapDisplayActionInfo.EntityClassId, out var value))
			{
				value = new Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, Dictionary<int, EntityMapDisplayActionInfo>>>>>>();
				mInfos.Add(entityMapDisplayActionInfo.EntityClassId, value);
			}
			if (!value.TryGetValue(entityMapDisplayActionInfo.GroupId, out var value2))
			{
				value2 = new Dictionary<int, Dictionary<string, Dictionary<GroundType, Dictionary<string, Dictionary<int, EntityMapDisplayActionInfo>>>>>();
				value.Add(entityMapDisplayActionInfo.GroupId, value2);
			}
			if (!value2.TryGetValue(entityMapDisplayActionInfo.MapClassId, out var value3))
			{
				value3 = new Dictionary<string, Dictionary<GroundType, Dictionary<string, Dictionary<int, EntityMapDisplayActionInfo>>>>();
				value2.Add(entityMapDisplayActionInfo.MapClassId, value3);
			}
			if (!value3.TryGetValue(entityMapDisplayActionInfo.AreaTag, out var value4))
			{
				value4 = new Dictionary<GroundType, Dictionary<string, Dictionary<int, EntityMapDisplayActionInfo>>>();
				value3.Add(entityMapDisplayActionInfo.AreaTag, value4);
			}
			if (!value4.TryGetValue(entityMapDisplayActionInfo.GroundType, out var value5))
			{
				value5 = new Dictionary<string, Dictionary<int, EntityMapDisplayActionInfo>>();
				value4.Add(entityMapDisplayActionInfo.GroundType, value5);
			}
			string tag = entityMapDisplayActionInfo.Tag;
			if (!value5.TryGetValue(tag, out var value6))
			{
				value6 = new Dictionary<int, EntityMapDisplayActionInfo>();
				value5.Add(tag, value6);
			}
			value6.Add(entityMapDisplayActionInfo.State, entityMapDisplayActionInfo);
		}
	}

	public EntityMapDisplayActionInfo Get(int entityClassId, int groupId, int mapClassId, string areaTag, GroundType groundType, int state, string tag)
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
		if (!value6.TryGetValue(state, out var value7) && !value6.TryGetValue(-1, out value7))
		{
			return null;
		}
		return value7;
	}


}
}
