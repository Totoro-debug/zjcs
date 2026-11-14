using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.HomePlayerHealthUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Skills;
using Zjcs.Official.HomeExperienceRules;

public static class HomeNoBlessProfileChecks
{
    static void Require(bool condition,string message){if(!condition)throw new Exception("No-blessing: "+message);}
    public static void Run(OfflineMainlineProgressSession profile, OfflineCharacterProgressionService growth)
    {
        var rules=OfflineHomeNoBlessConfiguration.LoadDefault();
        Require(rules.LimitRowCount==1124 && rules.Resonances.MinLevel==1 && rules.Resonances.MaxLevel==240,"complete selected configuration");
        string unchanged=JsonUtility.ToJson(profile.Save);
        var initial=new OfflineHomeNoBlessPlayerView(growth.Player,Rank.Norank,0,rules);
        var initialProps=initial.Evaluate(growth.PropertyCalculator,profile.Save.World,0);
        Require(initial.Player.Level==1 && initial.Inputs.PlanesRank==2 && initial.Inputs.SeasonId==-1
            && initial.Inputs.EquipResonance.Count==0 && initialProps.CombatRating>0,"actual fresh profile reaches original provider and shared calculator");
        Require(initialProps.CombatRating==growth.PropertyCalculator.Evaluate(growth.Player,profile.Save.World,0).CombatRating,"fresh unblessed player has equal real ratings");
        Require(JsonUtility.ToJson(profile.Save)==unchanged,"projection and real shared calculation do not change the profile");
        var json=JObject.Parse(Resources.Load<TextAsset>("Home/no-bless-properties").text);
        var rows=json["limits"].Cast<JObject>().ToArray();
        foreach(var group in rows.GroupBy(row=>(int)row["SubRank"]))
        {
            int subRank=group.Key,rank=(int)((SubRank)subRank).ToRank();
            int cap=group.Where(row=>(int)row["PlayerBlessLevel"]==0).Max(row=>(int)row["PlayerLevel"]);
            int equipCap=rows.Where(row=>(int)((SubRank)(int)row["SubRank"]).ToRank()==rank && (int)row["EquipBlessLevel"]==0).Max(row=>(int)row["EquipLevel"]);
            int skillCap=rows.Where(row=>(int)((SubRank)(int)row["SubRank"]).ToRank()==rank && (int)row["SkillBlessLevel"]==0).Max(row=>(int)row["SkillLevel"]);
            Require(rules.LevelLimits.GetPlayerBaseLevelLimit((SubRank)subRank)==cap
                && rules.LevelLimits.GetEquipBaseLevelLimit((SubRank)subRank)==equipCap
                && rules.LevelLimits.GetSkillBaseLevelLimit((SubRank)subRank)==skillCap,"independent selected base-limit aggregation for every subrank");
            foreach(var row in group)
                Require(rules.LevelLimits.GetPetLevelLimit((SubRank)subRank,(int)row["PlayerLevel"]+(int)row["PlayerBlessLevel"])
                    ==(int)row["PetLevel"]+(int)row["PetBlessLevel"],"every selected exact pet limit");
            var player=JsonUtility.FromJson<OfflinePlayerProgress>(JsonUtility.ToJson(profile.Save.Player));
            player.SubRank=subRank;player.Rank=rank;player.Level=cap+50;
            player.Equipment.SlotProgress=new List<OfflineEquipmentSlotProgress>{new OfflineEquipmentSlotProgress{
                Position=10,BaseLevel=equipCap-1,BlessLevels=new List<OfflineEquipmentBlessProgress>{
                    new OfflineEquipmentBlessProgress{SeasonId=rank,Level=20}}}};
            player.Skills.ActiveSlotLevels=new[]{skillCap-1,1,1,1};
            player.Skills.BlessRecords=new List<OfflineSkillBlessRecord>{new OfflineSkillBlessRecord{SeasonId=rank,ActiveSlotLevels=new[]{20,0,0,0}}};
            player.Pets.Pets=new List<OfflinePetInstance>{new OfflinePetInstance{Index=1,ClassId=1,Level=10000,
                Name="保留幻兽字段",Quality=3,EvolutionPhase=2,Abilities=new List<OfflinePetAbilityLevel>{new OfflinePetAbilityLevel{NodeId=1,Level=2}}}};
            string before=JsonUtility.ToJson(player);
            var view=new OfflineHomeNoBlessPlayerView(player,(Rank)rank,0,rules);
            int petCap=rules.LevelLimits.GetPetLevelLimit((SubRank)subRank,cap);
            if(petCap<=0)petCap=rules.LevelLimits.GetPetBaseLevelLimit((SubRank)subRank);
            Require(view.Player.Level==cap && view.Player.Equipment.SlotProgress[0].BaseLevel==equipCap
                && view.Player.Equipment.SlotProgress[0].BlessLevels.Count==0 && view.Player.Skills.ActiveSlotLevels[0]==skillCap
                && view.Player.Skills.BlessRecords.Count==0 && view.Player.Pets.Pets[0].Level==petCap,"original caps applied to detached real profile shapes");
            Require(view.Player.Pets.Pets[0].Name=="保留幻兽字段" && view.Player.Pets.Pets[0].Quality==3
                && view.Player.Pets.Pets[0].Abilities[0].Level==2 && JsonUtility.ToJson(player)==before,"original pet clone changes only its level and never the owner");
            var lower=new OfflineHomeNoBlessPlayerView(player,Rank.None,0,rules);
            Require(lower.Inputs.PlanesRank==1 && lower.Inputs.SeasonId==-1
                && lower.Player.Equipment.SlotProgress[0].BaseLevel==equipCap-1
                && lower.Player.Skills.ActiveSlotLevels[0]==skillCap-1,"explicit world rank selects records independently of personal rank");
        }
        var total=new Dictionary<int,long>();
        foreach(var row in json["resonances"].Cast<JObject>().OrderBy(row=>(int)row["Level"]))
        {
            foreach(var effect in ((JObject)row["Effect"]).Properties())
            {
                int id=int.Parse(effect.Name);total.TryGetValue(id,out long value);total[id]=value+(long)effect.Value;
            }
            var original=new Dictionary<int,long>();rules.Resonances.CalcEquipResonanceProps((uint)(int)row["Level"],original);
            Require(total.Count==original.Count && total.All(pair=>original[pair.Key]==pair.Value),"cumulative selected resonance effects at every activated level");
        }
        Require(JsonUtility.ToJson(profile.Save)==unchanged,"all limit and resonance checks leave actual save untouched");
        CheckPropertyChunks(profile,growth,rules,json);
        Debug.Log("HOME_NO_BLESS_RULES_CHECKED: all 1124 limit rows, all 240 cumulative resonance levels, real fresh profile and detached capped fields.");
    }

    static int I(Dictionary<string,object> row,string key)=>row!=null && row.TryGetValue(key,out var value)?Convert.ToInt32(value):0;
    static bool Equal(IReadOnlyDictionary<int,long> left,IReadOnlyDictionary<int,long> right)
        =>left.Count==right.Count && left.All(pair=>right.TryGetValue(pair.Key,out var value)&&value==pair.Value);
    static OfflinePlayerProgress Copy(OfflinePlayerProgress player)=>JsonUtility.FromJson<OfflinePlayerProgress>(JsonUtility.ToJson(player));
    static CharacterPropScoreContext Context(int rank,int season,IReadOnlyDictionary<int,long> resonance=null)
        =>new CharacterPropScoreContext{Inputs=new CharacterPropCalculationInputs(rank,season,resonance??new Dictionary<int,long>())};

    static void CheckPropertyChunks(OfflineMainlineProgressSession profile,OfflineCharacterProgressionService growth,
        OfflineHomeNoBlessConfiguration rules,JObject raw)
    {
        var catalog=growth.Catalog;
        Require(catalog.Rows("treasure").Count==1396 && catalog.Rows("pet").Count==21
            && catalog.Rows("pet_ability_prop").Count==29250,"complete selected property chunk inputs reach the actual calculator");
        var equipment=OfficialEquipmentAssetCatalog.LoadDefault().Load();
        var source=new OfflineEquipmentPropSource(equipment,catalog.RankOfSubRank);
        var player=Copy(growth.Player);
        player.Equipment.Instances.Clear();player.Equipment.Equipped.Clear();player.Equipment.SlotProgress.Clear();
        player.Level=100;
        player.Equipment.Instances.Add(new OfflineEquipmentInstance{InstanceId=1,ItemId=20028,ItemLevel=100,BlessLevel=20});
        player.Equipment.Equipped.Add(new OfflineEquippedSlot{InstanceId=1,Position=10});
        player.Equipment.SlotProgress.Add(new OfflineEquipmentSlotProgress{Position=10,BaseLevel=5});
        var blessed=source.CalculateProps(player,Context(4,0));
        var nextWorld=source.CalculateProps(player,Context(5,0));
        player.Equipment.Instances[0].BlessLevel=0;
        var unblessed=source.CalculateProps(player,Context(4,0));
        Require(Equal(nextWorld,unblessed) && !Equal(blessed,nextWorld),"equipment blessing uses explicit provider world rank independently of personal rank");

        player=Copy(growth.Player);
        player.Equipment.SlotProgress=Enumerable.Range(10,5).Select(pos=>new OfflineEquipmentSlotProgress{Position=pos,BaseLevel=10000}).ToList();
        var capped=new OfflineHomeNoBlessPlayerView(player,Rank.Norank,240,rules);
        int cap=rules.LevelLimits.GetEquipBaseLevelLimit((SubRank)player.SubRank);
        var expected=new Dictionary<int,long>();
        foreach(var row in raw["resonances"].Cast<JObject>().Where(row=>(int)row["EquipLevel"]<=cap && (int)row["EquipCount"]<=5))
            foreach(var effect in ((JObject)row["Effect"]).Properties())
            {int id=int.Parse(effect.Name);expected.TryGetValue(id,out long value);expected[id]=value+(long)effect.Value;}
        Require(expected.Count>0 && Equal(capped.Inputs.EquipResonance,expected),"active resonance is downgraded against capped upgrade slots and keeps all lower effects");
        Require(new OfflineHomeNoBlessPlayerView(player,Rank.Norank,0,rules).Inputs.EquipResonance.Count==0,
            "high upgrade levels never fabricate a manual resonance activation");
        var calculator=new CharacterPropCalculator(catalog);
        calculator.AddSource(source);calculator.AddSource(new OfflineEquipmentResonancePropSource(source));
        Require(Equal(capped.Evaluate(calculator,profile.Save.World,0).Chunks[CharacterPropHandleType.EquipResonance],expected),
            "shared chunk dispatch consumes original cumulative resonance values");

        var treasure=catalog.Rows("treasure").First(row=>Convert.ToString(row["EffectTypeName"])=="None"
            && ((List<object>)row["LevelPropTypes"]).Count>0);
        int treasureId=I(treasure,"ClassId"),element=I(catalog.Find("item",treasureId),"ElementType");
        player.CharacterProperties.Treasures=new List<OfflineCharacterTreasure>{new OfflineCharacterTreasure{
            ClassId=treasureId,Active=true,SlotIndex=-1,BaseLevel=1,Level=1,HitCount=1}};
        player.CharacterProperties.TreasureElementLevels.Clear();
        var treasureSource=new OfflineCharacterConfiguredPropSource(catalog,CharacterPropHandleType.Treasure);
        var absent=treasureSource.CalculateProps(player,null,0,Context(2,0));
        player.CharacterProperties.Treasures[0].BaseLevel=500;player.CharacterProperties.Treasures[0].Level=500;
        Require(absent.Count>0 && Equal(absent,treasureSource.CalculateProps(player,null,0,Context(2,0))),
            "an unassigned treasure with no element record uses original level one, not stale per-item levels");
        player.CharacterProperties.TreasureElementLevels.Add(new OfflineTreasureElementLevel{ElementType=element,BaseLevel=2,Level=10});
        var shared=treasureSource.CalculateProps(player,null,0,Context(2,0));
        player.CharacterProperties.TreasureElementLevels[0].Level=30;
        Require(!Equal(shared,absent) && Equal(shared,treasureSource.CalculateProps(player,null,0,Context(2,0))),
            "a differing provider world rank reads element base sharing and strips only the blessing");

        var petSource=new OfflinePetPropSource(catalog);
        var pet=catalog.Rows("pet").First();int petId=I(pet,"ClassId");
        player.Pets.Pets=new List<OfflinePetInstance>{new OfflinePetInstance{Index=1,ClassId=petId,Level=2,
            SlotIndex=-1,EvolutionPhase=1,Quality=I(catalog.Find("item",petId),"Quality")}};
        player.SeasonRank=7;
        var basePet=petSource.CalculateProps(player,Context(2,0));
        Require(basePet.Count>0 && Equal(basePet,petSource.CalculateProps(player,Context(2,-1))),
            "original missing-season sentinel reaches base pet curve");
        player.Pets.Pets[0].Level=10000;
        var petView=new OfflineHomeNoBlessPlayerView(player,Rank.Norank,0,rules);
        var projectedPet=petSource.CalculateProps(petView.Player,new CharacterPropScoreContext{Inputs=petView.Inputs});
        player.Pets.Pets[0].Level=petView.Player.Pets.Pets[0].Level;player.SeasonRank=0;
        Require(projectedPet.Count>0 && Equal(projectedPet,petSource.CalculateProps(player)),
            "original pet clamp and explicit season reach the same real base properties");
        var previous=OfficialCharacterAssetCatalog.LoadDefault().Load();
        Require(previous.Rows("treasure").Count==0 && previous.Rows("pet").Count==0,
            "attaching selected property inputs leaves the baseline catalog detached");
        Debug.Log("HOME_NO_BLESS_CHUNKS_CHECKED: equipment blessing gate, manual/capped cumulative resonance, treasure element sharing and actual pet curves.");
    }
}
