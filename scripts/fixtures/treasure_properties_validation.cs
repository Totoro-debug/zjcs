using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using B=Zjcs.Official.TreasureProperties;
using O=TreasurePropertyOriginal;
using H=Zjcs.Official.HomeLevelUpRules;
using P=Zjcs.Official.HomeIncomeRules.PropType;
using R=Zjcs.Official.HomeExperienceRules.SubRank;
using S=Zjcs.Official.TreasureSlots;

public static class TreasurePropertiesValidation
{
    private static int checks;
    private static readonly List<string> errors=new List<string>(),warnings=new List<string>();
    private static string Work=>Environment.GetEnvironmentVariable("ZJCS_TREASURE_PROPERTIES_OUTPUT");
    private delegate bool SourceRow(int classId,int level,out H.IPropDictionary value);
    private static SourceRow sourceRow;
    private static H.LevelPropParser binary;
    private static OfflineTreasurePropertyCatalog catalog;
    private static OfflineTreasurePropertyCatalog.Payload payload;
    private static JObject raw;
    public static void Run()
    {
        bool success=false;string failure=null;
        try
        {
            string project=Environment.GetEnvironmentVariable("ZJCS_TREASURE_PROPERTIES_PROJECT");
            if(string.IsNullOrEmpty(project)||Path.GetFullPath(Application.dataPath)!=Path.Combine(Path.GetFullPath(project),"Assets")||string.IsNullOrEmpty(Work))throw new InvalidOperationException("Use the selected independent source project.");
            Validate();success=true;
        }
        catch(Exception error){failure=error.ToString();Debug.LogException(error);}
        finally{binary?.Dispose();H.HomeLevelUpDiagnostics.Error=message=>System.Diagnostics.Trace.TraceError(message);}
        File.WriteAllText(Path.Combine(Work,"result.json"),JsonConvert.SerializeObject(new{success,checks,failure}));
        if(success)Debug.Log("TREASURE_PROPERTIES_PASSED: "+checks);
        EditorApplication.Exit(success?0:1);
    }
    private static void Check(bool result,string label)
    {checks++;if(!result)throw new InvalidOperationException(label);}
    private static List<P> Props(JToken value)=>value.Type==JTokenType.Null?null:value.ToObject<int[]>().Select(n=>(P)n).ToList();
    private static void Validate()
    {
        catalog=OfflineTreasurePropertyCatalog.LoadDefault(errors.Add,warnings.Add);
        payload=JsonUtility.FromJson<OfflineTreasurePropertyCatalog.Payload>(Resources.Load<TextAsset>("Treasure/treasure-properties").text);
        Check(OfflineTreasurePropertyCatalog.Parse(Resources.Load<TextAsset>("Treasure/treasure-properties").text.Replace(payload.Manifest,"verification-label"),errors.Add,warnings.Add).GetTreasure(61001).StarMax==catalog.GetTreasure(61001).StarMax,"Source labels remain provenance; runtime loads data by structure and item identity.");
        raw=JObject.Parse(File.ReadAllText(Path.Combine(Work,"raw-tables.json")));
        H.HomeLevelUpDiagnostics.Error=message=>O.Diagnostics.ReportError("binary:"+message);
        binary=new H.LevelPropParser(File.ReadAllBytes(Path.Combine(Work,"reference-level-prop.g.bytes")),raw["Defaults"].Select(r=>new H.LevelPropDefaultValue{ClassId=(int)r["ClassId"],DefaultValue=(long)r["DefaultValue"],PropTypes=Props(r["PropTypes"])}));
        sourceRow=(SourceRow)Delegate.CreateDelegate(typeof(SourceRow),binary,typeof(H.LevelPropParser).GetMethod("TryGetPropDict",BindingFlags.Instance|BindingFlags.NonPublic));
        var limits=new S.SystemLevelLimitInfoParser(raw["Limits"].Select(r=>new S.SystemLevelLimitInfo{SubRank=(S.SubRank)(int)r["SubRank"],PlayerLevel=(int)r["PlayerLevel"],PlayerBlessLevel=(int)r["PlayerBlessLevel"],TreasureLevel=(int)r["TreasureLevel"],TreasureBlessLevel=(int)r["TreasureBlessLevel"]}));
        O.Parsers.Instance=new O.Sources{LevelPropParser=new O.LevelPropParser{ReadRow=ReadSourceRow,ReadRange=binary.TryGetLevelRange},SystemLevelLimitInfoParser=new O.LimitLookup{Read=r=>limits.GetTreasureBaseLevelLimit((S.SubRank)(int)r)}};
        O.Parsers.GameSettings=new O.Settings{TreasureSlotPropAddRate=(float)raw["Settings"]["TreasureSlotPropAddRate"]};
        Check(catalog.TreasureSlotPropAddRate==O.Parsers.GameSettings.TreasureSlotPropAddRate&&catalog.ElementPropTypes.SequenceEqual(O.TreasureInfoParser.sElementProps.Select(p=>(int)p)),"Current original settings and element search order.");
        Check(catalog.Treasures.Count==raw["Treasures"].Count()&&catalog.Suits.Count==raw["Suits"].Count(),"Complete current treasure and suit catalog.");
        foreach(var row in raw["SubRanks"])
        {
            int rank=(int)row["SubRank"],prop=(int)row["TreasurePropBless"];
            O.Parsers.Instance.PlayerSubRankInfoParser.Values.Add((R)rank,new O.PlayerSubRankInfo{TreasurePropBless=prop});
            Check(catalog.GetSubRank(rank).TreasurePropBless==prop&&catalog.GetMaxTreasureBaseLevel(rank)==limits.GetTreasureBaseLevelLimit((S.SubRank)rank),"Every original subrank property class and base capacity: "+rank);
        }
        foreach(var row in raw["Treasures"])
        {
            int id=(int)row["ClassId"];O.ITreasureEffect effect=new O.TreasureEffectNone();
            if(row["RawEffect"] is JObject e&&(string)e["_type"]=="TreasureEffectTask")effect=new O.TreasureEffectTask{PerCount=(int)e["PerCount"],MaxHitCount=(int)e["MaxHitCount"],LevelHitCount=e["LevelHitCount"].Type==JTokenType.Null?null:e["LevelHitCount"].ToObject<List<int>>()};
            var info=new O.TreasureInfo{ClassId=id,StarMax=(int)row["StarMax"],StarScalePropId=(int)row["StarScalePropId"],StarPropTypes=Props(row["StarPropTypes"]),LevelPropTypes=Props(row["LevelPropTypes"]),EffectPropTypes=Props(row["EffectPropTypes"]),LevelPropId=row["LevelPropId"].ToObject<Dictionary<int,int>>(),Effect=effect};
            O.Parsers.Instance.TreasureInfoParser.Values.Add(id,info);O.Parsers.Instance.ItemInfoParser.Values.Add(id,new O.ItemInfo{ClassId=id,PartInfo=info});
            var local=catalog.GetTreasure(id);
            Check(local.StarMax==info.StarMax&&local.StarScalePropId==info.StarScalePropId&&local.TreasurePieceId==(int)row["TreasurePieceId"]&&SameTypes(local.StarPropTypes,info.StarPropTypes)&&SameTypes(local.LevelPropTypes,info.LevelPropTypes)&&SameTypes(local.EffectPropTypes,info.EffectPropTypes)&&local.LevelPropId.SequenceEqual(info.LevelPropId),"Decoded original property fields and order: "+id);
            foreach(int level in new[]{1,2,5,16,17,999})Check(local.Effect.GetLevelMaxHitCount(level)==effect.GetLevelMaxHitCount(level),"Original per-level hit maximum: "+id+":"+level);
            Check(local.Effect.GetMaxHitCount()==effect.GetMaxHitCount(),"Original absolute hit maximum: "+id);
        }
        CheckBinaryRows();CheckProperties();CheckBlessing();CheckSuits();CheckProviders();CheckNumericalBoundaries();CheckSavedPlayer();
    }
    private static bool ReadSourceRow(int id,int level,out IReadOnlyDictionary<P,long> row)
    {bool found=sourceRow(id,level,out var result);row=result;return found;}
    private static bool SameTypes(List<int> local,List<P> original)=>local==null?original==null:original!=null&&local.SequenceEqual(original.Select(p=>(int)p));
    private static void ClearReports(){errors.Clear();warnings.Clear();O.Diagnostics.Errors.Clear();O.Diagnostics.Warnings.Clear();}
    private static void Same(string label,Func<Dictionary<int,long>> local,Func<Dictionary<P,long>> original)
    {
        ClearReports();Dictionary<int,long> a=null;Dictionary<P,long> b=null;Type ae=null,be=null;
        try{a=local();}catch(Exception e){ae=e.GetType();}
        try{b=original();}catch(Exception e){be=e.GetType();}
        Check(ae==be&&(ae!=null||a.SequenceEqual(b.Select(p=>new KeyValuePair<int,long>((int)p.Key,p.Value))))&&errors.SequenceEqual(O.Diagnostics.Errors)&&warnings.SequenceEqual(O.Diagnostics.Warnings),label+" local="+JsonConvert.SerializeObject(a)+" original="+JsonConvert.SerializeObject(b)+" errors="+string.Join(",",errors)+" / "+string.Join(",",O.Diagnostics.Errors)+" exception="+ae+" / "+be);
    }
    private static void CheckBinaryRows()
    {
        foreach(var group in payload.Classes)
        {
            Check(binary.TryGetLevelRange(group.ClassId,out var range)&&range==(group.Min,group.Max),"Original binary class range: "+group.ClassId);
            foreach(int level in Enumerable.Range(group.Min,group.Max-group.Min+1).Concat(new[]{group.Min-1,group.Max+1,group.Max+100}))
            {
                ClearReports();var local=catalog.GetProps(group.ClassId,level);var original=O.Parsers.Instance.LevelPropParser.GetProps(group.ClassId,level);
                Check((local==null?original==null:original!=null&&local.SequenceEqual(original.Select(p=>new KeyValuePair<int,long>((int)p.Key,p.Value))))&&errors.SequenceEqual(O.Diagnostics.Errors),"Decoded binary row/default/order and range branch: "+group.ClassId+":"+level);
            }
        }
    }
    private static void CheckProperties()
    {
        var calculator=new B.TreasurePropertyCalculator(catalog);O.PlanesInfoUtils.SeasonId=0;
        foreach(var local in catalog.Treasures)
        {
            var original=O.Parsers.Instance.TreasureInfoParser.Get(local.ClassId);
            foreach(int star in Enumerable.Range(0,local.StarMax+1))foreach(int level in new[]{1,5,16,17})foreach(bool inSlot in new[]{false,true})
            {
                var b=B.TreasureCalcPropData.CreateCalcPropDataByBaseLevel(catalog,level);var o=O.TreasureCalcPropData.CreateCalcPropDataByBaseLevel(level);
                foreach(int hits in new[]{0,1,local.Effect.GetLevelMaxHitCount(level),local.Effect.GetLevelMaxHitCount(level)+1}.Distinct())
                    Same("Official property partitions/truncation/hit count/slot bonus "+local.ClassId+":"+star+":"+level+":"+hits+":"+inSlot,
                        ()=>{var result=new Dictionary<int,long>{{999999,7}};calculator.CalcTreasureProps(local,star,b,hits,result,inSlot,0);return result;},
                        ()=>{var result=new Dictionary<P,long>{{(P)999999,7}};O.BattleFormula.Instance.CalcTreasureProps(original,star,o,hits,result,inSlot,0);return result;});
            }
            foreach(int season in local.LevelPropId.Keys.Concat(new[]{-1,99999}))
            {
                var b=B.TreasureCalcPropData.CreateCalcPropDataByBaseLevel(catalog,1);var o=O.TreasureCalcPropData.CreateCalcPropDataByBaseLevel(1);
                Same("Original season selection and fallback: "+local.ClassId+":"+season,()=>calculator.CalcTreasureStarPartProps(local,local.StarMax,b,1,true,season),()=>O.BattleFormula.Instance.CalcTreasureStarPartProps(original,original.StarMax,o,1,true,season));
                Same("Original level partition: "+local.ClassId+":"+season,()=>calculator.CalcTreasureLevelPartProps(local,0,b,1,false,season),()=>O.BattleFormula.Instance.CalcTreasureLevelPartProps(original,0,o,1,false,season));
            }
            var query=new B.TreasurePropertyQueries(catalog,()=>0);var player=new Player{Level=1,Slot=true};var sourcePlayer=new O.Player{FinalLevel=_=>1,InSlot=_=>true};
            var localData=B.TreasureCalcPropData.CreateCalcPropDataByBaseLevel(catalog,1);var sourceData=O.TreasureCalcPropData.CreateCalcPropDataByBaseLevel(1);
            Same("Original element property search order: "+local.ClassId,()=>{var p=query.GetElementPropInfo(local,player,0,localData,1);return new Dictionary<int,long>{{p.PropType,p.PropValue}};},()=>{var p=O.TreasurePropInfoConfigUtils.GetElementPropInfo(original,sourcePlayer,0,sourceData,1);return new Dictionary<P,long>{{p.PropType,p.PropValue}};});
            Same("Original non-element effect search order: "+local.ClassId,()=>{var p=query.GetEffectPropInfo(local,player,0,localData,1,false);return new Dictionary<int,long>{{p.PropType,p.PropValue}};},()=>{var p=O.TreasurePropInfoConfigUtils.GetEffectPropInfo(original,sourcePlayer,0,sourceData,1,false);return new Dictionary<P,long>{{p.PropType,p.PropValue}};});
            Same("Original star/level property selection: "+local.ClassId,()=>{var p=new Dictionary<int,long>();query.GetStarProps(local,player,0,localData,1,p);return p;},()=>{var p=new Dictionary<P,long>();O.TreasurePropInfoConfigUtils.GetStarProps(original,sourcePlayer,0,sourceData,1,p);return p;});
        }
        Debug.Log("Treasure property arithmetic cases: "+checks);
    }
    private static void CheckBlessing()
    {
        var calculator=new B.TreasurePropertyCalculator(catalog);
        var representative=payload.SubRanks.GroupBy(r=>(r.TreasurePropBless,catalog.GetMaxTreasureBaseLevel(r.SubRank))).Select(g=>g.First()).ToArray();
        foreach(var rank in representative)foreach(var local in catalog.Treasures)foreach(int bless in new[]{0,1,2,20})
        {
            int baseLevel=Math.Max(1,catalog.GetMaxTreasureBaseLevel(rank.SubRank));
            ClearReports();var b=B.TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(catalog,baseLevel+bless,baseLevel);var o=O.TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(baseLevel+bless,baseLevel);
            b.AssignBlessLevelPropDict(rank.SubRank);o.AssignBlessLevelPropDict((R)rank.SubRank);
            Check(b.HasBlessLevelPropData()==o.HasBlessLevelPropData()&&errors.SequenceEqual(O.Diagnostics.Errors),"Original blessing assignment and fallback diagnostics.");
            var original=O.Parsers.Instance.TreasureInfoParser.Get(local.ClassId);
            Same("Original blessing factor: "+local.ClassId+":"+rank.SubRank+":"+bless,()=>{var result=new Dictionary<int,long>();calculator.CalcTreasureProps(local,local.StarMax,b,1,result,true,0);return result;},()=>{var result=new Dictionary<P,long>();O.BattleFormula.Instance.CalcTreasureProps(original,original.StarMax,o,1,result,true,0);return result;});
            foreach(int type in local.StarPropTypes.Concat(local.LevelPropTypes))Check(b.GetBlessScaleFactor(type)==o.GetBlessScaleFactor((P)type),"Original per-property blessing scale.");
            b.ResetCalcData(1,1,rank.SubRank);o.ResetCalcData(1,1,(R)rank.SubRank);Check(!b.HasBlessLevelPropData()&&!o.HasBlessLevelPropData(),"Reset removes earlier blessing cache.");
            b.Clear();o.Clear();Check(b.BaseLevel==o.BaseLevel&&b.FinalLevel==o.FinalLevel&&!b.HasBlessLevelPropData(),"Clear resets original calculation state.");
        }
    }
    private static void CheckSuits()
    {
        var calc=new B.TreasurePropertyCalculator(catalog);
        foreach(var row in raw["Suits"])
        {
            int id=(int)row["ClassId"];var local=catalog.GetSuit(id);var types=(JArray)row["PropTypes"];int i=0;
            var original=new O.TreasureSuitInfo{Treasures=row["Treasures"].ToObject<List<int>>(),PropData=new Dictionary<O.TreasureSuitPropCondition,(int,List<P>)>()};
            foreach(var prop in ((JObject)row["Props"]).Properties())original.PropData.Add((O.TreasureSuitPropCondition)int.Parse(prop.Name),((int)prop.Value,Props(types[i++])));
            Check(local.Treasures.SequenceEqual(original.Treasures)&&local.PropData.Select(p=>((int)p.Key,p.Value.PropId,string.Join(",",p.Value.PropTypes))).SequenceEqual(original.PropData.Select(p=>((int)p.Key,p.Value.PropId,string.Join(",",p.Value.PropTypes.Select(p2=>(int)p2))))),"Original suit property pairing/order: "+id);
            foreach(int starMode in new[]{-1,0,2,3,999})foreach(bool incomplete in new[]{false,true})
            {
                var stars=new Dictionary<int,int>();foreach(int treasure in local.Treasures)if(starMode>=0)stars.Add(treasure,starMode==999?catalog.GetTreasure(treasure).StarMax:starMode);
                if(incomplete&&stars.Count>0)stars.Remove(stars.Keys.First());
                stars.Add(999999,999);
                Check(local.GetActivedCondition(stars).Select(c=>(int)c).SequenceEqual(original.GetActivedCondition(stars).Select(c=>(int)c)),"Original suit conditions, thresholds and unrelated-item filtering: "+id);
                var items=stars.Where(p=>O.Parsers.Instance.ItemInfoParser.Values.ContainsKey(p.Key)).Select(p=>new O.PlayerItemDataWrap{Id=p.Key,Info=O.Parsers.Instance.ItemInfoParser.Get(p.Key),State=new O.TreasureState{Active=true,Star=p.Value}}).ToArray();
                Same("Original suit values and merge into existing result: "+id,()=>{var result=new Dictionary<int,long>{{999999,7}};calc.CalcTreasureSuitProps(local,stars,result);return result;},()=>{var result=new Dictionary<P,long>{{(P)999999,7}};O.BattleFormula.Instance.CalcTreasureSuitProps(new O.PlayerItemDataWrap{Info=new O.ItemInfo{PartInfo=original}},items,result);return result;});
            }
        }
    }
    private sealed class Player:B.ITreasurePropertyPlayer
    {
        public int SubRank{get;set;}public int Level;public bool Slot;public long? Count;public Zjcs.Official.TreasureCultivation.TreasureState State;
        public Zjcs.Official.TreasureCultivation.TreasureState GetTreasureState(int id)=>State;
        public bool IsTreasureInSlot(int id)=>Slot;public int GetTreasureFinalLevel(int id)=>Level;public long? GetCurrentCount(B.TreasureInfo info)=>Count;
    }
    private static string Provider(B.ITreasurePropProvider p)=>string.Join(":",p.Info().ClassId,p.Active(),p.Star(),p.Level(),p.BaseLevel(),p.BlessLevel(),p.InSlot(),p.SubRank(),p.GetHitCount());
    private static string Provider(O.ITreasurePropProvider p)=>string.Join(":",p.Info().ClassId,p.Active(),p.Star(),p.Level(),p.BaseLevel(),p.BlessLevel(),p.InSlot(),(int)p.SubRank(),p.GetHitCount());
    private static void CheckProviders()
    {
        foreach(var info in catalog.Treasures)
        {
            int rank=payload.SubRanks.Last().SubRank,cap=catalog.GetMaxTreasureBaseLevel(rank);
            foreach(bool owned in new[]{false,true})foreach(int level in new[]{1,cap,cap+2})foreach(bool slot in new[]{false,true})foreach(long? count in new long?[]{null,57})
            {
                var p=new Player{SubRank=rank,Level=level,Slot=slot,Count=count,State=owned?new Zjcs.Official.TreasureCultivation.TreasureState{Active=true,Star=info.StarMax,HitCount=13}:null};
                var q=new O.Player{SubRank=(R)rank,FinalLevel=_=>level,InSlot=_=>slot};O.MeManager.MainPlayer=q;
                var oi=O.Parsers.Instance.TreasureInfoParser.Get(info.ClassId);oi.TreasureData=count.HasValue?new O.TreasureDataBase{Read=_=>count.Value}:null;
                if(owned)q.Items.Add(info.ClassId,new O.PlayerItemDataWrap{Id=info.ClassId,Info=O.Parsers.Instance.ItemInfoParser.Get(info.ClassId),State=new O.TreasureState{Active=true,Star=info.StarMax,HitCount=13}});
                var local=new B.TreasureDummyPropProvider(catalog,p,info.ClassId);var original=new O.TreasureDummyPropProvider(info.ClassId);
                Check(Provider(local)==Provider(original),"Original live provider, cap and missing-parameter/count fallback: "+info.ClassId);
                Check(Provider(new B.TreasureInSlotPropProvider(local))==Provider(new O.TreasureInSlotPropProvider(original))&&Provider(new B.TreasureNoSlotPropProvider(local))==Provider(new O.TreasureNoSlotPropProvider(original)),"Original forced/no-slot wrappers delegate all other values.");
                var snapshot=new B.SnapshotTreasure{ItemId=info.ClassId,BaseLevel=level,BlessLevel=2,SubRank=rank,InSlot=slot,TreasureParam=new Zjcs.Official.TreasureCultivation.TreasureState{Active=true,Star=info.StarMax,HitCount=29}};
                var sourceSnapshot=new O.SnapshotTreasure{ItemId=info.ClassId,BaseLevel=level,BlessLevel=2,SubRank=(R)rank,InSlot=slot,TreasureParam=new O.TreasureState{Active=true,Star=info.StarMax,HitCount=29}};
                Check(Provider(new B.TreasureSnapshotPropProvider(catalog,snapshot))==Provider(new O.TreasureSnapshotPropProvider(sourceSnapshot)),"Original snapshot provider keeps its own levels and inactive flag.");
            }
        }
    }
    private static void CheckNumericalBoundaries()
    {
        // Original sparse parser behavior can be exercised independently of packaged data.
        var test=JsonConvert.DeserializeObject<JObject>(JsonUtility.ToJson(payload));
        test["Classes"]=JArray.Parse("[{\"ClassId\":987654,\"Min\":1,\"Max\":3,\"Rows\":[{\"Level\":1,\"Props\":[{\"Type\":1,\"Value\":7}]},{\"Level\":3,\"Props\":[{\"Type\":1,\"Value\":19}]}]}]");
        test["Defaults"]=JArray.Parse("[{\"ClassId\":987654,\"PropTypes\":[1,2,3],\"DefaultValue\":10000}]");
        var sparse=OfflineTreasurePropertyCatalog.Parse(test.ToString(),errors.Add,warnings.Add);var old=O.Parsers.Instance.LevelPropParser;
        var smallBinary=new H.LevelPropParser(SparseBinary(),new[]{new H.LevelPropDefaultValue{ClassId=987654,DefaultValue=10000,PropTypes=new List<P>{(P)1,(P)2,(P)3}}});
        var smallRow=(SourceRow)Delegate.CreateDelegate(typeof(SourceRow),smallBinary,typeof(H.LevelPropParser).GetMethod("TryGetPropDict",BindingFlags.Instance|BindingFlags.NonPublic));
        O.Parsers.Instance.LevelPropParser=new O.LevelPropParser{ReadRow=(int c,int l,out IReadOnlyDictionary<P,long> values)=>{bool found=smallRow(c,l,out var row);values=row;return found;},ReadRange=smallBinary.TryGetLevelRange};
        try
        {
            foreach(int id in new[]{987654,987655})foreach(int level in new[]{0,1,2,3,4})
            {ClearReports();var a=sparse.GetProps(id,level);var b=O.Parsers.Instance.LevelPropParser.GetProps(id,level);Check((a==null?b==null:b!=null&&a.SequenceEqual(b.Select(p=>new KeyValuePair<int,long>((int)p.Key,p.Value))))&&errors.SequenceEqual(O.Diagnostics.Errors),"Sparse middle rows stay missing; only levels above the last row reuse the final row.");}
        }
        finally{O.Parsers.Instance.LevelPropParser=old;smallBinary.Dispose();}
        Check(CalculateVector(16777219,10000,false,1,false,1)==20132663,"Original slot bonus uses single-precision multiplication before integer conversion.");
        Check(CalculateVector(3,10000,true,1,false,1)==6,"Star and level partitions each truncate their own slot bonus before merging.");
        Check(CalculateVector(-9,3333,false,1,false,1)==-2,"Original multiplication truncates negative values toward zero.");
        Check(CalculateVector(100,10000,false,0,true,10)==0,"Zero supplied hit count stays zero.");
        Check(CalculateVector(100,10000,false,11,true,10)==132,"Core formula uses the supplied hit count without silently capping it.");
        bool zeroMaximum=false;try{CalculateVector(100,10000,false,1,true,0);}catch(DivideByZeroException){zeroMaximum=true;}
        Check(zeroMaximum,"Invalid zero hit maximum retains the original arithmetic exception.");
    }
    private static byte[] SparseBinary()
    {
        using(var data=new MemoryStream())
        {
            using(var writer=new BinaryWriter(data))
            {
                writer.Write(1280328262u);writer.Write((ushort)1);writer.Write(14);writer.Write(22);
                writer.Write(1);writer.Write((ushort)1);writer.Write((ushort)1);
                writer.Write(1);writer.Write(987654);writer.Write(1);writer.Write(3);writer.Write(50);writer.Write(-1);writer.Write(52);
                writer.Write(new byte[]{0,14,0,38});writer.Flush();return data.ToArray();
            }
        }
    }
    private static long CalculateVector(long value,long scale,bool duplicate,int hits,bool effect,int maximum)
    {
        var test=JObject.Parse(JsonUtility.ToJson(payload));
        test["Classes"]=JArray.FromObject(new[]{
            new{ClassId=987654,Min=1,Max=1,Rows=new[]{new{Level=1,Props=new[]{new{Type=1,Value=value}}}}},
            new{ClassId=987653,Min=0,Max=0,Rows=new[]{new{Level=0,Props=new[]{new{Type=1,Value=scale}}}}}
        });
        test["Treasures"]=JArray.FromObject(new[]{new{ClassId=987650,TreasurePieceId=987651,StarMax=6,StarScalePropId=987653,
            StarPropTypes=duplicate?new[]{1}:Array.Empty<int>(),LevelPropTypes=new[]{1},EffectPropTypes=effect?new[]{1}:Array.Empty<int>(),
            LevelPropId=new[]{new{SeasonId=0,PropId=987654}},Effect=new{Kind=effect?"TreasureEffectTask":"TreasureEffectNone",PerCount=1,MaxHitCount=maximum,LevelHitCount=(int[])null}}});
        var tables=OfflineTreasurePropertyCatalog.Parse(test.ToString(),errors.Add,warnings.Add);var calculator=new B.TreasurePropertyCalculator(tables);var data=B.TreasureCalcPropData.CreateCalcPropDataByBaseLevel(tables,1);
        var original=new O.TreasureInfo{ClassId=987650,StarMax=6,StarScalePropId=987653,StarPropTypes=duplicate?new List<P>{(P)1}:new List<P>(),LevelPropTypes=new List<P>{(P)1},EffectPropTypes=effect?new List<P>{(P)1}:new List<P>(),LevelPropId=new Dictionary<int,int>{{0,987654}},Effect=effect?(O.ITreasureEffect)new O.TreasureEffectTask{PerCount=1,MaxHitCount=maximum}:new O.TreasureEffectNone()};
        var old=O.Parsers.Instance.LevelPropParser;
        O.Parsers.Instance.LevelPropParser=new O.LevelPropParser{ReadRow=(int c,int l,out IReadOnlyDictionary<P,long> row)=>{row=new Dictionary<P,long>{{(P)1,c==987654?value:scale}};return true;},ReadRange=(int c,out (int,int) range)=>{range=c==987654?(1,1):(0,0);return true;}};
        Func<Dictionary<int,long>> local=()=>{var result=new Dictionary<int,long>();calculator.CalcTreasureProps(tables.GetTreasure(987650),0,data,hits,result,true,0);return result;};
        try
        {
            Same("Independent arithmetic boundary vector",local,()=>{var result=new Dictionary<P,long>();O.BattleFormula.Instance.CalcTreasureProps(original,0,O.TreasureCalcPropData.CreateCalcPropDataByBaseLevel(1),hits,result,true,0);return result;});
            return local()[1];
        }
        finally{O.Parsers.Instance.LevelPropParser=old;}
    }
    private static void CheckSavedPlayer()
    {
        string directory=Path.Combine(Work,"isolated-"+Guid.NewGuid().ToString("N"));var creation=Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.OpenDefault(directory);
        string id=creation.CreateSlot();var draft=creation.OpenDraft(id);draft.name="属性查询";
        Check(creation.TryCreateCharacter(id,draft,out string error)&&creation.TrySetInitialWeapon(id,Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.SwordInitialWeaponType,out error),"Create isolated actual profile: "+error);
        var profiles=new Zjcs.Offline.State.ProfileSlotCatalog(directory);var profile=OfflineMainlineProgressSession.OpenCharacter(profiles,id);var slots=new OfflineTreasureSlotService(profile,OfflineTreasureSlotCatalog.LoadDefault());
        const int treasure=61001;
        Check(profile.ApplyInventoryTransaction("property-seed",s=>{s.Player.Inventory.Add(new OfflineItemStack{ItemId=treasure,Count=1});s.Player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=treasure,Active=true,Star=2,HitCount=13});}).IsApplied,"Seed actual owned treasure.");
        Check(slots.SetupTreasureSlot(treasure,0,"property-equip").IsApplied,"Equip through actual shared slot transaction.");
        var graph=slots.Snapshot();graph.GetTreasureSlotData((S.ElementType)slots.Snapshot().GetItemById(treasure).Info.ElementType,0).BaseLevel=8;
        Check(profile.ApplyInventoryTransaction("property-training",s=>s.Player.CharacterProperties.TreasureSlots=OfflineTreasureSlotService.Capture(graph)).IsApplied,"Persist actual physical slot training.");
        string file=Path.Combine(profiles.GetSlotDirectory(id),"city-save.json");byte[] before=File.ReadAllBytes(file);int countCalls=0;
        var player=new OfflineTreasurePropertyPlayer(profile,slots,_=>{countCalls++;return 77;});var provider=new B.TreasureDummyPropProvider(catalog,player,treasure);
        Check(provider.Active()&&provider.Star()==2&&provider.GetHitCount()==13&&provider.Level()==8&&provider.InSlot()&&countCalls==0,"Read real saved state and physical training; owned hit count skips fallback.");
        var parameters=player.GetTreasureState(treasure);parameters.Star=999;Check(provider.Star()==2,"Read-only player projection returns detached parameter state.");
        var calcData=B.TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(catalog,provider.Level(),provider.BaseLevel());calcData.AssignBlessLevelPropDict(provider.SubRank());var calculator=new B.TreasurePropertyCalculator(catalog);
        var values=new Dictionary<int,long>();calculator.CalcTreasureProps(provider.Info(),provider.Star(),calcData,provider.GetHitCount(),values,provider.InSlot(),0);Check(values.Count>0&&before.SequenceEqual(File.ReadAllBytes(file)),"Actual property queries return values without rewriting the role save.");
        var external=OfflineMainlineProgressSession.OpenCharacter(profiles,id);Check(external.ApplyInventoryTransaction("property-external",s=>{var t=s.Player.CharacterProperties.Treasures.Single(r=>r.ClassId==treasure);t.Star=3;t.HitCount=29;}).IsApplied,"Another owner commits real cultivation state.");profile.Reload();
        Check(provider.Star()==3&&provider.GetHitCount()==29,"Original live provider observes the enclosing owner's reload.");
        var cold=OfflineMainlineProgressSession.OpenCharacter(profiles,id);var coldSlots=new OfflineTreasureSlotService(cold,OfflineTreasureSlotCatalog.LoadDefault());var coldProvider=new B.TreasureDummyPropProvider(catalog,new OfflineTreasurePropertyPlayer(cold,coldSlots,_=>null),treasure);
        Check(Provider(provider)==Provider(coldProvider),"Cold role reopen restores the same actual per-item property inputs.");
    }
}
