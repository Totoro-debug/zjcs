using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Zjcs.Official.TreasureProperties;
using PropType=System.Int32;
using S=Zjcs.Official.TreasureSlots;

namespace Zjcs.Offline.Progression
{
    // Selected original rows, indices and sparse lookup behavior. Runtime never reads extraction caches.
    public sealed class OfflineTreasurePropertyCatalog:ITreasurePropertyTables
    {
        [Serializable] public sealed class Prop {public int Type;public long Value;}
        [Serializable] public sealed class LevelRow {public int Level;public Prop[] Props;}
        [Serializable] public sealed class PropClass {public int ClassId,Min,Max;public LevelRow[] Rows;}
        [Serializable] public sealed class SeasonProp {public int SeasonId,PropId;}
        [Serializable] public sealed class EffectRow {public string Kind;public int PerCount,MaxHitCount;public int[] LevelHitCount;}
        [Serializable] public sealed class TreasureRow {public int ClassId,TreasurePieceId,StarMax,StarScalePropId;public int[] StarPropTypes,LevelPropTypes,EffectPropTypes;public SeasonProp[] LevelPropId;public EffectRow Effect;}
        [Serializable] public sealed class SuitProp {public int Condition,PropId;public int[] PropTypes;}
        [Serializable] public sealed class SuitRow {public int ClassId;public int[] Treasures;public SuitProp[] Props;}
        [Serializable] public sealed class SubRankRow {public int SubRank,TreasurePropBless;}
        [Serializable] public sealed class LimitRow {public int SubRank,PlayerLevel,PlayerBlessLevel,TreasureLevel,TreasureBlessLevel;}
        [Serializable] public sealed class DefaultRow {public int ClassId;public int[] PropTypes;public long DefaultValue;}
        [Serializable] public sealed class Payload
        {public string Manifest;public float TreasureSlotPropAddRate;public int[] ElementPropTypes;public TreasureRow[] Treasures;public SuitRow[] Suits;public SubRankRow[] SubRanks;public LimitRow[] Limits;public DefaultRow[] Defaults;public PropClass[] Classes;}
        public string Manifest{get;}
        public float TreasureSlotPropAddRate{get;}
        public IReadOnlyList<int> ElementPropTypes{get;}
        public IReadOnlyList<TreasureInfo> Treasures{get;}
        public IReadOnlyList<TreasureSuitInfo> Suits{get;}
        private readonly Dictionary<int,TreasureInfo> treasures=new Dictionary<int,TreasureInfo>();
        private readonly Dictionary<int,TreasureSuitInfo> suits=new Dictionary<int,TreasureSuitInfo>();
        private readonly Dictionary<int,PlayerSubRankInfo> subRanks=new Dictionary<int,PlayerSubRankInfo>();
        private readonly Dictionary<int,(int,int)> ranges=new Dictionary<int,(int,int)>();
        private readonly Dictionary<(int,int),IReadOnlyDictionary<int,long>> rows=new Dictionary<(int,int),IReadOnlyDictionary<int,long>>();
        private readonly S.SystemLevelLimitInfoParser limits;
        private readonly Action<string> error,warning;
        public static OfflineTreasurePropertyCatalog LoadDefault(Action<string> error=null,Action<string> warning=null)
        {
            var resource=Resources.Load<TextAsset>("Treasure/treasure-properties");
            if(!resource)throw new InvalidOperationException("Original treasure property configuration is absent.");
            return Parse(resource.text,error,warning);
        }
        public static OfflineTreasurePropertyCatalog Parse(string json,Action<string> error=null,Action<string> warning=null)
        {
            var data=JsonUtility.FromJson<Payload>(json);
            if(data==null||data.Treasures==null||data.Suits==null||data.SubRanks==null||data.Classes==null||data.Defaults==null||data.Limits==null||data.ElementPropTypes==null)
                throw new FormatException("Invalid original treasure property payload.");
            return new OfflineTreasurePropertyCatalog(data,error??Debug.LogError,warning??Debug.LogWarning);
        }
        private OfflineTreasurePropertyCatalog(Payload data,Action<string> error,Action<string> warning)
        {
            this.error=error;this.warning=warning;Manifest=data.Manifest;TreasureSlotPropAddRate=data.TreasureSlotPropAddRate;
            ElementPropTypes=Array.AsReadOnly((int[])data.ElementPropTypes.Clone());
            limits=new S.SystemLevelLimitInfoParser(data.Limits.Select(r=>new S.SystemLevelLimitInfo{SubRank=(S.SubRank)r.SubRank,PlayerLevel=r.PlayerLevel,PlayerBlessLevel=r.PlayerBlessLevel,TreasureLevel=r.TreasureLevel,TreasureBlessLevel=r.TreasureBlessLevel}));
            foreach(var rank in data.SubRanks)subRanks.Add(rank.SubRank,new PlayerSubRankInfo{SubRank=rank.SubRank,TreasurePropBless=rank.TreasurePropBless});
            var defaults=data.Defaults.ToDictionary(r=>r.ClassId);
            foreach(var group in data.Classes)
            {
                ranges.Add(group.ClassId,(group.Min,group.Max));
                foreach(var row in group.Rows)
                {
                    var values=new Dictionary<int,long>();foreach(var prop in row.Props)values.Add(prop.Type,prop.Value);
                    if(defaults.TryGetValue(group.ClassId,out var fallback))foreach(int type in fallback.PropTypes)if(!values.ContainsKey(type))values.Add(type,fallback.DefaultValue);
                    rows.Add((group.ClassId,row.Level),new ReadOnlyDictionary<int,long>(values));
                }
            }
            foreach(var r in data.Treasures)
            {
                ITreasureEffect effect;
                if(r.Effect.Kind=="TreasureEffectNone")effect=new TreasureEffectNone();
                else if(r.Effect.Kind=="TreasureEffectTask")effect=new TreasureEffectTask{PerCount=r.Effect.PerCount,MaxHitCount=r.Effect.MaxHitCount,LevelHitCount=r.Effect.LevelHitCount?.ToList()};
                else throw new FormatException("Unbound original treasure effect: "+r.Effect.Kind);
                treasures.Add(r.ClassId,new TreasureInfo(this){ClassId=r.ClassId,TreasurePieceId=r.TreasurePieceId,StarMax=r.StarMax,StarScalePropId=r.StarScalePropId,StarPropTypes=r.StarPropTypes?.ToList(),LevelPropTypes=r.LevelPropTypes?.ToList(),EffectPropTypes=r.EffectPropTypes?.ToList(),LevelPropId=r.LevelPropId.ToDictionary(p=>p.SeasonId,p=>p.PropId),Effect=effect});
            }
            Treasures=Array.AsReadOnly(treasures.Values.ToArray());
            foreach(var r in data.Suits)suits.Add(r.ClassId,new TreasureSuitInfo(this){ClassId=r.ClassId,Treasures=r.Treasures.ToList(),PropData=r.Props.ToDictionary(p=>(TreasureSuitPropCondition)p.Condition,p=>(p.PropId,p.PropTypes.ToList()))});
            Suits=Array.AsReadOnly(suits.Values.ToArray());
        }
        public TreasureInfo GetTreasure(int id)=>treasures.TryGetValue(id,out var value)?value:null;
        public TreasureSuitInfo GetSuit(int id)=>suits.TryGetValue(id,out var value)?value:null;
        public PlayerSubRankInfo GetSubRank(int subRank)=>subRanks.TryGetValue(subRank,out var value)?value:null;
        public int GetMaxTreasureBaseLevel(int subRank)=>limits.GetTreasureBaseLevelLimit((S.SubRank)subRank);
        public bool TryGetLevelRange(int classId,out (int,int) levelRange)=>ranges.TryGetValue(classId,out levelRange);
        private bool TryGetPropDict(int classId,int level,out IReadOnlyDictionary<int,long> value)=>rows.TryGetValue((classId,level),out value);
        public void ReportError(string method)=>error(method);
        public void ReportWarning(string method)=>warning(method);
        public IReadOnlyDictionary<PropType, long> GetProps(int classId, int level)
	{
		if (!TryGetPropDict(classId, level, out var propDict))
		{
			if (!TryGetLevelRange(classId, out var levelRange))
			{

				this.ReportError("GetConfigProps");
				return null;
			}
			if (level < levelRange.Item2)
			{

				this.ReportError("GetConfigProps");
				return null;
			}
			TryGetPropDict(classId, levelRange.Item2, out propDict);
		}
		return propDict;
	}
public long GetProp(int classId, int level, PropType propType)
	{
		IReadOnlyDictionary<PropType, long> configProps = GetProps(classId, level);
		if (configProps == null)
		{

			this.ReportError("GetProp");
			return 0L;
		}
		configProps.TryGetValue(propType, out var value);
		return value;
	}

    }
}
