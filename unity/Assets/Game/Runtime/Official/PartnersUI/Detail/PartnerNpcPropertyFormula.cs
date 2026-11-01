using System;
using System.Collections.Generic;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
namespace Zjcs.Official.PartnersUI
{
    public sealed class PartnerNpcPropertyInfo { public int LevelPropId; }
    public sealed class PartnerNpcTalentPropertyInfo
    {
        public int PropId, PropLevel;
        public List<PropType> PropTypes;
    }
    public interface IPartnerNpcPropertyPlayer
    {
        IReadOnlyDictionary<int, int> GetNpcFriendshipLevelDict();
        IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int npcId);
    }
    public interface IPartnerNpcPropertySource
    {
        PartnerNpcPropertyInfo GetNpc(int npcId);
        PartnerNpcTalentPropertyInfo GetNodeMapPropInfo(int npcId, int nodeId);
        IReadOnlyDictionary<PropType, long> GetProps(int propId, int level);
    }
    // Selected BattleFormulaHandler methods; preserve the original branches,
    // merge order, zero entries and integer addition.
    public sealed class PartnerNpcPropertyFormula
    {
        private readonly IPartnerNpcPropertySource source;
        public PartnerNpcPropertyFormula(IPartnerNpcPropertySource value)
        { source = value ?? throw new ArgumentNullException(nameof(value)); }
public IReadOnlyDictionary<PropType, long> CalcPlayerNpcProps(IPartnerNpcPropertyPlayer player, int npcClassId)
	{
		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();
		if (player.GetNpcFriendshipLevelDict().TryGetValue(npcClassId, out var value))
		{
			IReadOnlyDictionary<PropType, long> readOnlyDictionary = CalcNpcFriendshipProps(npcClassId, value);
			if (readOnlyDictionary != null || readOnlyDictionary.Count > 0)
			{
				MergePropsInPlace(dictionary, readOnlyDictionary);
			}
		}
		IReadOnlyCollection<int> npcTalentTreeUnlockedNodeIds = player.GetNpcTalentTreeUnlockedNodeIds(npcClassId);
		if (npcTalentTreeUnlockedNodeIds.Count > 0)
		{
			IReadOnlyDictionary<PropType, long> readOnlyDictionary2 = CalcNpcTalentTreeProps(npcClassId, npcTalentTreeUnlockedNodeIds);
			if (readOnlyDictionary2 != null || readOnlyDictionary2.Count > 0)
			{
				MergePropsInPlace(dictionary, readOnlyDictionary2);
			}
		}
		return dictionary;
	}

public IReadOnlyDictionary<PropType, long> CalcNpcFriendshipProps(int npcClassId, int friendshipLevel)
	{
		PartnerNpcPropertyInfo npcInfo = source.GetNpc(npcClassId);
		if (npcInfo == null)
		{
			UnityEngine.Debug.LogError("Official partner property configuration or destination is absent.");
			return null;
		}
		return source.GetProps(npcInfo.LevelPropId, friendshipLevel);
	}

public IReadOnlyDictionary<PropType, long> CalcNpcTalentTreeProps(int npcClassId, IReadOnlyCollection<int> activeTalentPoint)
	{

		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();

		foreach (int item in activeTalentPoint)
		{
			PartnerNpcTalentPropertyInfo nodeMapPropInfo = source.GetNodeMapPropInfo(npcClassId, item);
			int propId = nodeMapPropInfo.PropId;
			int propLevel = nodeMapPropInfo.PropLevel;
			List<PropType> propTypes = nodeMapPropInfo.PropTypes;
			foreach (KeyValuePair<PropType, long> prop in source.GetProps(propId, propLevel))
			{
				PropType key = prop.Key;
				long value = prop.Value;
				if (propTypes.Contains(key))
				{
					AddProp(dictionary, key, value);
				}
			}
		}
		return dictionary;
	}

public void MergePropsInPlace(Dictionary<PropType, long> dest, IReadOnlyDictionary<PropType, long> add)
	{
		if (dest == null)
		{
			UnityEngine.Debug.LogError("Official partner property configuration or destination is absent.");
		}
		if (add == null || add.Count == 0)
		{
			return;
		}
		foreach (KeyValuePair<PropType, long> item in add)
		{
			if (!dest.TryGetValue(item.Key, out var value))
			{
				dest.Add(item.Key, item.Value);
			}
			else
			{
				dest[item.Key] = value + item.Value;
			}
		}
	}

public long GetRawProp(Dictionary<PropType, long> props, PropType type)
	{
		if (props == null || !props.TryGetValue(type, out var value))
		{
			return 0L;
		}
		return value;
	}

public void AddProp(Dictionary<PropType, long> props, PropType propType, long value)
	{
		props[propType] = GetRawProp(props, propType) + value;
	}
    }
}
