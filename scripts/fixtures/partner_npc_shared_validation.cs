using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.PartnersNpc;
using Zjcs.Offline.World.NativeMap14SharedEntities;

[InitializeOnLoad]
public static class PartnerNpcSharedValidation
{
    private const string Active = "Zjcs.Partners.NpcRuntime.Validation";
    private static bool ready, ran;
    static PartnerNpcSharedValidation()
    {
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        EditorApplication.update += Update;
    }
    public static void Run()
    {
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
    private static void Update()
    {
        if (ran || !ready || !EditorApplication.isPlaying || !SessionState.GetBool(Active, false)) return;
        ran = true;
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var result = new Result();
        try
        {
            if (string.IsNullOrEmpty(work) || !File.Exists(Path.Combine(work, ".zjcs-validation.json")))
                throw new InvalidOperationException("Use the owned NPC validation workspace");
            var legacyCatalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            var catalog = OfficialPartnerNpcRuntimeCatalog.Load();
            using (var legacy = new NativeMap14SharedAssemblies(legacyCatalog))
            {
                using (var runtime = new OfflinePartnerNpcRuntime(catalog))
                {
                    var selected = AppDomain.CurrentDomain.GetAssemblies().Single(value => value.GetName().Name == "Zjcs.Partners884.Console");
                    Require(!ReferenceEquals(legacy.Shared, selected), "historical/current modules coexist");
                    Type parsers = selected.GetType("Parsers", true);
                    FieldInfo singleton = parsers.GetField("Instance");
                    Require(singleton.GetValue(null) == null, "initialization restores original static parser service");
                    var expected = JsonUtility.FromJson<Expected>(File.ReadAllText(Path.Combine(work, "npc-runtime-expected.json")));
                    Require(runtime.NpcClasses().OrderBy(value => value).SequenceEqual(expected.npc_classes), "all selected original NPC definitions");
                    int[] classes = runtime.CombatNpcClasses();
                    Require(classes.OrderBy(value => value).SequenceEqual(expected.combat_npc_classes), "all selected original combat NPC definitions");
                    Type subRank = selected.GetType("Common.SubRank", true);
                    int gold = Convert.ToInt32(Enum.Parse(subRank, "Gold2"));
                    int[] ranks = new[] { runtime.Norank, Convert.ToInt32(Enum.Parse(subRank, "Blackiron3")), gold }
                        .Concat(expected.reference_ranks.Where(value => value != gold)).ToArray();
                    int[] levels = new[] { 1, 20, 100 }.Concat(expected.reference_ranks.Where(value => value != gold).Select(_ => 100)).ToArray();
                    for (int index = 0; index < levels.Length; index++)
                    foreach (int npc in classes)
                    {
                        var info = runtime.CreateLiteInfo(8001L, npc, 7001L, levels[index], ranks[index], 1);
                        Require(info.TargetId == 8001L && info.OwnerPlayerId == 7001L && info.NpcClassId == npc &&
                            info.Level == levels[index] && info.SubRank == ranks[index] && info.CreatePlanesRank == 1,
                            "original target identity and requested level/rank");
                        Require(info.CombatRating >= 0 && !string.IsNullOrEmpty(info.Icon) && !string.IsNullOrEmpty(info.NameKey), "original NPC projection");
                        result.rows.Add(new Row { npc = npc, level = info.Level, rank = info.SubRank,
                            profession = info.Profession, sex = info.Sex, icon = info.Icon, nameKey = info.NameKey, rating = info.CombatRating });
                        Require(singleton.GetValue(null) == null, "query restores original static parser service");
                    }
                    var golden = runtime.CreateLiteInfo(8001L, 8402, 7001L, 1, runtime.Norank, 1);
                    Require(golden.CombatRating == 1045 && golden.Profession == Convert.ToInt32(Enum.Parse(selected.GetType("Common.Profession", true), "Fashi")) && golden.Sex == 1 &&
                        golden.Icon == "UI/NPC/Npc_Icon/Map_11/npc_icon_8402" && golden.NameKey == "ui_npc_name_8402", "previously executed original 8402 result");
                    using (var second = new OfflinePartnerNpcRuntime(catalog))
                        Require(second.CreateLiteInfo(8001L, 8402, 7001L, 1, second.Norank, 1).CombatRating == golden.CombatRating, "second configuration owner");
                    Require(runtime.CreateLiteInfo(8001L, 8402, 7001L, 1, runtime.Norank, 1).CombatRating == golden.CombatRating, "first owner survives second disposal");
                    bool rejected = false;
                    try { runtime.CreateLiteInfo(8001L, 8402, 7001L, 1, 0, 1); }
                    catch (ArgumentOutOfRangeException) { rejected = true; }
                    Require(rejected && singleton.GetValue(null) == null, "None cannot silently replace Norank");
                    rejected = false;
                    try { runtime.CreateLiteInfo(8001L, -1, 7001L, 1, runtime.Norank, 1); }
                    catch (InvalidOperationException) { rejected = true; }
                    Require(rejected && singleton.GetValue(null) == null, "invalid NPC preserves static services");
                    using (var reloadedLegacy = new NativeMap14SharedAssemblies(legacyCatalog))
                        Require(ReferenceEquals(legacy.Shared, reloadedLegacy.Shared), "historical loader can reopen after NPC queries");
                }
                using (var reloaded = new OfflinePartnerNpcRuntime(catalog))
                    Require(reloaded.CreateLiteInfo(8001L, 8402, 7001L, 1, reloaded.Norank, 1).CombatRating == 1045, "NPC runtime reload");
            }
            result.success = true;
            Debug.Log("Original NPC runtime Play Mode passed: " + result.rows.Count + " projections, map coexistence and owner reload.");
        }
        catch (Exception error) { result.error = error.ToString(); Debug.LogException(error); }
        File.WriteAllText(Path.Combine(work, "npc-runtime-result.json"), JsonUtility.ToJson(result, true));
        SessionState.SetBool(Active, false);
        EditorApplication.Exit(result.success ? 0 : 1);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Result { public bool success; public string error; public List<Row> rows = new List<Row>(); }
    [Serializable] private sealed class Expected { public int[] npc_classes, combat_npc_classes, reference_ranks; }
    [Serializable] private sealed class Row { public int npc, level, rank, profession, sex; public string icon, nameKey; public long rating; }
}
