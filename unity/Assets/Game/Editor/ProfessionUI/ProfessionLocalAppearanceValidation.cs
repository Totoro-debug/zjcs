using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionLocalAppearanceValidation
    {
        private const string Active = "Zjcs.ProfessionLocalAppearance.Active";
        private static bool ready;
        static ProfessionLocalAppearanceValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Local appearance validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Local profession appearance passed actual Play Mode: local save-schema projection, original current table resolution and animation names, atomic refresh/event lifecycle, self lite-info, all ten protocol clone fields and nulls, and original profession preview preserving the local profile. Character endpoint remains a capture fixture; full page, actor graphics, mouse flow and Windows Player remain unbound.");
                Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var rules = AssetDatabase.LoadAssetAtPath<AppearanceAnimNameCombineRule>("Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("ProfessionUI/appearance-tables").text);
            var resolver = new ProfessionAppearanceResolver(tables);
            var defaults = resolver.BuildDefaultAppearanceData(config, Sex.Male, Profession.Fashi);
            var player = new OfflinePlayerProgress { InitialWeaponType = 201 };
            player.Wardrobe.Sex = (int)Sex.Male;
            player.Wardrobe.Profession = (int)Profession.Fashi;
            foreach (var pair in defaults.SkinMap) player.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
            foreach (var pair in defaults.DyeMap) player.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId, Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
            player.Wardrobe.WornItems.Add(new OfflineAppearanceSkin { Type = (int)SkinType.body, Id = 35501 });
            player.Wardrobe.WearTimes.Add(new OfflineAppearanceWearTime { Type = (int)SkinType.body, UnixSeconds = 1700000000 });
            player.Wardrobe.WornSkins.Add(new OfflineAppearanceSkin { Type = (int)SkinType.pWeapon, Id = 0 });
            player.Wardrobe.WornTitleId = 12345;
            player.Wardrobe.AppearanceLevel = 6;
            string savePath = ProfessionValidationWorkspace.Output("local-appearance-profile.json");
            string saved = JsonUtility.ToJson(player);
            File.WriteAllText(savePath, saved);
            player = JsonUtility.FromJson<OfflinePlayerProgress>(File.ReadAllText(savePath));
            int? currentItemType = 200;
            var graph = new OfflineProfessionAppearanceBindings(17, () => player, _ => currentItemType, tables, config, rules);
            Require(graph.MainPlayer == graph && graph.Player == graph && graph.Id == 17 && graph.Sex == Sex.Male && graph.Profession == 10 && graph.Config == config && graph.AnimationRules == rules, "selected local player identity/configuration");
            var snapshot = (AppearanceData)graph.AppearanceData;
            Require(graph.IsAppearanceValid(snapshot) && snapshot.SkinMap.Count == defaults.SkinMap.Count && graph.GetSkinNames(snapshot).SequenceEqual(defaults.SkinMap.Values.Select(x => resolver.GetSkinName(x.SkinId))), "saved base skin order and real source skin names");
            Require(snapshot.AppearanceLevel == 6 && snapshot.HonorItemDict[HonorType.Title] == 12345 && snapshot.AppendAppearanceWearTimeDict[SkinType.body] == new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc), "level, title and exact Unix-to-UTC wear time");
            Require(snapshot.EquipSkinDict.Count == 1 && snapshot.EquipSkinDict[EquipPos.Weapon] == 0 && !snapshot.EquipSkinDict.ContainsKey(EquipPos.SecondWeapon), "local skin-type to original equipment-position mapping and absent/zero");
            foreach (var dye in defaults.DyeMap) Require(snapshot.DyeMap[dye.Key].ColorId == dye.Value.ColorId && snapshot.DyeMap[dye.Key].Hsv == dye.Value.Hsv, "saved dye values");
            var runtime = new AppearanceRuntimeData();
            graph.BuildAppearanceRuntimeData(Sex.Male, 10, snapshot, config.GetAppendSkins(AppendSkinType.UI), runtime);
            Require(runtime.SkinMap[SkinType.body] == "body_200_M_01", "real saved mage outfit item resolves through current original tables");
            foreach (var row in tables.ProfessionBaseInfoParser.GetAll())
            {
                if (row.Profession == Profession.None) continue;
                var root = row; while (root.PrePro != Profession.None) root = tables.ProfessionBaseInfoParser.Get(root.PrePro);
                Require(graph.GetBaseProfession((int)row.Profession) == (int)root.Profession && graph.GetProfessionIdStr((int)row.Profession) == (root.Profession == Profession.Fashi ? "200" : "100"), "all original profession chains");
            }
            Require(graph.GetBaseProfession(0) == 10, "initial weapon fallback without equipment");
            player.Equipment.Equipped.Add(new OfflineEquippedSlot { Position = 10, InstanceId = 9 });
            graph.Refresh(AppearanceOperateSource.Normal, false);
            Require(graph.GetBaseProfession(0) == 1, "equipped object with missing inventory item means None, not initial weapon");
            player.Equipment.Instances.Add(new OfflineEquipmentInstance { InstanceId = 9, ItemId = 99 });
            graph.Refresh(AppearanceOperateSource.Normal, false);
            Require(graph.GetBaseProfession(0) == 1, "equipped sword");
            currentItemType = 201; graph.Refresh(AppearanceOperateSource.Normal, false);
            Require(graph.GetBaseProfession(0) == 10 && ProfessionAppearanceAnimationNames.GetAnimName(graph, rules, "profession_sex", new List<string> { "idle", "1" }, Sex.Male, 0) == "200/idle_M_01", "equipped staff and original animation rule");
            CheckClone(graph, snapshot);
            var node = new GameObject("Original local appearance event lifecycle");
            try
            {
                var probe = node.AddComponent<Listener>(); probe.ConfigurePlayerAppearance(graph); probe.Init(17);
                Require(probe.Count == 1 && ReferenceEquals(probe.Last, graph.AppearanceData), "original self refresh receives real local snapshot");
                var old = (AppearanceData)graph.AppearanceData;
                graph.Refresh(AppearanceOperateSource.Map); graph.Refresh(AppearanceOperateSource.ProfessionPromote);
                Require(probe.Count == 1, "original event filters");
                graph.Refresh(AppearanceOperateSource.ProfessionChange);
                Require(probe.Count == 2 && ReferenceEquals(probe.Last, graph.AppearanceData) && ((AppearanceData)probe.Last).ModifyTimeStamp > old.ModifyTimeStamp, "snapshot replaced before event, monotonic local portrait revision");
                node.SetActive(false); graph.Refresh(AppearanceOperateSource.Normal); Require(probe.Count == 2, "disabled listener unsubscribes");
                node.SetActive(true); Require(probe.Count == 3, "enable refreshes current snapshot");
                old = (AppearanceData)graph.AppearanceData; int count = probe.Count;
                currentItemType = null;
                Throws(() => graph.Refresh(AppearanceOperateSource.Normal));
                Require(ReferenceEquals(graph.AppearanceData, old) && count == probe.Count, "missing current item type does not publish partial state");
                currentItemType = 201;
                ProfessionPlayerLiteAppearance lite = null;
                graph.GetLiteInfo(17, new ProfessionLiteInfoCallback(v => lite = v), true);
                Require(lite != null && lite.Sex == graph.Sex && lite.Profession == graph.Profession && ReferenceEquals(lite.AppearanceData, graph.AppearanceData), "local lite info uses latest data");
                Throws(() => graph.GetLiteInfo(99, new ProfessionLiteInfoCallback(_ => { }), false));
                CheckPreview(graph, config, node);
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
            Require(File.ReadAllText(savePath) == saved, "projection, refresh and previews never write the selected save");
        }
        private static void CheckClone(OfflineProfessionAppearanceBindings graph, AppearanceData source)
        {
            source.FullBodyAppearanceId = 123; source.LimitItemExpSet.Add(456);
            source.SkinMap.Add(SkinType.pWeapon, null); source.DyeMap.Add((DyeType)99, null);
            var view = graph.CloneAppearance(source); var clone = (AppearanceData)view.Source;
            Require(!ReferenceEquals(clone, source) && ReferenceEquals(view.AppendAppearanceDict, clone.AppendAppearanceDict) && ReferenceEquals(view.EquipSkinDict, clone.EquipSkinDict), "preview dictionary views address the clone");
            Require(clone.ModifyTimeStamp == source.ModifyTimeStamp && clone.AppearanceLevel == source.AppearanceLevel && clone.FullBodyAppearanceId == 123 && clone.LimitItemExpSet.SetEquals(source.LimitItemExpSet) && clone.HonorItemDict.SequenceEqual(source.HonorItemDict) && clone.AppendAppearanceWearTimeDict.SequenceEqual(source.AppendAppearanceWearTimeDict) && clone.AppendAppearanceDict.SequenceEqual(source.AppendAppearanceDict) && clone.EquipSkinDict.SequenceEqual(source.EquipSkinDict), "all ten original protocol fields survive cloning");
            Require(clone.SkinMap.Keys.SequenceEqual(source.SkinMap.Keys) && clone.SkinMap[SkinType.pWeapon] == null && clone.DyeMap[(DyeType)99] == null && !ReferenceEquals(clone.SkinMap[SkinType.body], source.SkinMap[SkinType.body]) && !ReferenceEquals(clone.DyeMap.First().Value, source.DyeMap.First().Value), "nested values, nulls and order");
            clone.SkinMap[SkinType.body].SkinId = -1; clone.DyeMap.First().Value.ColorId = -1; clone.LimitItemExpSet.Clear(); clone.AppendAppearanceDict.Clear(); clone.EquipSkinDict.Clear(); clone.HonorItemDict.Clear(); clone.AppendAppearanceWearTimeDict.Clear();
            Require(source.SkinMap[SkinType.body].SkinId > 0 && source.DyeMap.First().Value.ColorId > 0 && source.LimitItemExpSet.Count == 1 && source.AppendAppearanceDict.Count == 1 && source.EquipSkinDict.Count == 1 && source.HonorItemDict.Count == 1 && source.AppendAppearanceWearTimeDict.Count == 1, "clone mutations cannot reach source");
            clone = OfflineProfessionAppearanceBindings.Clone(new AppearanceData { SkinMap = null, DyeMap = null, AppendAppearanceDict = null, EquipSkinDict = null, HonorItemDict = null, LimitItemExpSet = null, AppendAppearanceWearTimeDict = null });
            Require(clone.SkinMap == null && clone.DyeMap == null && clone.AppendAppearanceDict == null && clone.EquipSkinDict == null && clone.HonorItemDict == null && clone.LimitItemExpSet == null && clone.AppendAppearanceWearTimeDict == null, "serialization clone's null collection values preserved");
        }
        private static void CheckPreview(OfflineProfessionAppearanceBindings graph, AppearanceConfig config, GameObject node)
        {
            var source = (AppearanceData)graph.AppearanceData;
            var before = OfflineProfessionAppearanceBindings.Clone(source);
            var endpoint = node.AddComponent<Capture>();
            var character = node.AddComponent<AppearanceCharacter>(); character.Configure(new CharacterBindings(config));
            ProfessionAppearancePreview.SetProfessionAppearance(graph, config, character, graph.Sex, 10, source, AppendSkinType.UI);
            Require(endpoint.Skins.Contains("body_200_M_01") && character.TryGetSkinName(SkinType.body, out string body) && body == "body_200_M_01", "original preview resolves and delivers real mage body");
            ProfessionAppearancePreview.SetProfessionAppearance(graph, config, character, graph.Sex, 1, source, AppendSkinType.UI);
            Require(endpoint.Skins.Contains("body_100_M_01"), "original preview changes to actual warrior appearance reward");
            Require(source.AppendAppearanceDict.SequenceEqual(before.AppendAppearanceDict) && source.EquipSkinDict.SequenceEqual(before.EquipSkinDict) && source.SkinMap.All(p => p.Value.SkinId == before.SkinMap[p.Key].SkinId), "cross-profession previews preserve original local snapshot");
        }
        public sealed class Listener : PlayerAppearanceInfo
        {
            public int Count; public object Last;
            public override void RefreshAppearance(long playerId, Sex sex, int profession, object data) { Count++; Last = data; }
        }
        public sealed class Capture : MonoBehaviour, IAppearanceComponent
        {
            public List<string> Skins; public bool Valid => true;
            public void Init(ScriptableObject main, ScriptableObject split) { }
            public void SetSkins(List<string> skins) { Skins = new List<string>(skins); }
            public void SetDye(string name, Dictionary<int,HsvChange> values) { }
            public bool TryGetMaterial(string name, out Material material) { material = null; return false; }
            public void Release(bool tryUnloadAllAssets = false) { }
        }
        private sealed class CharacterBindings : IProfessionAppearanceCharacterBindings
        {
            public AppearanceConfig Config { get; }
            public CharacterBindings(AppearanceConfig config) { Config = config; }
            public IProfessionAppearanceCharacterAsset LoadAsset(string path) => new Asset(Config);
            public void SetPartSkins(Component handler, Dictionary<SkinType,string> skins) => throw new InvalidOperationException("Native part renderer is outside the local-data validation.");
            public void ReleaseParts(Component handler, bool all) => throw new InvalidOperationException("Native part renderer is outside the local-data validation.");
        }
        private sealed class Asset : IProfessionAppearanceCharacterAsset
        { public ScriptableObject Result { get; } public Asset(ScriptableObject value) { Result = value; } public void Dispose() { } }
        private static void Throws(Action action) { bool threw = false; try { action(); } catch (InvalidOperationException) { threw = true; } Require(threw, "unsupported binding must report its actual gap"); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Local appearance: " + message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
