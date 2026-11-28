using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using B=Zjcs.Official.TreasureProperties;
using C=Zjcs.Official.TreasureCultivation;
using O=TreasurePropertyOriginal;
using P=Zjcs.Official.HomeIncomeRules.PropType;
using R=Zjcs.Official.HomeExperienceRules.SubRank;

public static partial class TreasureDescriptionsValidation
{
    private sealed class DescriptionServices : B.ITreasureDescriptionServices, B.ITreasurePropertyPlayer
    {
        public int Season, Rank=1; public bool InSlot;
        public C.TreasureState State;
        public B.ITreasurePropertyTables Tables=>catalog;
        public B.ITreasurePropertyPlayer Player=>this;
        public int SeasonId=>Season;
        public int SubRank=>Rank;
        public bool IsTreasureInSlot(int id)=>InSlot;
        public C.TreasureState GetTreasureState(int id)=>State;
        public int GetTreasureFinalLevel(int id)=>1;
        public long? GetCurrentCount(B.TreasureInfo info)=>null;
        public object Compact(long value)=>new NumberArgument(value);
        public object PropertyValue(int type,long value,bool includeUnit,bool includePropDesc,long targetValue,bool needSpaceBeforeValue)
            =>new PropertyArgument(type,value,includeUnit,includePropDesc,targetValue,needSpaceBeforeValue);
    }
    private sealed class NumberArgument
    {
        private readonly long value;
        public NumberArgument(long value){this.value=value;}
        public override string ToString()=>"number:"+value;
    }
    private sealed class PropertyArgument
    {
        private readonly int type; private readonly long value,target;
        private readonly bool unit,desc,space;
        public PropertyArgument(int type,long value,bool unit,bool desc,long target,bool space)
        {this.type=type;this.value=value;this.unit=unit;this.desc=desc;this.target=target;this.space=space;}
        public override string ToString()=>"property:"+type+":"+value+":"+unit+":"+desc+":"+target+":"+space;
    }
    private static string Arguments(object[] args)=>args==null?"<null>":string.Join("|",args.Select(v=>v?.ToString()??"<null argument>"));
    private static void SameDescription(B.TreasureDescriptionQuery query,DescriptionServices services,
        B.TreasureInfo local,O.TreasureInfo original,B.ITreasurePropProvider provider,O.ITreasurePropProvider oracle,
        B.TreasurePropDisplayOption mode)
    {
        O.PlanesInfoUtils.SeasonId=services.Season;
        O.MeManager.MainPlayer=new O.Player{SubRank=(R)services.Rank,InSlot=_=>services.InSlot,FinalLevel=_=>1};
        if(services.State!=null)
            O.MeManager.MainPlayer.Items.Add(local.ClassId,new O.PlayerItemDataWrap{Id=local.ClassId,
                Info=new O.ItemInfo{ClassId=local.ClassId,PartInfo=original},
                State=new O.TreasureState{Active=services.State.Active,Star=services.State.Star,HitCount=services.State.HitCount}});
        ClearReports();
        var actual=query.GenerateDesc(local.Effect,local,provider,mode);
        var expected=O.TreasureConfigUtils.GenerateDesc(original.Effect,original,oracle,(O.TreasurePropDisplayOption)(int)mode);
        Check(actual.Key==expected.Key&&Arguments(actual.Params)==Arguments(expected.Params),
            "Original description key, null-array contract, argument types/options/order and property values: "+local.ClassId+":"+services.Season+":"+mode);
        Check(errors.SequenceEqual(O.Diagnostics.Errors)&&warnings.SequenceEqual(O.Diagnostics.Warnings),
            "Original description lookup fallback and diagnostics: "+local.ClassId);
    }
    private static void CheckDescriptions()
    {
        var services=new DescriptionServices();var query=new B.TreasureDescriptionQuery(services);
        int[] ranks={payload.SubRanks.First().SubRank,payload.SubRanks.Last().SubRank};
        foreach(var info in catalog.Treasures)
        {
            var original=O.Parsers.Instance.TreasureInfoParser.Get(info.ClassId);
            original.TreasureData=new O.TreasureDataBase{Read=_=>0};
            foreach(int season in info.LevelPropId.Keys.Concat(new[]{-1,99999}).Distinct())
            {
                services.Season=season;
                foreach(bool slot in new[]{false,true})
                {
                    services.InSlot=slot;
                    foreach(B.TreasurePropDisplayOption mode in Enum.GetValues(typeof(B.TreasurePropDisplayOption)))
                        SameDescription(query,services,info,original,null,null,mode);
                    foreach(int rank in ranks)foreach(int level in new[]{1,16})foreach(int bless in new[]{0,2})
                    foreach(int star in new[]{0,info.StarMax})foreach(int hits in new[]{0,info.Effect.GetLevelMaxHitCount(level)+1})
                    {
                        services.Rank=rank;
                        var snapshot=new B.SnapshotTreasure{ItemId=info.ClassId,BaseLevel=level,BlessLevel=bless,SubRank=rank,InSlot=slot,
                            TreasureParam=new C.TreasureState{Star=star,HitCount=hits}};
                        var source=new O.SnapshotTreasure{ItemId=info.ClassId,BaseLevel=level,BlessLevel=bless,SubRank=(R)rank,InSlot=slot,
                            TreasureParam=new O.TreasureState{Star=star,HitCount=hits}};
                        var p=new B.TreasureSnapshotPropProvider(catalog,snapshot);var o=new O.TreasureSnapshotPropProvider(source);
                        SameDescription(query,services,info,original,p,o,B.TreasurePropDisplayOption.None);
                        SameDescription(query,services,info,original,new B.TreasureInSlotPropProvider(p),new O.TreasureInSlotPropProvider(o),B.TreasurePropDisplayOption.Level);
                        SameDescription(query,services,info,original,new B.TreasureNoSlotPropProvider(p),new O.TreasureNoSlotPropProvider(o),B.TreasurePropDisplayOption.Star);
                    }
                }
            }
            services.Season=0;services.Rank=ranks[0];
            foreach(bool owned in new[]{false,true})
            {
                services.State=owned?new C.TreasureState{Active=true,Star=info.StarMax,HitCount=17}:null;
                SameDescription(query,services,info,original,
                    new B.TreasureDummyPropProvider(catalog,services,info.ClassId),new O.TreasureDummyPropProvider(info.ClassId),
                    B.TreasurePropDisplayOption.None);
            }
            services.State=null;
        }
        // A provider can refer to another item. The key still comes from the
        // explicit info argument; task dispatch still comes from the effect argument.
        var task=catalog.Treasures.First(t=>t.Effect is B.TreasureEffectTask);
        var ordinary=catalog.Treasures.First(t=>t.Effect is B.TreasureEffectNone);
        var other=new B.SnapshotTreasure{ItemId=ordinary.ClassId,BaseLevel=1,SubRank=ranks[0],TreasureParam=new C.TreasureState{HitCount=7}};
        var otherO=new O.SnapshotTreasure{ItemId=ordinary.ClassId,BaseLevel=1,SubRank=(R)ranks[0],TreasureParam=new O.TreasureState{HitCount=7}};
        services.Season=0;
        SameDescription(query,services,task,O.Parsers.Instance.TreasureInfoParser.Get(task.ClassId),
            new B.TreasureSnapshotPropProvider(catalog,other),new O.TreasureSnapshotPropProvider(otherO),B.TreasurePropDisplayOption.None);
        // Preserve null parameters for an effect with no level property class.
        var noProps=new B.TreasureInfo(catalog){ClassId=ordinary.ClassId,LevelPropId=new Dictionary<int,int>{{0,0}},Effect=new B.TreasureEffectNone()};
        var noPropsO=new O.TreasureInfo{ClassId=ordinary.ClassId,LevelPropId=new Dictionary<int,int>{{0,0}},Effect=new O.TreasureEffectNone()};
        SameDescription(query,services,noProps,noPropsO,null,null,B.TreasurePropDisplayOption.None);
        // Returned arrays and query scratch data must survive subsequent calls.
        var first=query.GenerateDesc(ordinary.Effect,ordinary);string retained=Arguments(first.Params);
        var second=new B.TreasureDescriptionQuery(new DescriptionServices{Season=99999,InSlot=true});
        second.GenerateDesc(task.Effect,task);query.GenerateDesc(task.Effect,task);
        Check(Arguments(first.Params)==retained,"Independent owners and subsequent queries retain previous argument arrays.");
    }
}
