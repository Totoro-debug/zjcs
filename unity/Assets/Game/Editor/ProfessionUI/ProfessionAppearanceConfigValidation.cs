using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearanceConfigValidation
    {
        private const string Active = "Zjcs.ProfessionAppearanceConfig.Active";
        private static bool ready;
        [Serializable] private sealed class Fixture { public string manifest, path; public string[] paths; public Original original; }
        [Serializable] private sealed class WorkspaceMarker { public int format; public string repository; }
        [Serializable] private sealed class Original
        {
            public Skin[] mPlayerSkinTypes;
            public Append[] mAppendSkinConfigs;
            public AppearancePartConfig[] mPartConfigs;
            public AppearanceOverrideConfig[] mAppearanceOverrideConfigs;
            public Preset[] mAppearancePresets;
            public AppearanceHashValue[] mAppearanceHashConfigs;
            public AppearanceSkeletonPath[] mAppearanceSkeletonPaths;
            public Color MimicsMonsterColor;
        }
        [Serializable] private sealed class Skin { public int mSkinType, mBaseSkin, mCanNormalSet; }
        [Serializable] private sealed class Append { public int mAppendSkinType; public string[] mAppendSkins; }
        [Serializable] private sealed class Preset
        {
            public string mId;
            public int mSex;
            public string[] mSkinNames, mAppendSkins;
            public AppearancePresetDye[] mDyes;
        }

        static ProfessionAppearanceConfigValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                Begin(Path.Combine(ProfessionValidationWorkspace.DirectoryPath, "appearance-config-validation.json"));
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        public static void RunCurrentResourceConfig()
        {
            try
            {
                string repository = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"));
                string workspace = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
                Require(string.Equals(Directory.GetParent(workspace)?.FullName, Path.Combine(repository, "output", "validation"), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFullPath(Application.dataPath), Path.Combine(repository, "unity", "Assets"), StringComparison.OrdinalIgnoreCase), "owned source project and validation directory");
                var marker = JsonUtility.FromJson<WorkspaceMarker>(File.ReadAllText(Path.Combine(workspace, ".zjcs-validation.json")));
                Require(marker != null && marker.format == 1 && string.Equals(Path.GetFullPath(marker.repository), repository, StringComparison.OrdinalIgnoreCase), "owned workspace marker");
                Begin(Path.Combine(workspace, "appearance-config-validation.json"));
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Begin(string fixture)
        {
            ready = false;
            SessionState.SetString(Active + ".Fixture", fixture);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Application.runInBackground = true;
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
            EditorApplication.EnterPlaymode();
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Appearance configuration fixture did not finish.");
                if (!ready || !EditorApplication.isPlaying) return;
                Validate();
                Debug.Log("Profession original appearance configuration passed: selected fields and presets, ordered lookups, lazy dictionaries, duplicate failures and clone-only hash mutations. Rendering is checked by consuming UI and battle flows.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Validate()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(SessionState.GetString(Active + ".Fixture", "")));
            Require(fixture.manifest == "911_156995" && fixture.paths.Length == 2, "selected original source and both configuration copies");
            foreach (string path in fixture.paths)
                ValidateAsset(AssetDatabase.LoadAssetAtPath<AppearanceConfig>(path), fixture.original);
            Debug.Log("Current appearance configuration passed: both native copies, all fields, preset sex/skin/append/dye values, signed hashes, ordered lookups and clone-only mutations.");
        }
        private static void ValidateAsset(AppearanceConfig asset, Original e)
        {
            Require(asset != null && e.mPlayerSkinTypes.Length == 13 && e.mAppearanceHashConfigs.Length == 1909 && e.mAppearanceSkeletonPaths.Length == 502 && e.mAppearancePresets.Length == 4, "original native import and expected complete scope");
            Require(asset.MimicsMonsterColor.Equals(e.MimicsMonsterColor), "original color components");
            Require(asset.PlayerSkinTypes.Count == e.mPlayerSkinTypes.Length, "complete ordered skin list");
            for (int i = 0; i < e.mPlayerSkinTypes.Length; i++)
            {
                var skin = e.mPlayerSkinTypes[i];
                var actual = asset.PlayerSkinTypes[i];
                Require((int)actual.SkinType == skin.mSkinType && actual.BaseSkin == (skin.mBaseSkin != 0) && actual.CanNormalSet == (skin.mCanNormalSet != 0), "all original skin enum and flags");
                Require(ReferenceEquals(asset.GetPlayerSkinType(actual.SkinType), actual), "original first matching skin query");
            }
            Require(asset.GetPlayerSkinType((SkinType)1000) == null, "missing skin type returns null");
            var appends = Field<List<AppendSkinConfig>>(asset, "mAppendSkinConfigs");
            Require(appends.Count == e.mAppendSkinConfigs.Length, "complete append groups");
            for (int i = 0; i < appends.Count; i++)
            {
                var expected = e.mAppendSkinConfigs[i];
                Require((int)appends[i].AppendSkinType == expected.mAppendSkinType && appends[i].AppendSkins.SequenceEqual(expected.mAppendSkins), "all original append group values and order");
                Require(ReferenceEquals(asset.GetAppendSkins(appends[i].AppendSkinType), appends[i].AppendSkins), "append query returns the original mutable list");
            }
            Require(asset.GetAppendSkins(AppendSkinType.None) == null, "no invented None group");
            var parts = Field<List<AppearancePartConfig>>(asset, "mPartConfigs");
            Require(parts.Count == e.mPartConfigs.Length, "all part groups");
            for (int i = 0; i < parts.Count; i++)
            {
                var actual = parts[i]; var expected = e.mPartConfigs[i];
                Require(actual.SkinType == expected.SkinType && ReferenceEquals(asset.GetPartConfig(actual.SkinType), actual), "original part type and lookup");
                Require(actual.AnimConfigList.Count == expected.AnimConfigList.Count, "complete part animation aliases");
                for (int j = 0; j < actual.AnimConfigList.Count; j++)
                {
                    var alias = actual.AnimConfigList[j]; var target = expected.AnimConfigList[j];
                    Require(alias.SourceAnim == target.SourceAnim && alias.TargetAnim == target.TargetAnim && ReferenceEquals(actual.SourceKeyAnimDict[alias.SourceAnim], alias), "source alias lookup preserves original instance");
                }
                foreach (var group in actual.AnimConfigList.GroupBy(x => x.TargetAnim))
                    Require(actual.TargetKeyAnimDict[group.Key].SequenceEqual(group), "target alias lookup retains every source in original order");
                Require(actual.SourceKeyAnimDict.Count == actual.AnimConfigList.Count && actual.TargetKeyAnimDict.Count == actual.AnimConfigList.Select(x => x.TargetAnim).Distinct().Count(), "exact alias dictionary contents");
                Require(ReferenceEquals(actual.SourceKeyAnimDict, actual.SourceKeyAnimDict) && ReferenceEquals(actual.TargetKeyAnimDict, actual.TargetKeyAnimDict), "lazy alias dictionary identity");
            }
            Require(asset.GetPartConfig((SkinType)1000) == null, "missing part returns null");
            var overrides = Field<List<AppearanceOverrideConfig>>(asset, "mAppearanceOverrideConfigs");
            Require(overrides.Count == e.mAppearanceOverrideConfigs.Length, "all override groups");
            for (int i = 0; i < overrides.Count; i++)
            {
                var actual = overrides[i]; var expected = e.mAppearanceOverrideConfigs[i];
                Require(actual.Type == expected.Type && actual.MaleOverrideSkinIdList.SequenceEqual(expected.MaleOverrideSkinIdList) && actual.FemaleOverrideSkinIdList.SequenceEqual(expected.FemaleOverrideSkinIdList), "original sex-specific override IDs and order");
                Require(ReferenceEquals(asset.GetOverrideConfig(actual.Type), actual), "original override lookup identity");
            }
            Require(asset.GetOverrideConfig((AppearanceOverrideType)1000) == null, "missing override returns null");
            var presets = Field<List<AppearancePreset>>(asset, "mAppearancePresets");
            Require(presets.Count == e.mAppearancePresets.Length, "all original appearance presets");
            for (int i = 0; i < presets.Count; i++)
            {
                var actual = presets[i]; var expected = e.mAppearancePresets[i];
                Require(actual.Id == expected.mId && (int)actual.Sex == expected.mSex
                    && actual.SkinNames.SequenceEqual(expected.mSkinNames) && actual.AppendSkins.SequenceEqual(expected.mAppendSkins), "original ordered preset identity, sex and skins");
                Require(ReferenceEquals(asset.GetAppearancePreset(actual.Id), presets.First(preset => preset.Id == actual.Id)), "original first matching preset identity");
                Require(actual.Dyes.Count == expected.mDyes.Length, "all preset dyes");
                for (int j = 0; j < actual.Dyes.Count; j++)
                {
                    var dye = actual.Dyes[j]; var original = expected.mDyes[j];
                    Require(dye.DyeType == original.DyeType && dye.H == original.H && dye.S == original.S && dye.V == original.V, "unmodified preset dye channel and HSV integers");
                }
            }
            Require(asset.GetAppearancePreset("not-an-original-preset") == null, "missing preset returns null");
            Require(asset.AppearanceToHash.Count == e.mAppearanceHashConfigs.Length, "complete original skin hash list");
            for (int i = 0; i < e.mAppearanceHashConfigs.Length; i++)
                Require(asset.AppearanceToHash[i].SkinName == e.mAppearanceHashConfigs[i].SkinName && asset.AppearanceToHash[i].HashValue == e.mAppearanceHashConfigs[i].HashValue, "exact ordered signed 64-bit original hash");
            var paths = Field<List<AppearanceSkeletonPath>>(asset, "mAppearanceSkeletonPaths");
            Require(paths.Count == e.mAppearanceSkeletonPaths.Length, "complete original skeleton paths");
            for (int i = 0; i < paths.Count; i++)
                Require(paths[i].SkeletonName == e.mAppearanceSkeletonPaths[i].SkeletonName && paths[i].Path == e.mAppearanceSkeletonPaths[i].Path && asset.GetSkeletonPath(paths[i].SkeletonName) == paths[i].Path, "all original skeleton names, paths and lookup");
            Require(asset.GetSkeletonPath("not-an-original-skeleton") == string.Empty, "missing skeleton returns empty string");
            var clone = Object.Instantiate(asset);
            try
            {
                var copiedPresets = Field<List<AppearancePreset>>(clone, "mAppearancePresets");
                var duplicatePreset = JsonUtility.FromJson<AppearancePreset>(JsonUtility.ToJson(copiedPresets[copiedPresets.Count - 1]));
                copiedPresets.Insert(0, duplicatePreset);
                Require(ReferenceEquals(clone.GetAppearancePreset(duplicatePreset.Id), duplicatePreset)
                    && Field<List<AppearancePreset>>(asset, "mAppearancePresets").Count == e.mAppearancePresets.Length, "first matching preset and clone-owned list");
                string removed = e.mAppearanceHashConfigs[0].SkinName;
                clone.AddAppearanceHash(new Dictionary<string, long> { { removed, -77 }, { "fixture-z-last", long.MinValue }, { "fixture-a-first", long.MaxValue } });
                Require(clone.AppearanceToHash.Count == asset.AppearanceToHash.Count + 3 && clone.AppearanceToHash.Count(x => x.SkinName == removed) == 2, "original hash addition retains duplicates");
                for (int i = 1; i < clone.AppearanceToHash.Count; i++) Require(clone.AppearanceToHash[i - 1].SkinName.CompareTo(clone.AppearanceToHash[i].SkinName) <= 0, "original name comparison sorting");
                var remove = new List<string> { removed, "fixture-z-last" };
                clone.ClearAppearanceHash(in remove);
                Require(clone.AppearanceToHash.Count == asset.AppearanceToHash.Count && clone.AppearanceToHash.All(x => !remove.Contains(x.SkinName)) && clone.AppearanceToHash.Single(x => x.SkinName == "fixture-a-first").HashValue == long.MaxValue, "backward hash deletion removes all matches and preserves signed values");
                Require(asset.AppearanceToHash.Count == e.mAppearanceHashConfigs.Length, "validation clone does not change original config");
            }
            finally { Object.Destroy(clone); }
            ValidateLazyAndDuplicateSemantics();
        }
        private static void ValidateLazyAndDuplicateSemantics()
        {
            Require(new AppearancePartConfig().SourceKeyAnimDict == null && new AppearancePartConfig().TargetKeyAnimDict == null, "original null animation list stays null");
            var part = new AppearancePartConfig { AnimConfigList = new List<PartAnimConfig> { new PartAnimConfig { SourceAnim = "a", TargetAnim = "x" }, new PartAnimConfig { SourceAnim = "b", TargetAnim = "x" } } };
            var source = part.SourceKeyAnimDict; var target = part.TargetKeyAnimDict;
            part.AnimConfigList.Add(new PartAnimConfig { SourceAnim = "c", TargetAnim = "y" });
            Require(source.Count == 2 && target.Count == 1 && target["x"].Count == 2, "original cached dictionaries do not rebuild after source list mutation");
            var duplicate = new AppearancePartConfig { AnimConfigList = new List<PartAnimConfig> { new PartAnimConfig { SourceAnim = "a", TargetAnim = "x" }, new PartAnimConfig { SourceAnim = "a", TargetAnim = "y" } } };
            ExpectArgument(() => { var ignored = duplicate.SourceKeyAnimDict; }, "duplicate source alias is rejected by original Add");
            var config = ScriptableObject.CreateInstance<AppearanceConfig>();
            try
            {
                SetField(config, "mAppearanceSkeletonPaths", new List<AppearanceSkeletonPath> { new AppearanceSkeletonPath { SkeletonName = "a", Path = "first" }, new AppearanceSkeletonPath { SkeletonName = "a", Path = "second" } });
                ExpectArgument(() => config.GetSkeletonPath("a"), "duplicate skeleton name is rejected by original Add");
                Require(config.GetSkeletonPath("a") == "first", "original partially filled dictionary survives the duplicate exception");
            }
            finally { Object.Destroy(config); }
        }
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void SetField(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
        private static void ExpectArgument(Action action, string message)
        { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException(message); }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Appearance config: " + message); }
        private static void Finish(int code)
        { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
