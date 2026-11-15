using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.HomePlayerHealthUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeExperienceRules;

public static class HomeResonanceProfileValidation
{
    public static void Run()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        Require(File.Exists(Path.Combine(work, ".zjcs-validation.json")), "Owned workspace required");
        string json = File.ReadAllText(Path.Combine(repository,"unity/Assets/Official/Equipment/Runtime/equipment-runtime-rules.json"));
        var catalog = OfficialEquipmentCatalog.Load(json);
        var source = new OfflineEquipmentPropSource(catalog, _ => 1);
        var player = new OfflinePlayerProgress();
        Require(source.ResonanceProps(player).Count == 0, "Original unactivated state has no resonance");
        var expected = new Dictionary<int,long>();
        var rows = JObject.Parse(json)["tables"]["equip_resonance"].Cast<JObject>().OrderBy(row=>(int)row["Level"]).ToArray();
        Require(rows.Length == 240, "Complete selected equipment resonance configuration");
        foreach (var row in rows)
        {
            foreach (var effect in ((JObject)row["Effect"]).Properties())
            {
                int id=int.Parse(effect.Name); expected.TryGetValue(id,out long value); expected[id]=value+(long)effect.Value;
            }
            player.ActivedEquipResonanceLevel=(uint)(int)row["Level"];
            Require(Equal(source.ResonanceProps(player),expected), "Actual shared source uses cumulative effects at each activated level");
        }
        player.ActivedEquipResonanceLevel=0;
        foreach (int position in Enumerable.Range(10,5))
        {
            player.Equipment.Instances.Add(new OfflineEquipmentInstance { InstanceId=position,ItemId=20028,ItemLevel=10000 });
            player.Equipment.Equipped.Add(new OfflineEquippedSlot { Position=position,InstanceId=position });
        }
        Require(source.ResonanceProps(player).Count==0,"Equipped items cannot activate resonance");
        player.ActivedEquipResonanceLevel=240;
        Require(Equal(source.ResonanceProps(player),expected),"Saved activation survives equipment changes");
        player.Equipment.Instances.Clear(); player.Equipment.Equipped.Clear();
        Require(Equal(source.ResonanceProps(player),expected),"Removing equipment does not revoke a saved activation");
        player.ActivedEquipResonanceLevel=uint.MaxValue;
        Require(source.ResonanceProps(player).Count==0,"Original out-of-range Get returns no effects");
        var slots=new ProfileSlotCatalog(Path.Combine(work,"resonance-saves")); string slot=slots.CreateSlot();
        var profile=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
        Require(profile.Save.Player.ActivedEquipResonanceLevel==0,"New profile retains original zero default");
        Require(JsonUtility.FromJson<OfflinePlayerProgress>("{}").ActivedEquipResonanceLevel==0,"Old records without the field remain unactivated");
        Require(profile.ApplyCharacterMutation(saved=>{
            saved.SubRank=(int)SubRank.Norank; saved.Rank=(int)Rank.Norank; saved.Level=1; saved.ActivedEquipResonanceLevel=240;
            saved.Equipment.SlotProgress=Enumerable.Range(10,5).Select(position=>new OfflineEquipmentSlotProgress { Position=position,BaseLevel=550 }).ToList();
        },"resonance:persist").IsApplied,"Actual activation record transaction");
        profile.Reload();
        Require(profile.Save.Player.ActivedEquipResonanceLevel==240,"Activation survives disk reload");
        var rules=OfflineHomeNoBlessConfiguration.LoadDefault();
        string unchanged=JsonUtility.ToJson(profile.Save);
        var automatic=new OfflineHomeNoBlessPlayerView(profile.Save.Player,Rank.Norank,rules);
        var explicitView=new OfflineHomeNoBlessPlayerView(profile.Save.Player,Rank.Norank,240,rules);
        Require(automatic.Inputs.EquipResonance.Count>0 && Equal(automatic.Inputs.EquipResonance,explicitView.Inputs.EquipResonance),
            "Original no-blessing decorator reads the durable activation and caps it against base upgrade slots");
        Require(JsonUtility.ToJson(profile.Save)==unchanged,"No-blessing projection never rewrites activation or equipment");
        Debug.Log("HOME_RESONANCE_PROFILE_PASSED: all 240 selected cumulative effects, independent activation, zero/old-record defaults, disk reload and original no-blessing projection.");
    }
    static bool Equal(IReadOnlyDictionary<int,long> left,IReadOnlyDictionary<int,long> right)
        => left.Count==right.Count && left.All(pair=>right.TryGetValue(pair.Key,out long value)&&value==pair.Value);
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}
