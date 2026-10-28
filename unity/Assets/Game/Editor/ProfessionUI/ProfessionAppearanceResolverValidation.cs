using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearanceResolverValidation
    {
        private const string Active = "Zjcs.ProfessionAppearanceResolver.Active";
        private static bool ready;
        [Serializable] private sealed class Fixture
        {
            public string ConfigurationSha256, FiltersSha256, AllowedSha256;
            public DefaultRow[] Defaults;
        }
        [Serializable] private sealed class DefaultRow { public Sex Sex; public Profession Profession; public Skin[] Skins; }
        [Serializable] private sealed class Skin { public SkinType Type; public int Id; }
        static ProfessionAppearanceResolverValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Appearance configuration validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Original appearance resolution passed Play Mode: all seven pinned tables, 62 sex/profession defaults, all expanded blacklist/whitelist sets and ordered inherited allowed lists; original append/equipment/default precedence, bound-skin overwrite, dye and pool reuse, secondary-weapon null/zero/incompatible branches, ordered mutual exclusion and cycle propagation. Real player, character graphics, native page and Windows Player remain unbound.");
                Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("appearance-resolver-validation.json")));
            var asset = Resources.Load<TextAsset>("ProfessionUI/appearance-tables");
            Require(asset && Hash(asset.bytes) == fixture.ConfigurationSha256, "packaged current table bytes");
            var tables = ProfessionAppearanceTables.Load(asset.text);
            Require(tables.SpineSkinInfoParser.Count == 1723 && tables.CharacterSkinInfoParser.Rows.Length == 62 && tables.ProfessionBaseInfoParser.Count == 31 && tables.AppendAppearanceInfoParser.Count == 1567 && tables.CharacterSkinIdGroupInfoParser.Count == 48 && tables.CharacterSkinDyeInfoParser.Count == 3 && tables.CharacterSkinColorInfoParser.Count == 23, "all current row counts");
            var filter = new StringBuilder();
            foreach (var row in tables.SpineSkinInfoParser.GetAll())
                filter.Append(row.Id).Append(':').Append(string.Join(",", row.BlackIdSet.OrderBy(x => x))).Append(':').Append(string.Join(",", row.WhiteIdSet.OrderBy(x => x))).Append('\n');
            Require(Hash(Encoding.UTF8.GetBytes(filter.ToString())) == fixture.FiltersSha256, "all blacklist and whitelist expansions against independent Python projection");
            var allowed = new StringBuilder();
            foreach (var row in tables.CharacterSkinInfoParser.Rows)
                foreach (var type in row.TypeDic)
                    foreach (var use in type.Value)
                        allowed.Append((int)row.Gender).Append(':').Append((int)row.ProfessionLimit).Append(':').Append((int)type.Key).Append(':').Append((int)use.Key).Append(':').Append(string.Join(",", use.Value)).Append('\n');
            Require(Hash(Encoding.UTF8.GetBytes(allowed.ToString())) == fixture.AllowedSha256, "all allowed lists, inherited ordering and duplicates against independent Python projection");
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            Require(config, "original appearance config");
            var resolver = new ProfessionAppearanceResolver(tables);
            int runtimeSkins = 0;
            foreach (var row in fixture.Defaults)
            {
                var data = resolver.BuildDefaultAppearanceData(config, row.Sex, row.Profession);
                Require(data.SkinMap.Count == row.Skins.Length && data.Valid && resolver.IsAppearanceDataValid(data, config), "default base-skin coverage");
                foreach (var skin in row.Skins) Require(data.SkinMap[skin.Type].Type == skin.Type && data.SkinMap[skin.Type].SkinId == skin.Id, "original default skin " + row.Sex + "/" + row.Profession + "/" + skin.Type);
                foreach (var dye in tables.CharacterSkinDyeInfoParser.GetAll())
                {
                    var value = data.DyeMap[dye.DyeType]; var color = tables.CharacterSkinColorInfoParser.Get(dye.FixedColorIdList[0]);
                    Require(value.ColorId == color.Id && value.Hsv == new Hsv(color.H, color.S, color.V), "first fixed dye color");
                }
                var append = new List<string> { "original-extra-skin" };
                var runtime = resolver.BuildAppearanceRuntimeData(row.Sex, row.Profession, data, append);
                Require(runtime.Sex == row.Sex && ReferenceEquals(runtime.AppendSkins, append), "runtime source list identity");
                foreach (var skin in row.Skins) Require(runtime.SkinMap[skin.Type] == tables.SpineSkinInfoParser.Get(skin.Id).SkinName, "default runtime name");
                runtimeSkins += runtime.SkinMap.Count;
                var dyes = runtime.DyeMap.Values.ToArray();
                resolver.BuildAppearanceRuntimeData(row.Sex, row.Profession, data, append, runtime);
                Require(dyes.All(x => runtime.DyeMap.Values.Contains(x) || x.Count == 0), "old dye dictionaries recycled or reused");
            }
            Require(fixture.Defaults.Length == 62 && runtimeSkins > 600, "all male/female profession default runtime data");
            CheckEquipment(tables, resolver);
            CheckMutex(tables, resolver);
            CheckDye(tables, resolver);
        }
        private static void CheckEquipment(ProfessionAppearanceTables tables, ProfessionAppearanceResolver resolver)
        {
            var data = resolver.BuildDefaultAppearanceData(AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset"), Sex.Male, Profession.Fashi);
            var definition = tables.ProfessionBaseInfoParser.Get(Profession.Fashi);
            foreach (var pair in definition.DefaultSkins[Sex.Male])
                if (pair.Key == SkinType.pWeapon || pair.Key == SkinType.sWeapon) data.EquipSkinDict[pair.Key.ToEquipPos()] = pair.Value;
            data.SkinMap[SkinType.pWeapon] = new SkinData { Type = SkinType.pWeapon, SkinId = data.SkinMap[SkinType.body].SkinId };
            var effective = new HashSet<int>(); var lost = new HashSet<SkinType>();
            Require(resolver.TryGetFinalSkinId(data, SkinType.pWeapon, Sex.Male, Profession.Fashi, effective, lost, out int id) && id == definition.DefaultSkins[Sex.Male][SkinType.pWeapon], "compatible equipped weapon overrides base");
            data.EquipSkinDict[EquipPos.Weapon] = -1;
            Require(resolver.TryGetFinalSkinId(data, SkinType.pWeapon, Sex.Male, Profession.Fashi, effective, lost, out id) && id == data.SkinMap[SkinType.pWeapon].SkinId, "incompatible equipment falls back to base");
            Require(resolver.IsSkinLimit(data, SkinType.sWeapon, Sex.Male, Profession.Fashi), "absent second weapon blocks skin");
            data.EquipSkinDict[EquipPos.SecondWeapon] = 0;
            Require(!resolver.IsSkinLimit(data, SkinType.sWeapon, Sex.Male, Profession.Fashi), "explicit zero second weapon does not block");
            data.EquipSkinDict[EquipPos.SecondWeapon] = -1;
            Require(resolver.IsSkinLimit(data, SkinType.sWeapon, Sex.Male, Profession.Fashi), "incompatible second weapon blocks skin");
            data.AppendAppearanceDict[SkinType.body] = definition.AppendAppearanceRewards[Sex.Male][SkinType.body];
            Require(resolver.TryGetAppendSkinId(data, SkinType.body, Sex.Male, Profession.Fashi, out id) && tables.SpineSkinInfoParser.Get(id).SkinName == "body_200_M_01", "real mage preview reward resolves original body");
            Require(resolver.TryGetFinalSkinId(data, SkinType.body, Sex.Male, Profession.Fashi, effective, lost, out int final) && final == id, "append overrides base");
            lost.Add(SkinType.body);
            Require(resolver.TryGetFinalSkinId(data, SkinType.body, Sex.Male, Profession.Fashi, effective, lost, out final) && final == data.SkinMap[SkinType.body].SkinId, "mutex loser falls back to original base");
            var linked = tables.SpineSkinInfoParser.GetAll().First(x => x.BindingIdList.Count > 0);
            data = new AppearanceData(); data.SkinMap.Add(linked.SkinType, new SkinData { Type = linked.SkinType, SkinId = linked.Id });
            if (linked.SkinType == SkinType.sWeapon) data.EquipSkinDict[EquipPos.SecondWeapon] = 0;
            var runtime = resolver.BuildAppearanceRuntimeData(Sex.Male, Profession.Fashi, data, null);
            foreach (int bound in linked.BindingIdList)
            {
                var info = tables.SpineSkinInfoParser.Get(bound);
                Require(runtime.SkinMap[info.SkinType] == info.SkinName, "real bound skin is inserted/overwritten in source order");
            }
        }
        private static void CheckMutex(ProfessionAppearanceTables tables, ProfessionAppearanceResolver resolver)
        {
            var a = Add(tables, 900001, SkinType.body); var b = Add(tables, 900002, SkinType.hair);
            var c = Add(tables, 900003, SkinType.headDecoration); var d = Add(tables, 900004, SkinType.backDecoration);
            var appearances = new Dictionary<SkinType, int> { { a.SkinType, a.Id }, { b.SkinType, b.Id } };
            var time = new Dictionary<SkinType, DateTime>(); var effective = new HashSet<int>(); var lost = new HashSet<SkinType>(); var cycles = new List<SkinType>();
            a.BlackIdSet.Add(b.Id); b.BlackIdSet.Add(a.Id);
            resolver.ResolveAppendAppearanceMutex(appearances, time, Sex.Male, effective, lost, cycles);
            Require(effective.SetEquals(new[] { a.Id }) && lost.SetEquals(new[] { b.SkinType }) && cycles.Count == 0, "equal-time mutual conflict uses lower item ID");
            time[a.SkinType] = DateTime.MinValue.AddSeconds(1);
            resolver.ResolveAppendAppearanceMutex(appearances, time, Sex.Male, effective, lost, cycles);
            Require(effective.SetEquals(new[] { b.Id }) && lost.SetEquals(new[] { a.SkinType }), "earlier wear time precedes item ID");
            b.BlackIdSet.Clear();
            resolver.ResolveAppendAppearanceMutex(appearances, null, Sex.Male, effective, lost, cycles);
            Require(effective.SetEquals(new[] { a.Id }) && lost.Count == 0, "one-way block is filtered rather than mutex fallback");
            a.WhiteIdSet.Add(b.Id);
            resolver.ResolveAppendAppearanceMutex(appearances, null, Sex.Male, effective, lost, cycles);
            Require(effective.SetEquals(new[] { a.Id, b.Id }), "white ID exempts black ID");
            a.WhiteIdSet.Clear(); b.BlackIdSet.Add(c.Id); c.BlackIdSet.Add(a.Id); appearances.Add(c.SkinType, c.Id);
            resolver.ResolveAppendAppearanceMutex(appearances, null, Sex.Male, effective, lost, cycles);
            Require(effective.Count == 0 && lost.SetEquals(appearances.Keys) && cycles.Count == 3, "three-way block cycle removes all cycle participants");
            appearances.Add(d.SkinType, d.Id); d.BlackIdSet.Add(a.Id);
            resolver.ResolveAppendAppearanceMutex(appearances, null, Sex.Male, effective, lost, cycles);
            Require(effective.SetEquals(new[] { b.Id, d.Id }) && lost.Count == 0 && cycles.Count == 0, "external blocker resolves cycle before cycle kill");
            resolver.ResolveAppendAppearanceMutex((AppearanceData)null, Sex.Male, effective, lost, cycles);
            Require(effective.Count + lost.Count + cycles.Count == 0, "null appearance clears reused output collections");
        }
        private static SpineSkinInfo Add(ProfessionAppearanceTables tables, int id, SkinType type)
        {
            var row = new SpineSkinInfo { Id = id, SkinType = type, SkinName = "validation-only-" + id, BindingIdList = new List<int>() };
            tables.SpineSkinInfoParser.Add(id, row);
            tables.AppendAppearanceInfoParser.Add(id, new ProfessionAppendAppearanceInfo { ClassId = id, SkinDict = new Dictionary<Sex, int> { { Sex.Male, id } } });
            return row;
        }
        private static void CheckDye(ProfessionAppearanceTables tables, ProfessionAppearanceResolver resolver)
        {
            foreach (var dye in tables.CharacterSkinDyeInfoParser.GetAll())
            {
                var same = resolver.GetDyeHsvChange(dye, dye.BaseH, dye.BaseS, dye.BaseV);
                Require(Mathf.Abs(same.OffsetH) < 1e-6 && Mathf.Abs(same.OffsetScaleS) < 1e-6 && Mathf.Abs(same.OffsetScaleV) < 1e-6, "base dye is zero change in linear color space");
            }
            Require(resolver.HsvToColor(-1, -1, -1) == Color.black && resolver.HsvToColor(999, 999, 999) == Color.red, "HSV source clamping");
            resolver.BuildAppearanceRuntimeData(Sex.Male, Profession.Fashi, null, null, null);
        }
        private static string Hash(byte[] value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Appearance resolver: " + message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
