using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionSkinDefaultsValidation
    {
        private const string Active = "Zjcs.ProfessionSkinDefaults.Active";
        private static bool ready;
        static ProfessionSkinDefaultsValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Skin defaults validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Original profession skin defaults passed actual Play Mode: 62 sex/profession default branches, initial Sword/Staff/None/other branches, source skin validation/removal/dirty order, repeated equip positions, read-only local missing-category reconstruction and equipment absence/zero. No weapon graphic or full-page completion is claimed.");
                Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var rules = AssetDatabase.LoadAssetAtPath<AppearanceAnimNameCombineRule>("Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("ProfessionUI/appearance-tables").text);
            var errors = new List<string>();
            var defaults = new ProfessionAppearanceSkinDefaults(tables, Resources.Load<TextAsset>("ProfessionUI/skin-defaults").text, errors.Add);
            Require(defaults.GetEquipPosByProfession(Profession.None).SequenceEqual(new[] { (EquipPos)10, (EquipPos)10, (EquipPos)11, (EquipPos)12, (EquipPos)13 }), "original repeated equip position/order");
            var resolver = new ProfessionAppearanceResolver(tables);
            int count = 0;
            foreach (Sex sex in new[] { Sex.Male, Sex.Female })
            foreach (var row in tables.ProfessionBaseInfoParser.GetAll())
            {
                var data = new AppearanceData();
                data.SkinMap.Add(SkinType.sWeapon, new SkinData { Type = SkinType.sWeapon, SkinId = -1 });
                var expected = row.DefaultSkins[sex].Where(x => defaults.GetEquipPosByProfession(row.Profession).Contains(x.Key.ToEquipPos())).ToDictionary(x => x.Key, x => x.Value);
                int dirty = 0;
                defaults.SetProfessionDefaultSkin(sex, row.Profession, data, row.Profession, AppearanceOperateSource.ProfessionChange, source =>
                {
                    dirty++;
                    Require(source == AppearanceOperateSource.ProfessionChange && data.SkinMap.Count == expected.Count && data.SkinMap.All(x => expected[x.Key] == x.Value.SkinId), "dirty occurs after all original mutations");
                });
                Require(dirty == 1, "one source dirty event");
                Require(data.SkinMap.Keys.SequenceEqual((expected.ContainsKey(SkinType.sWeapon) ? new[] { SkinType.sWeapon, SkinType.pWeapon, SkinType.body } : new[] { SkinType.body, SkinType.pWeapon }).Where(expected.ContainsKey)), "source removal/insertion order");

                var baseData = resolver.BuildDefaultAppearanceData(config, sex, row.Profession);
                var player = new OfflinePlayerProgress { InitialWeaponType = 200 };
                player.Wardrobe.Sex = (int)sex; player.Wardrobe.Profession = (int)row.Profession;
                foreach (var pair in baseData.SkinMap)
                    player.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                string before = JsonUtility.ToJson(player);
                var graph = new OfflineProfessionAppearanceBindings(17, () => player, _ => 200, tables, config, rules, defaults);
                var snapshot = (AppearanceData)graph.AppearanceData;
                Require(snapshot.SkinMap.Take(baseData.SkinMap.Count).Select(x => x.Key).SequenceEqual(baseData.SkinMap.Keys), "saved skin order remains intact");
                if (row.Profession == Profession.None)
                    Require(snapshot.SkinMap[SkinType.pWeapon].SkinId == 35000, "original initial sword default");
                else
                    foreach (var pair in expected)
                        if (!config.GetPlayerSkinType(pair.Key).BaseSkin)
                            Require(snapshot.SkinMap[pair.Key].SkinId == pair.Value, "missing non-base category uses official default");
                Require(snapshot.EquipSkinDict.Count == 0 && snapshot.AppendAppearanceDict.Count == 0 && JsonUtility.ToJson(player) == before, "compatibility projection leaves profile/equipment/appended values intact");
                if (snapshot.SkinMap.ContainsKey(SkinType.sWeapon))
                {
                    var runtime = new AppearanceRuntimeData();
                    graph.BuildAppearanceRuntimeData(sex, (int)row.Profession, snapshot, new List<string>(), runtime);
                    Require(!runtime.SkinMap.ContainsKey(SkinType.sWeapon), "original absent secondary equipment suppresses secondary appearance");
                    snapshot.EquipSkinDict[EquipPos.SecondWeapon] = 0;
                    graph.BuildAppearanceRuntimeData(sex, (int)row.Profession, snapshot, new List<string>(), runtime);
                    Require(runtime.SkinMap.ContainsKey(SkinType.sWeapon), "explicit zero allows original base secondary skin");
                }
                count++;
            }
            Require(count == 62 && errors.Count == 0, "all current default skins validate against source character tables");
            foreach (int weapon in new[] { 200, 201, 0, 999 })
            {
                var data = new AppearanceData(); int dirty = 0;
                defaults.SetInitialWeaponSkin(Sex.Male, Profession.None, data, weapon, _ => { dirty++; Require(data.SkinMap.ContainsKey(SkinType.pWeapon), "initial dirty follows set"); });
                Require(data.SkinMap[SkinType.pWeapon].SkinId == (weapon == 200 ? 35000 : 36000) && dirty == 1, "source initial weapon conditional including None");
                defaults.SetInitialWeaponSkin(Sex.Male, Profession.None, data, 200, _ => dirty++);
                Require(dirty == 1, "existing initial weapon is not overwritten or dirtied");
            }
            var invalid = new AppearanceData();
            defaults.SetSkinById(Sex.Male, Profession.None, invalid, SkinType.pWeapon, -1);
            Require(invalid.SkinMap.Count == 0 && errors.Count == 1, "invalid source skin logs without mutation");
            defaults.SetProfessionDefaultSkin((Sex)99, Profession.None, invalid, Profession.None, AppearanceOperateSource.Normal, _ => throw new Exception("Unexpected dirty"));
            Require(invalid.SkinMap.Count == 0 && errors.Count == 2, "missing source sex returns before mutation/event");
            var empty = new OfflinePlayerProgress();
            var emptyGraph = new OfflineProfessionAppearanceBindings(18, () => empty, _ => 200, tables, config, rules, defaults);
            Require(((AppearanceData)emptyGraph.AppearanceData).SkinMap.Count == 0, "uninitialized wardrobe is not fabricated");
            empty.Wardrobe.Sex = (int)Sex.Male; empty.Wardrobe.Profession = 10;
            empty.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = 7, Id = 35000 });
            empty.Wardrobe.WornSkins.Add(new OfflineAppearanceSkin { Type = 7, Id = 0 });
            string saved = JsonUtility.ToJson(empty); emptyGraph.Refresh(AppearanceOperateSource.Normal, false);
            var preserved = (AppearanceData)emptyGraph.AppearanceData;
            Require(preserved.SkinMap.Count == 1 && preserved.SkinMap[SkinType.pWeapon].SkinId == 35000 && preserved.EquipSkinDict[EquipPos.Weapon] == 0 && JsonUtility.ToJson(empty) == saved, "existing supplied weapon and explicit equipment zero survive unchanged");
        }
        private static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
